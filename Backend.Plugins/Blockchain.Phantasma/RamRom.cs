using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Backend.Api;
using Backend.Commons;
using Backend.PluginEngine;
using Database.Main;
using Microsoft.EntityFrameworkCore;
using PhantasmaPhoenix.Cryptography;
using PhantasmaPhoenix.Cryptography.Extensions;
using PhantasmaPhoenix.VM;
using Serilog;
using Address = PhantasmaPhoenix.Cryptography.Address;
using Nft = Database.Main.Nft;

namespace Backend.Blockchain;

public struct TokenProperty
{
    public string Key;
    public string Value;
}

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    // When we reach this number of loaded ROMs/RAMs, we save received/processed results.
    private const int MaxRomRamUpdatesForOneSession = 100;
    private const int ChunkPerRequest = 20;


    private static string GetPropertyValue(IEnumerable<TokenProperty> properties, string key)
    {
        if (properties == null || string.IsNullOrEmpty(key))
            return null;

        foreach (var property in properties)
        {
            if (string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        }

        return null;
    }


    private void NewNftsSetRomRam(int chainId, string chainName)
    {
        int updatedNftCount;

        do
        {
            var startTime = DateTime.Now;

            using MainDbContext databaseContext = new();
            updatedNftCount = HandleNftsRomRamBatch(chainId, chainName, databaseContext, startTime);
        } while (updatedNftCount > 0);
    }


    private static bool TryGetRpcError(JsonElement responseRoot, out string error)
    {
        error = null;

        if (responseRoot.ValueKind != JsonValueKind.Object)
            return false;

        if (responseRoot.TryGetProperty("error", out var errorProperty))
            error = errorProperty.ValueKind == JsonValueKind.String ? errorProperty.GetString() : errorProperty.ToString();
        else if (responseRoot.TryGetProperty("Error", out var carbonErrorProperty))
            error = carbonErrorProperty.ValueKind == JsonValueKind.String ? carbonErrorProperty.GetString() : carbonErrorProperty.ToString();

        return !string.IsNullOrWhiteSpace(error);
    }

    private JsonDocument ApiRequestJsonWithFixes(string url, string symbol, string idsForLog)
    {
        var response = Client.ApiRequest<JsonDocument>(url, out var stringResponse, null, 10);
        if (response != null)
            return response;

        Log.Warning(
            "[{Name}][RAM/ROM update] Response is null, raw response is {StringResponse} for {Symbol} NFT(s) with id(s) {ID}",
            Name, stringResponse, symbol, idsForLog);

        if (!string.IsNullOrEmpty(stringResponse) &&
             string.Equals(symbol, "GAME", StringComparison.OrdinalIgnoreCase))
        {
            // Hack for binary data inside JSON.
            var cutFrom = stringResponse.IndexOf(",{\"Key\" : \"OriginalMetadata\"",
                StringComparison.InvariantCulture);
            if (cutFrom > 0)
            {
                var hackedResponse = stringResponse[..cutFrom] + "]}";

                if (hackedResponse.Contains(
                        "\"creatorAddress\" : \"P2K9ih2iuscWbHT3eyzcjdX4UyEbyY44aJnukdQAS8C2WUY\""))
                    // Hack for broken links inside NFT descriptions.
                    hackedResponse = hackedResponse.Replace(
                        "P2KJ5JxtdZcnh9EXuQ1unq3FmLZzNKgXYoTTJNVH9rSvqkL",
                        "P2K9ih2iuscWbHT3eyzcjdX4UyEbyY44aJnukdQAS8C2WUY");

                try
                {
                    response = JsonDocument.Parse(hackedResponse);
                }
                catch (Exception e)
                {
                    Log.Error(
                        "[{Name}] GAME hack parsing error:\n{Message}\nHacked response: {StringResponse}",
                        Name, e.Message, hackedResponse);
                }
            }
        }

        // Would make JSON invalid, should be fixed on the node.
        Utils.ReplaceCharacter(ref stringResponse, ref response, @"\u000X", Name);
        return response;
    }

	    private int HandleNftsRomRamBatch(int chainId, string chainName, MainDbContext databaseContext, DateTime startTime)
	    {
	        var updatedNftCount = 0;

        // We only hydrate metadata for "ghost" NFTs (ROM is null). This keeps the load bounded and avoids
        // repeatedly re-fetching metadata for already-hydrated NFTs.
        var nfts = databaseContext.Nfts
            .Include(x => x.Contract)
            .Include(x => x.Series)
            .Where(x => x.ChainId == chainId && x.ROM == null && x.BURNED != true)
            .Take(MaxRomRamUpdatesForOneSession)
            .ToList();

	        if (nfts.Count == 0)
	            return 0;

	        // Some legacy rows contain TOKEN_ID values decoded as *signed* BigInteger (negative),
	        // while the node RPC expects token IDs as unsigned 256-bit integers.
	        // Normalize these IDs in-place to stop repeated "invalid ID" errors and allow hydration.
	        //
	        // TODO(legacy): Once all legacy rows are backfilled/normalized in DB, and upstream decoding stops producing
	        // negative token IDs, remove this in-place normalization.
	        //
	        // Important: legacy DB may contain *both* the negative (signed) and normalized (unsigned) representations
	        // of the same token id in separate rows. In that case, normalizing TOKEN_ID would violate the unique index
	        // (ContractId, TOKEN_ID), so we merge the duplicate row into the canonical one and delete the duplicate.
	        var removedNftIds = new HashSet<int>();
	        foreach (var nft in nfts)
	        {
	            if (TryNormalizeTokenIdText(nft.TOKEN_ID, out var normalized))
	            {
	                var old = nft.TOKEN_ID;
	                var contractId = nft.ContractId;

	                var canonicalNftId = databaseContext.Nfts
	                    .Where(x => x.ContractId == contractId && x.TOKEN_ID == normalized)
	                    .Select(x => x.ID)
	                    .FirstOrDefault();

	                if (canonicalNftId > 0 && canonicalNftId != nft.ID)
	                {
	                    MergeDuplicateNftRow(databaseContext, duplicateNftId: nft.ID, canonicalNftId, oldTokenId: old,
	                        canonicalTokenId: normalized);
	                    databaseContext.Nfts.Remove(nft);
	                    removedNftIds.Add(nft.ID);
	                    Log.Warning(
	                        "[{Name}] Removed duplicate NFT row after TOKEN_ID normalization conflict (symbol {Symbol}): duplicate NftDbId {DuplicateId} ({OldTokenId}) -> canonical NftDbId {CanonicalId} ({CanonicalTokenId})",
	                        Name, nft.Contract?.SYMBOL, nft.ID, old, canonicalNftId, normalized);
	                    continue;
	                }

	                nft.TOKEN_ID = normalized;

	                // Keep event filters consistent: update Events.TOKEN_ID where it references this NFT row.
	                databaseContext.Database.ExecuteSqlInterpolated(
	                    $@"UPDATE ""Events"" SET ""TOKEN_ID"" = {normalized} WHERE ""NftId"" = {nft.ID} AND ""TOKEN_ID"" = {old};");

	                Log.Warning(
	                    "[{Name}] Normalized negative TOKEN_ID for {Symbol} NFT row {NftDbId}: {OldTokenId} -> {NewTokenId}",
	                    Name, nft.Contract?.SYMBOL, nft.ID, old, normalized);
	            }
	        }

	        foreach (var symbolGroup in nfts
	                     .Where(x => !removedNftIds.Contains(x.ID))
	                     .Where(x => x.Contract != null && !string.IsNullOrWhiteSpace(x.Contract.SYMBOL))
	                     .GroupBy(x => x.Contract.SYMBOL, StringComparer.OrdinalIgnoreCase))
	        {
	            var symbol = symbolGroup.Key;
	            var nftsByTokenId = new Dictionary<string, Nft>(StringComparer.Ordinal);
            foreach (var nft in symbolGroup)
            {
                if (string.IsNullOrWhiteSpace(nft.TOKEN_ID))
                    continue;

                if (!nftsByTokenId.TryAdd(nft.TOKEN_ID, nft))
                {
                    Log.Warning(
                        "[{Name}] Duplicate TOKEN_ID {TokenId} for symbol {Symbol} in RAM/ROM batch (dropping NftDbId {DroppedId})",
                        Name, nft.TOKEN_ID, symbol, nft.ID);
                }
            }

            foreach (var chunk in nftsByTokenId.Keys.Chunk(ChunkPerRequest))
            {
                updatedNftCount += UpdateNftChunk(databaseContext, chainId, symbol, chunk.ToList(), nftsByTokenId);
            }
        }

        try
        {
            databaseContext.SaveChanges();
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains(
                    "Database operation expected to affect 1 row(s) but actually affected 0 row(s)."))
            {
                var attemptTime = DateTime.Now - startTime;
                Log.Warning(
                    "[{Name}] RAM/ROM update failed because some NFTs were deleted in another thread, attempt took {AttemptTime} sec",
                    Name, Math.Round(attemptTime.TotalSeconds, 3));
            }
            else
                // Unknown exception, throwing further.
                ExceptionDispatchInfo.Capture(ex).Throw();
        }

        var updateTime = DateTime.Now - startTime;
        if (updateTime.TotalSeconds > 1 || updatedNftCount > 0)
        {
            Log.Information(
                "[{Name}] RAM/ROM update took {UpdateTime} sec, {UpdatedNftCount} NFTs updated",
                Name, Math.Round(updateTime.TotalSeconds, 3), updatedNftCount);
        }

	        return updatedNftCount;
	    }

	    private void MergeDuplicateNftRow(MainDbContext databaseContext, int duplicateNftId, int canonicalNftId,
	        string oldTokenId, string canonicalTokenId)
	    {
	        if (duplicateNftId <= 0 || canonicalNftId <= 0 || duplicateNftId == canonicalNftId)
	            return;

	        // Move references off the duplicate row so we can safely delete it.
	        // We update both by NftId and by the legacy token_id string to catch cases where the relationship was not set.
	        databaseContext.Database.ExecuteSqlInterpolated(
	            $@"UPDATE ""Events"" SET ""NftId"" = {canonicalNftId}, ""TOKEN_ID"" = {canonicalTokenId} WHERE ""NftId"" = {duplicateNftId};");
	        databaseContext.Database.ExecuteSqlInterpolated(
	            $@"UPDATE ""Events"" SET ""NftId"" = {canonicalNftId}, ""TOKEN_ID"" = {canonicalTokenId} WHERE ""NftId"" IS NULL AND ""ContractId"" = (SELECT ""ContractId"" FROM ""Nfts"" WHERE ""ID"" = {canonicalNftId}) AND ""TOKEN_ID"" = {oldTokenId};");

	        databaseContext.Database.ExecuteSqlInterpolated(
	            $@"UPDATE ""Infusions"" SET ""NftId"" = {canonicalNftId} WHERE ""NftId"" = {duplicateNftId};");
	        databaseContext.Database.ExecuteSqlInterpolated(
	            $@"UPDATE ""Nfts"" SET ""InfusedIntoId"" = {canonicalNftId} WHERE ""InfusedIntoId"" = {duplicateNftId};");

	        // NftOwnerships has a unique index on (AddressId, NftId); move rows when possible and drop redundant ones.
	        databaseContext.Database.ExecuteSqlInterpolated(
	            $@"
UPDATE ""NftOwnerships"" o
SET ""NftId"" = {canonicalNftId}
WHERE ""NftId"" = {duplicateNftId}
  AND NOT EXISTS (
    SELECT 1 FROM ""NftOwnerships"" o2
    WHERE o2.""AddressId"" = o.""AddressId"" AND o2.""NftId"" = {canonicalNftId}
  );");
	        databaseContext.Database.ExecuteSqlInterpolated(
	            $@"DELETE FROM ""NftOwnerships"" WHERE ""NftId"" = {duplicateNftId};");
	    }

    private int UpdateNftChunk(MainDbContext databaseContext, int chainId, string symbol, List<string> tokenIds,
        Dictionary<string, Nft> nftsByTokenId)
    {
        if (tokenIds == null || tokenIds.Count == 0)
            return 0;

        if (tokenIds.Count == 1)
            return UpdateSingleNft(databaseContext, chainId, symbol, tokenIds[0], nftsByTokenId);

        var idsJoined = string.Join(",", tokenIds);
        var url = $"{Settings.Default.GetRest()}/api/v1/getNFTs?symbol={symbol.ToUpper()}&IDtext={idsJoined}&extended=true";

        var response = ApiRequestJsonWithFixes(url, symbol, idsJoined);
        if (response == null || response.RootElement.ValueKind != JsonValueKind.Array)
        {
            // Some nodes fail the whole batch when a single ID is missing; split to isolate the bad ID(s).
            var mid = tokenIds.Count / 2;
            var left = tokenIds.Take(mid).ToList();
            var right = tokenIds.Skip(mid).ToList();
            return UpdateNftChunk(databaseContext, chainId, symbol, left, nftsByTokenId) +
                   UpdateNftChunk(databaseContext, chainId, symbol, right, nftsByTokenId);
        }

        var updated = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var element in response.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
                continue;

            if (!element.TryGetProperty("id", out var idProperty))
                continue;

            var tokenId = idProperty.GetString();
            if (string.IsNullOrWhiteSpace(tokenId))
                continue;

            seen.Add(tokenId);

            if (!nftsByTokenId.TryGetValue(tokenId, out var nft))
                continue;

            if (TryApplyNftRpcResponse(databaseContext, chainId, nft, symbol, element))
                updated++;
        }

        // Correctness: if the node returned a partial array, fall back to single-item queries for missing IDs.
        foreach (var tokenId in tokenIds)
        {
            if (seen.Contains(tokenId))
                continue;

            updated += UpdateSingleNft(databaseContext, chainId, symbol, tokenId, nftsByTokenId);
        }

        return updated;
    }

    private int UpdateSingleNft(MainDbContext databaseContext, int chainId, string symbol, string tokenId,
        Dictionary<string, Nft> nftsByTokenId)
    {
        if (string.IsNullOrWhiteSpace(tokenId))
            return 0;

        if (!nftsByTokenId.TryGetValue(tokenId, out var nft) || nft == null)
            return 0;

        Log.Verbose("[{Name}] checking NFT, Symbol {Symbol}, Token Id {Token}", Name, symbol, tokenId);

        var url = $"{Settings.Default.GetRest()}/api/v1/getNFT?symbol={symbol.ToUpper()}&IDtext={tokenId}&extended=true";
        var response = ApiRequestJsonWithFixes(url, symbol, tokenId);
        if (response == null || response.RootElement.ValueKind != JsonValueKind.Object)
            return 0;

        return TryApplyNftRpcResponse(databaseContext, chainId, nft, symbol, response.RootElement) ? 1 : 0;
    }

    private bool TryApplyNftRpcResponse(MainDbContext databaseContext, int chainId, Nft nft, string symbol,
        JsonElement responseRoot)
    {
        if (nft == null)
            return false;

        if (TryGetRpcError(responseRoot, out var error))
        {
            var lower = error.ToLowerInvariant();

            if (lower.Contains("invalid cast: expected"))
            {
                // NFT is broken, getNFT chain call is failing.
                // TODO introduce blacklisting instead of burning.
                nft.BURNED = true;
                nft.DM_UNIX_SECONDS = UnixSeconds.Now();
                Log.Error("[{Name}] NFT {ID} is broken, chain error: {Error}", Name, nft.TOKEN_ID, error);
                return true;
            }

            if (lower.Contains("nft does not exists") ||
                 (lower.Contains("nft") && lower.Contains("does not exist")) ||
                 lower.Contains("id not found"))
            {
                // NFT was burned (or otherwise not found), marking it.
                nft.BURNED = true;
                nft.DM_UNIX_SECONDS = UnixSeconds.Now();
                return true;
            }

            Log.Error("[{Name}] Request error for {Symbol} NFT {ID}: {Error}", Name, symbol, nft.TOKEN_ID, error);
            return false;
        }

        nft.DM_UNIX_SECONDS = UnixSeconds.Now();

        // Saving getNFT results for future use.
        try
        {
            nft.CHAIN_API_RESPONSE = JsonDocument.Parse(responseRoot.GetRawText());
        }
        catch
        {
            // ignore: metadata below is still the primary source for UI and API fields.
        }

        // Reading properties
        List<TokenProperty> properties = new();
        if (responseRoot.TryGetProperty("properties", out var propertiesNode) &&
             propertiesNode.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in propertiesNode.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                    continue;

                if (!entry.TryGetProperty("key", out var keyNode))
                    continue;

                TokenProperty property = new()
                {
                    Key = keyNode.GetString()
                };

                if (entry.TryGetProperty("value", out var valueProperty))
                    property.Value = valueProperty.GetString();

                properties.Add(property);
            }
        }

        if (responseRoot.TryGetProperty("creatorAddress", out var creatorNode))
        {
            var creator = creatorNode.GetString();
            if (!string.IsNullOrWhiteSpace(creator))
                nft.CreatorAddress = AddressMethods.Upsert(databaseContext, chainId, creator);
        }

        string seriesId = null;
        if (responseRoot.TryGetProperty("series", out var seriesNode))
            seriesId = seriesNode.GetString();

        if (!string.IsNullOrWhiteSpace(seriesId))
        {
            // Series IDs can exceed Int64 (21+ digits on some chains). We store them as strings.
            nft.Series = SeriesMethods.Upsert(databaseContext, nft.ContractId, seriesId);
            if (nft.Series?.ID > 0)
                nft.SeriesId = nft.Series.ID;
        }

        if (responseRoot.TryGetProperty("mint", out var mintNode))
        {
            var mintString = mintNode.GetString();
            if (int.TryParse(mintString, out var mintNumber))
                nft.MINT_NUMBER = mintNumber;
        }

        if (responseRoot.TryGetProperty("rom", out var romNode))
            nft.ROM = romNode.GetString();

        if (responseRoot.TryGetProperty("ram", out var ramNode))
            nft.RAM = ramNode.GetString();

        // Parsing ROM
        byte[] romBytes = Array.Empty<byte>();
        if (!string.IsNullOrWhiteSpace(nft.ROM))
        {
            try
            {
                romBytes = nft.ROM.Decode();
            }
            catch
            {
                romBytes = Array.Empty<byte>();
            }
        }

        IRom parsedRom = symbol?.ToUpperInvariant() switch
        {
            "CROWN" => new CrownRom(romBytes),
            "TTRS" => new DummyRom(romBytes),
            _ => new CustomRom(romBytes, $"{symbol}#{nft.TOKEN_ID}")
        };

        // For cases when NFT was created not from mint event, try to get date from ROM.
        if (nft.MINT_DATE_UNIX_SECONDS == 0)
        {
            var date = parsedRom.GetDate();
            nft.MINT_DATE_UNIX_SECONDS = date;
        }

        nft.NAME = GetPropertyValue(properties, "name");
        nft.DESCRIPTION = GetPropertyValue(properties, "description");
        nft.IMAGE = NormalizeImageUrl(GetPropertyValue(properties, "imageURL"));
        nft.INFO_URL = GetPropertyValue(properties, "infoURL");

        // Filling Series with available information, if needed.
        if (nft.Series != null && nft.Series.SERIES_ID != null)
        {
            nft.Series.CreatorAddressId = nft.CreatorAddressId;
            nft.Series.NAME = nft.NAME;
            nft.Series.DESCRIPTION = nft.DESCRIPTION;
            nft.Series.IMAGE = NormalizeImageUrl(nft.IMAGE);
            nft.Series.ROYALTIES =
                decimal.TryParse(GetPropertyValue(properties, "royalties"), out var royalties)
                    ? royalties
                    : 0;

            nft.Series.TYPE = (int)parsedRom.GetNftType();

            nft.Series.ATTR_TYPE_1 = GetPropertyValue(properties, "attrT1");
            nft.Series.ATTR_VALUE_1 = GetPropertyValue(properties, "attrV1");
            nft.Series.ATTR_TYPE_2 = GetPropertyValue(properties, "attrT2");
            nft.Series.ATTR_VALUE_2 = GetPropertyValue(properties, "attrV2");
            nft.Series.ATTR_TYPE_3 = GetPropertyValue(properties, "attrT3");
            nft.Series.ATTR_VALUE_3 = GetPropertyValue(properties, "attrV3");
            nft.Series.HAS_LOCKED = parsedRom.GetHasLocked();

            nft.Series.DM_UNIX_SECONDS = UnixSeconds.Now();
        }

        var metadata = ConvertPropertiesToMetadata(properties);
        AddIfNotEmpty(metadata, "token_id", nft.TOKEN_ID);
        AddIfNotEmpty(metadata, "creatorAddress", nft.CreatorAddress?.ADDRESS);
        AddIfNotEmpty(metadata, "series", seriesId);
        AddIfNotEmpty(metadata, "rom", nft.ROM);
        AddIfNotEmpty(metadata, "ram", nft.RAM);
        AddIfNotEmpty(metadata, "mint", nft.MINT_NUMBER.ToString());
        AddIfNotEmpty(metadata, "mint_date", nft.MINT_DATE_UNIX_SECONDS > 0
            ? nft.MINT_DATE_UNIX_SECONDS.ToString()
            : null);
        AddIfNotEmpty(metadata, "name", nft.NAME);
        AddIfNotEmpty(metadata, "description", nft.DESCRIPTION);
        AddIfNotEmpty(metadata, "imageURL", nft.IMAGE);
        AddIfNotEmpty(metadata, "infoURL", nft.INFO_URL);

        UpdateNftMetadata(nft, metadata);

        return true;
    }


    private List<Nft> GetNFTs(int chainId, string chainName, MainDbContext databaseContext)
    {
        var nfts = databaseContext.Nfts
            .Where(x => x.ChainId == chainId && x.ROM == null && x.BURNED != true
                /*x.Contract.SYMBOL.ToUpper() == "GHOST"*/).Take(MaxRomRamUpdatesForOneSession).ToList();

        // If we have available quota per iteration, adding other NFTs
        // 0.7 to avoid doing 2nd query for just couple or so NFTs
        if (nfts.Count < 0.7 * MaxRomRamUpdatesForOneSession)
        {
            var nftsOthers = databaseContext.Nfts
                .Where(x => x.ChainId == chainId && x.ROM == null && x.BURNED != true
                    /*x.Contract.SYMBOL.ToUpper() != "GHOST"*/)
                .Take(MaxRomRamUpdatesForOneSession - nfts.Count).ToList();
            nfts.AddRange(nftsOthers);
        }

        return nfts;
    }


    private void HandleNullResponse(JsonDocument response, string stringResponse, string symbol, string nftIDs)
    {
        if (response == null)
        {
            Log.Warning(
                "[{Name}][RAM/ROM update] Response in null, raw response is {StringResponse} for {Symbol} NFT with id {ID}",
                Name, stringResponse, symbol, nftIDs);

            if (symbol.ToUpper() == "GAME")
            {
                // Hack for binary data inside JSON
                var cutFrom = stringResponse.IndexOf(",{\"Key\" : \"OriginalMetadata\"",
                    StringComparison.InvariantCulture);
                stringResponse = stringResponse[..cutFrom] + "]}";

                if (stringResponse.Contains(
                        "\"creatorAddress\" : \"P2K9ih2iuscWbHT3eyzcjdX4UyEbyY44aJnukdQAS8C2WUY\""))
                    // Hack for broken links inside NFT descriptions
                    stringResponse = stringResponse.Replace(
                        "P2KJ5JxtdZcnh9EXuQ1unq3FmLZzNKgXYoTTJNVH9rSvqkL",
                        "P2K9ih2iuscWbHT3eyzcjdX4UyEbyY44aJnukdQAS8C2WUY");

                try
                {
                    response = JsonDocument.Parse(stringResponse);
                }
                catch (Exception e)
                {
                    Log.Error(
                        "[{Name}] GAME hack parsing error:\n{Message}\nHacked response: {StringResponse}",
                        Name, e.Message, stringResponse);
                }
            }


            //would make json invalid, should be fixed on the node 
            Utils.ReplaceCharacter(ref stringResponse, ref response, @"\u000X", Name);
        }
    }

    private void HandleMultipleNFTs(int chainId, string chainName, MainDbContext databaseContext, DateTime startTime, ref int updatedNftCount)
    {
        var nfts = GetNFTs(chainId, chainName, databaseContext);


        updatedNftCount = 0;
        List<string> contractSymbols = nfts.Select(nft => nft.Contract.SYMBOL).Distinct().ToList();
        foreach (var symbol in contractSymbols)
        {
            var numNFTs = nfts.Where(nft => nft.Contract.SYMBOL.ToUpper().Equals(symbol.ToUpper())).Count();
            for (int i = 0; i < numNFTs / ChunkPerRequest; i++)
            {
                var chunkArray = nfts.Where(nft => nft.Contract.SYMBOL.ToUpper().Equals(symbol.ToUpper())).Chunk(ChunkPerRequest);
                string requestIDs = string.Join(",", chunkArray);
                Log.Verbose("[{Name}] checking NFTs, Symbol {Symbol}, Token Id {Token}", Name, symbol, requestIDs);
                var url = $"{Settings.Default.GetRest()}/api/v1/getNFTs?symbol=" + symbol.ToUpper() +
                          "&IDtext=" + requestIDs + "&extended=true";

                var response = Client.ApiRequest<JsonDocument>(url, out var stringResponse, null, 10);
                HandleNullResponse(response, stringResponse, symbol, requestIDs);


            }
        }

    }


    public interface IRom
    {
        string GetName();
        string GetDescription();
        long GetDate();
        BigInteger GetNftType();
        bool GetHasLocked();
    }

    public class CrownRom : IRom
    {
        private long _date;
        private Address _staker;


        public CrownRom(byte[] rom)
        {
            using MemoryStream stream = new(rom);
            using BinaryReader reader = new(stream);
            UnserializeData(reader);
        }


        public string GetName()
        {
            return "Crown NFT";
        }


        public string GetDescription()
        {
            return "";
        }


        public long GetDate()
        {
            return _date;
        }


        public BigInteger GetNftType()
        {
            return 0;
        }


        public bool GetHasLocked()
        {
            return false;
        }


        private void UnserializeData(BinaryReader reader)
        {
            _staker = reader.ReadAddress();
            _date = reader.ReadUInt32();
        }
    }

    public class CustomRom : IRom
    {
        private readonly Dictionary<VMObject, VMObject> _fields = new();


        public CustomRom(byte[] romBytes, string context = null)
        {
            try
            {
                var rom = VMObject.FromBytes(romBytes);
                if (rom.Type == VMType.Struct)
                    _fields = (Dictionary<VMObject, VMObject>)rom.Data;
                else
                    Log.Warning("[PHA][CustomRom] Cannot parse ROM{Context}", BuildContextSuffix(context));
            }
            catch (Exception e)
            {
                Log.Warning("[PHA][CustomRom] ROM parsing failed{Context}: {Exception}", BuildContextSuffix(context),
                    e.Message);
            }
        }

        private static string BuildContextSuffix(string context)
        {
            return string.IsNullOrWhiteSpace(context) ? string.Empty : $" for {context}";
        }


        public string GetName()
        {
            return _fields.TryGetValue(VMObject.FromObject("name"), out var value) ? value.AsString() : "";
        }


        public string GetDescription()
        {
            return _fields.TryGetValue(VMObject.FromObject("description"), out var value) ? value.AsString() : "";
        }


        public long GetDate()
        {
            return _fields.TryGetValue(VMObject.FromObject("created"), out var value)
                ? value.AsTimestamp().Value
                : 0;
        }


        public BigInteger GetNftType()
        {
            if (_fields.TryGetValue(VMObject.FromObject("typeNFT"), out var value)) return value.AsNumber();

            // TODO - remove, for old testnet NFTs
            if (_fields.TryGetValue(VMObject.FromObject("type"), out value)) return value.AsNumber();

            // TODO - sounds better, no?
            return _fields.TryGetValue(VMObject.FromObject("nftType"), out value) ? value.AsNumber() : 0;
        }


        public bool GetHasLocked()
        {
            try
            {
                if (_fields.TryGetValue(VMObject.FromObject("hasLocked"), out var value))
                {
                    Log.Verbose("[PHA][CustomRom] ROM hasLocked {Value}", value);
                    //TODO maybe fix
                    //return value.GetType() == typeof(String) ? value.ToString().Equals("1") : value.AsBool();
                    return value.ToString().Equals("1");
                }
            }
            catch (Exception e)
            {
                Log.Error("[PHA][CustomRom] ROM parsing error in GetHasLocked(): {Exception}", e.Message);
            }

            return false;
        }
    }

    public class DummyRom : IRom
    {
        public DummyRom(byte[] rom)
        {
        }


        public string GetName()
        {
            return null;
        }


        public string GetDescription()
        {
            return null;
        }


        public long GetDate()
        {
            return 0;
        }


        public BigInteger GetNftType()
        {
            return 0;
        }


        public bool GetHasLocked()
        {
            return false;
        }
    }
}
