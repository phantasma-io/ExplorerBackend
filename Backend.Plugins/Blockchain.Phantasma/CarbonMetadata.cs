using System;
#nullable enable
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.EntityFrameworkCore;
using PhantasmaPhoenix.Cryptography;
using PhantasmaPhoenix.Protocol;
using PhantasmaPhoenix.Protocol.Carbon;
using PhantasmaPhoenix.Protocol.Carbon.Blockchain;
using PhantasmaPhoenix.Protocol.Carbon.Blockchain.Modules;
using PhantasmaPhoenix.Protocol.Carbon.Blockchain.Vm;
using Serilog;
using Transaction = Database.Main.Transaction;

namespace Backend.Blockchain;

public partial class PhantasmaPlugin
{
    private readonly ConcurrentDictionary<string, TokenSchemas> _carbonTokenSchemasCache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, CarbonSeriesCacheEntry> _carbonSeriesCache =
        new(StringComparer.OrdinalIgnoreCase);

    private sealed record CarbonSeriesCacheEntry(TokenSchemas Schemas, VmDynamicStruct? SeriesMetadata, byte[] SharedRom);

    private static string BuildTokenCacheKey(int chainId, string symbol)
    {
        return $"{chainId}:{symbol}";
    }

    private static string BuildSeriesCacheKey(int chainId, string symbol, uint carbonSeriesId)
    {
        return $"{chainId}:{symbol}:{carbonSeriesId}";
    }

    private static bool HasData(byte[]? value)
    {
        return value is {Length: > 0};
    }

    private static Token? FindTrackedToken(MainDbContext databaseContext, int chainId, string symbol)
    {
        return databaseContext.ChangeTracker.Entries<Token>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(x => x.ChainId == chainId &&
                                 x.SYMBOL.Equals(symbol, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryParseCarbonTokenSchemas(byte[] schemaBytes, string symbol, string chainName,
        out TokenSchemas parsed)
    {
        try
        {
            parsed = CarbonBlob.New<TokenSchemas>(schemaBytes);
            return true;
        }
        catch ( Exception e )
        {
            Log.Warning("[{Name}][Metadata] Failed to parse carbon token schemas for {Symbol} on {Chain}: {Message}",
                nameof(PhantasmaPlugin), symbol, chainName, e.Message);
            parsed = default;
            return false;
        }
    }

    private async Task<Transaction?> GetCreateTransactionAsync(MainDbContext databaseContext, Token token)
    {
        if ( token?.CreateEvent?.Transaction != null )
            return token.CreateEvent.Transaction;

        if ( token?.CreateEventId.HasValue == true )
        {
            var createEvent = await databaseContext.Events
                .Include(x => x.Transaction)
                .SingleOrDefaultAsync(x => x.ID == token.CreateEventId.Value);

            if ( createEvent != null )
                token.CreateEvent = createEvent;

            return createEvent?.Transaction;
        }

        return null;
    }

    private byte[] ExtractCarbonSchemasFromCreateTransaction(Transaction? createTransaction, string symbol)
    {
        if ( createTransaction == null || !createTransaction.CARBON_TX_TYPE.HasValue ||
             string.IsNullOrWhiteSpace(createTransaction.CARBON_TX_DATA) )
            return Array.Empty<byte>();

        try
        {
            var carbonType = ( TxTypes ) createTransaction.CARBON_TX_TYPE.Value;
            if ( carbonType != TxTypes.Call )
                return Array.Empty<byte>();

            var carbonBytes = Base16.Decode(createTransaction.CARBON_TX_DATA);
            if ( carbonBytes == null || carbonBytes.Length == 0 )
                return Array.Empty<byte>();
            var call = CarbonBlob.New<TxMsgCall>(carbonBytes);

            if ( call.moduleId != ( uint ) ModuleId.Token ||
                 call.methodId != ( uint ) TokenContract_Methods.CreateToken )
                return Array.Empty<byte>();

            var tokenInfo = CarbonBlob.New<TokenInfo>(call.args);
            return tokenInfo.tokenSchemas ?? Array.Empty<byte>();
        }
        catch ( Exception e )
        {
            Log.Warning("[{Name}][Metadata] Failed to extract carbon schemas for {Symbol} from tx {Hash}: {Message}",
                Name, symbol, createTransaction?.HASH, e.Message);
            return Array.Empty<byte>();
        }
    }

    private async Task<TokenSchemas?> GetCarbonTokenSchemasAsync(MainDbContext databaseContext, Chain chain,
        string symbol)
    {
        if ( databaseContext == null || chain == null || string.IsNullOrWhiteSpace(symbol) )
            return null;

        var cacheKey = BuildTokenCacheKey(chain.ID, symbol);
        if ( _carbonTokenSchemasCache.TryGetValue(cacheKey, out var cached) )
            return cached;

        var token = FindTrackedToken(databaseContext, chain.ID, symbol);

        if ( token == null )
        {
            token = await databaseContext.Tokens
                .Include(x => x.CreateEvent)
                .ThenInclude(x => x.Transaction)
                .SingleOrDefaultAsync(x => x.ChainId == chain.ID && x.SYMBOL == symbol);
        }

        if ( token == null )
            return null;

        var schemasBytes = token.CARBON_TOKEN_SCHEMAS;

        if ( !HasData(schemasBytes) )
        {
            var createTransaction = await GetCreateTransactionAsync(databaseContext, token);
            schemasBytes = ExtractCarbonSchemasFromCreateTransaction(createTransaction, symbol);

            if ( HasData(schemasBytes) )
                token.CARBON_TOKEN_SCHEMAS = schemasBytes;
        }

        if ( !HasData(schemasBytes) )
            return null;

        if ( !TryParseCarbonTokenSchemas(schemasBytes, symbol, chain.NAME, out var parsedSchemas) )
            return null;

        _carbonTokenSchemasCache[cacheKey] = parsedSchemas;
        return parsedSchemas;
    }

    private static bool TryDecodeCarbonStruct(VmStructSchema schema, byte[] data, string context,
        out VmDynamicStruct result)
    {
        try
        {
            result = VmDynamicStruct.New(schema, data);
            return true;
        }
        catch ( Exception e )
        {
            var hint = e.Message.IndexOf("BigInt too big", StringComparison.OrdinalIgnoreCase) >= 0
                ? " Schema expects Int256 first; payload looks like placeholder bytes (for example AA) instead of a VM struct."
                : string.Empty;
            Log.Warning("[{Name}][Metadata] Failed to decode carbon struct for {Context}: {Message} (dataLen={Length}){Hint}", nameof(PhantasmaPlugin),
                context, e.Message, data?.Length ?? 0, hint);
            result = default;
            return false;
        }
    }

    private static bool TryGetField(VmDynamicStruct structData, string name, out VmDynamicVariable value)
    {
        foreach ( var field in structData.fields )
        {
            if ( field.name.data.Equals(name, StringComparison.OrdinalIgnoreCase) )
            {
                value = field.value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? GetStringField(VmDynamicStruct structData, string name)
    {
        if ( TryGetField(structData, name, out var value) )
        {
            try
            {
                return value.type == VmType.String ? value.GetString() : value.data?.ToString();
            }
            catch
            {
                // ignored
            }
        }

        return null;
    }

    private static int? GetIntField(VmDynamicStruct structData, string name)
    {
        if ( TryGetField(structData, name, out var value) )
        {
            try
            {
                return value.type switch
                {
                    VmType.Int8 => value.GetInt8(),
                    VmType.Int16 => value.GetInt16(),
                    VmType.Int32 => value.GetInt32(),
                    VmType.Int64 => ( int ) value.GetInt64(),
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static byte[] GetBytesField(VmDynamicStruct structData, string name)
    {
        if ( TryGetField(structData, name, out var value) )
        {
            try
            {
                return value.type == VmType.Bytes ? value.GetBytes() : Array.Empty<byte>();
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }

        return Array.Empty<byte>();
    }

    private static string NormalizeImageUrl(string? url)
    {
        if ( string.IsNullOrWhiteSpace(url) )
            return url;

        var trimmed = url.Trim();
        if ( trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase) )
            return trimmed;

        if ( trimmed.Contains("://", StringComparison.Ordinal) )
            return trimmed;

        if ( trimmed.StartsWith("//", StringComparison.Ordinal) )
            return $"https:{trimmed}";

        return $"https://{trimmed}";
    }

    private void CacheCarbonSeries(int chainId, string symbol, uint carbonSeriesId, TokenSchemas schemas,
        VmDynamicStruct? seriesMetadata, byte[] sharedRom)
    {
        var cacheKey = BuildSeriesCacheKey(chainId, symbol, carbonSeriesId);
        var entry = new CarbonSeriesCacheEntry(schemas, seriesMetadata, sharedRom ?? Array.Empty<byte>());
        _carbonSeriesCache[cacheKey] = entry;
    }

    private CarbonSeriesCacheEntry? GetCachedSeries(int chainId, string symbol, uint carbonSeriesId)
    {
        var cacheKey = BuildSeriesCacheKey(chainId, symbol, carbonSeriesId);
        return _carbonSeriesCache.TryGetValue(cacheKey, out var cached) ? cached : null;
    }

    private CarbonSeriesCacheEntry? ProcessCarbonSeriesMetadata(MainDbContext databaseContext, Series series,
        int chainId, string symbol, uint carbonSeriesId, TokenSchemas schemas, byte[] metadataBytes)
    {
        VmDynamicStruct? metadataStruct = null;
        byte[] sharedRom = Array.Empty<byte>();

        if ( metadataBytes is {Length: > 0} &&
             TryDecodeCarbonStruct(schemas.seriesMetadata, metadataBytes,
                 $"series:{symbol}:{carbonSeriesId}:metadata", out var decodedMetadata) )
        {
            metadataStruct = decodedMetadata;

            var modeValue = GetIntField(decodedMetadata, "mode");
            var modeName = modeValue.HasValue && modeValue.Value != 0 ? "Duplicated" : "Unique";
            series.SeriesMode = SeriesModeMethods.Upsert(databaseContext, modeName, false);

            sharedRom = GetBytesField(decodedMetadata, "rom");
            if ( sharedRom.Length > 0 &&
                 TryDecodeCarbonStruct(schemas.rom, sharedRom,
                     $"series:{symbol}:{carbonSeriesId}:rom", out var sharedRomStruct) )
            {
                ApplyRomMetadataToSeries(series, sharedRomStruct);
            }
        }

        series.DM_UNIX_SECONDS = UnixSeconds.Now();

        CacheCarbonSeries(chainId, symbol, carbonSeriesId, schemas, metadataStruct, sharedRom);
        return GetCachedSeries(chainId, symbol, carbonSeriesId);
    }

    private void ProcessCarbonMint(Nft nft, string symbol, int chainId, TxMsgMintNonFungible carbonMintTx,
        TokenSchemas tokenSchemas)
    {
        if ( nft == null )
            return;

        VmDynamicStruct? sharedRomStruct = null;
        byte[] sharedRomBytes = Array.Empty<byte>();
        if ( carbonMintTx.seriesId > 0 )
        {
            var cachedSeries = GetCachedSeries(chainId, symbol, carbonMintTx.seriesId);
            if ( cachedSeries != null && cachedSeries.SharedRom.Length > 0 )
            {
                sharedRomBytes = cachedSeries.SharedRom;
                if ( TryDecodeCarbonStruct(tokenSchemas.rom, sharedRomBytes,
                         $"series:{symbol}:{carbonMintTx.seriesId}:sharedRom", out var decodedSharedRom) )
                    sharedRomStruct = decodedSharedRom;
            }
        }

        if ( carbonMintTx.rom == null || carbonMintTx.rom.Length == 0 )
        {
            if ( sharedRomStruct.HasValue )
                ApplyRomMetadataToNft(nft, sharedRomStruct.Value, sharedRomBytes,
                    carbonMintTx.ram ?? Array.Empty<byte>(), sharedRomStruct);

            return;
        }

        if ( !TryDecodeCarbonStruct(tokenSchemas.rom, carbonMintTx.rom,
                 $"nft:{symbol}:{carbonMintTx.seriesId}:rom", out var romStruct) )
        {
            if ( sharedRomStruct.HasValue )
            {
                var romBytes = sharedRomBytes.Length > 0 ? sharedRomBytes : carbonMintTx.rom ?? Array.Empty<byte>();
                ApplyRomMetadataToNft(nft, sharedRomStruct.Value, romBytes,
                    carbonMintTx.ram ?? Array.Empty<byte>(), sharedRomStruct);
            }
            else
            {
                if ( carbonMintTx.rom is {Length: > 0} ) nft.ROM = Base16.Encode(carbonMintTx.rom);
                if ( carbonMintTx.ram is {Length: > 0} ) nft.RAM = Base16.Encode(carbonMintTx.ram);
                nft.DM_UNIX_SECONDS = UnixSeconds.Now();
            }

            return;
        }

        ApplyRomMetadataToNft(nft, romStruct, carbonMintTx.rom ?? Array.Empty<byte>(),
            carbonMintTx.ram ?? Array.Empty<byte>(), sharedRomStruct);
    }

    private static void ApplyRomMetadataToSeries(Series series, VmDynamicStruct romStruct)
    {
        var name = GetStringField(romStruct, "name");
        var description = GetStringField(romStruct, "description");
        var image = NormalizeImageUrl(GetStringField(romStruct, "imageURL"));
        var royalties = GetIntField(romStruct, "royalties");

        if ( !string.IsNullOrWhiteSpace(name) ) series.NAME = name;
        if ( !string.IsNullOrWhiteSpace(description) ) series.DESCRIPTION = description;
        if ( !string.IsNullOrWhiteSpace(image) ) series.IMAGE = image;
        if ( royalties.HasValue ) series.ROYALTIES = royalties.Value;
    }

    private void ApplyRomMetadataToNft(Nft nft, VmDynamicStruct romStruct, byte[] carbonRomBytes, byte[] carbonRamBytes,
        VmDynamicStruct? sharedRomStruct)
    {
        if ( nft == null )
            return;

        var name = GetStringField(romStruct, "name") ?? (sharedRomStruct.HasValue ? GetStringField(sharedRomStruct.Value, "name") : null);
        var description = GetStringField(romStruct, "description") ??
                          (sharedRomStruct.HasValue ? GetStringField(sharedRomStruct.Value, "description") : null);
        var image = NormalizeImageUrl(GetStringField(romStruct, "imageURL") ??
                    (sharedRomStruct.HasValue ? GetStringField(sharedRomStruct.Value, "imageURL") : null));
        var infoUrl = GetStringField(romStruct, "infoURL") ??
                      (sharedRomStruct.HasValue ? GetStringField(sharedRomStruct.Value, "infoURL") : null);
        var royalties = GetIntField(romStruct, "royalties") ??
                        (sharedRomStruct.HasValue ? GetIntField(sharedRomStruct.Value, "royalties") : null);

        if ( !string.IsNullOrWhiteSpace(name) ) nft.NAME = name;
        if ( !string.IsNullOrWhiteSpace(description) ) nft.DESCRIPTION = description;
        if ( !string.IsNullOrWhiteSpace(image) ) nft.IMAGE = image;
        if ( !string.IsNullOrWhiteSpace(infoUrl) ) nft.INFO_URL = infoUrl;

        if ( royalties.HasValue && nft.Series != null )
            nft.Series.ROYALTIES = royalties.Value;

        if ( carbonRomBytes is {Length: > 0} ) nft.ROM = Base16.Encode(carbonRomBytes);
        if ( carbonRamBytes is {Length: > 0} ) nft.RAM = Base16.Encode(carbonRamBytes);

        var innerRom = GetBytesField(romStruct, "rom");
        if ( innerRom.Length > 0 )
        {
            try
            {
                var parsedRom = new CustomRom(innerRom, $"nft:{nft.TOKEN_ID}:innerRom");
                var mintDate = parsedRom.GetDate();
                if ( mintDate > 0 )
                    nft.MINT_DATE_UNIX_SECONDS = mintDate;

                if ( nft.Series != null )
                {
                    nft.Series.TYPE = ( int ) parsedRom.GetNftType();
                    nft.Series.HAS_LOCKED = parsedRom.GetHasLocked();
                }
            }
            catch ( Exception e )
            {
                Log.Warning("[{Name}][Metadata] Failed to parse inner ROM for NFT {TokenId}: {Message}", Name,
                    nft.TOKEN_ID, e.Message);
            }
        }

        nft.DM_UNIX_SECONDS = UnixSeconds.Now();
        if ( nft.Series != null ) nft.Series.DM_UNIX_SECONDS = UnixSeconds.Now();
    }
}
