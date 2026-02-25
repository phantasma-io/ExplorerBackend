using System;
#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Backend.Api;
using Backend.Commons;
using Backend.PluginEngine;
using Database.Main;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PhantasmaPhoenix.Cryptography;
using PhantasmaPhoenix.Protocol;
using PhantasmaPhoenix.Protocol.Carbon;
using PhantasmaPhoenix.Protocol.Carbon.Blockchain;
using PhantasmaPhoenix.Protocol.Carbon.Blockchain.Modules;
using PhantasmaPhoenix.Protocol.Carbon.Blockchain.Vm;
using PhantasmaPhoenix.Protocol.ExtendedEvents;
using PhantasmaPhoenix.RPC.Models;
using Serilog;
using System.Text.Json;
using System.Text.Json.Serialization;
using Address = PhantasmaPhoenix.Cryptography.Address;
using ChainMethods = Database.Main.ChainMethods;
using ContractMethods = Database.Main.ContractMethods;
using EventKind = PhantasmaPhoenix.Protocol.EventKind;
using Nft = Database.Main.Nft;
using NftMethods = Database.Main.NftMethods;
using RpcBlockResult = PhantasmaPhoenix.RPC.Models.BlockResult;
using CommonsUtils = Backend.Commons.Utils;

namespace Backend.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    private const int FetchBlocksPerIterationMax = 50;
    private long _balanceRefetchDate = 0;
    private string _balanceRefetchTimestampKey = "BALANCE_REFETCH_TIMESTAMP";
    private static readonly JsonSerializerOptions _payloadJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = null
    };

    private static Dictionary<string, object?> InitPayload(string eventKind, string chainName, string contractHash, string address)
    {
        return new Dictionary<string, object?>
        {
            ["event_kind"] = eventKind,
            ["chain"] = chainName,
            ["contract"] = contractHash,
            ["address"] = address
        };
    }

    private static string NormalizeAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return "NULL";

        if (address.Equals("[Null address]", StringComparison.OrdinalIgnoreCase))
            return "NULL";

        return address;
    }

    private static void ApplySeriesSupplyFromNftLifecycle(MainDbContext databaseContext, Nft nft, EventKind kind)
    {
        if (nft?.SeriesId == null)
            return;

        var series = nft.Series ??
                     databaseContext.Serieses.FirstOrDefault(x => x.ID == nft.SeriesId.Value) ??
                     DbHelper.GetTracked<Series>(databaseContext).FirstOrDefault(x => x.ID == nft.SeriesId.Value);

        if (series == null)
            return;

        switch (kind)
        {
            case EventKind.TokenMint:
                // Increment supply only when token becomes active.
                // This guards against duplicate mint events for an already active NFT.
                if (nft.BURNED == false)
                    return;

                nft.BURNED = false;
                series.CURRENT_SUPPLY += 1;
                break;

            case EventKind.TokenBurn:
                // Decrement supply only on first burn transition.
                if (nft.BURNED == true)
                    return;

                nft.BURNED = true;
                if (series.CURRENT_SUPPLY > 0)
                    series.CURRENT_SUPPLY -= 1;
                break;
        }
    }

    private static void FinalizePayload(Database.Main.Event? eventEntry, Dictionary<string, object?>? payload,
        string rawData)
    {
        if (eventEntry == null || payload == null)
        {
            return;
        }

        if (!payload.ContainsKey("token_id") && !string.IsNullOrEmpty(eventEntry.TOKEN_ID))
        {
            payload["token_id"] = eventEntry.TOKEN_ID;
        }

        if (eventEntry.TargetAddress != null && !payload.ContainsKey("address_event"))
        {
            payload["address_event"] = new Dictionary<string, object?>
            {
                ["address"] = eventEntry.TargetAddress.ADDRESS
            };
        }

        eventEntry.PAYLOAD_JSON = JsonSerializer.Serialize(payload, _payloadJsonOptions);
        eventEntry.PAYLOAD_FORMAT = "live.v1";
        eventEntry.RAW_DATA = rawData;
    }

    private static Dictionary<string, object?> BuildGasConfigPayload(GasConfig config)
    {
        return new Dictionary<string, object?>
        {
            ["version"] = config.version.ToString(),
            ["max_name_length"] = config.maxNameLength.ToString(),
            ["max_token_symbol_length"] = config.maxTokenSymbolLength.ToString(),
            ["fee_shift"] = config.feeShift.ToString(),
            ["max_structure_size"] = config.maxStructureSize.ToString(),
            ["fee_multiplier"] = config.feeMultiplier.ToString(),
            ["gas_token_id"] = config.gasTokenId.ToString(),
            ["data_token_id"] = config.dataTokenId.ToString(),
            ["minimum_gas_offer"] = config.minimumGasOffer.ToString(),
            ["data_escrow_per_row"] = config.dataEscrowPerRow.ToString(),
            ["gas_fee_transfer"] = config.gasFeeTransfer.ToString(),
            ["gas_fee_query"] = config.gasFeeQuery.ToString(),
            ["gas_fee_create_token_base"] = config.gasFeeCreateTokenBase.ToString(),
            ["gas_fee_create_token_symbol"] = config.gasFeeCreateTokenSymbol.ToString(),
            ["gas_fee_create_token_series"] = config.gasFeeCreateTokenSeries.ToString(),
            ["gas_fee_per_byte"] = config.gasFeePerByte.ToString(),
            ["gas_fee_register_name"] = config.gasFeeRegisterName.ToString(),
            ["gas_burn_ratio_mul"] = config.gasBurnRatioMul.ToString(),
            ["gas_burn_ratio_shift"] = config.gasBurnRatioShift.ToString()
        };
    }

    private static Dictionary<string, object?> BuildChainConfigPayload(ChainConfig config)
    {
        return new Dictionary<string, object?>
        {
            ["version"] = config.version.ToString(),
            ["reserved_1"] = config.reserved1.ToString(),
            ["reserved_2"] = config.reserved2.ToString(),
            ["reserved_3"] = config.reserved3.ToString(),
            ["allowed_tx_types"] = config.allowedTxTypes.ToString(),
            ["expiry_window"] = config.expiryWindow.ToString(),
            ["block_rate_target"] = config.blockRateTarget.ToString()
        };
    }

    private static Dictionary<string, object?> BuildSpecialResolutionPayload(SpecialResolutionData data)
    {
        return new Dictionary<string, object?>
        {
            ["resolution_id"] = data.ResolutionId.ToString(CultureInfo.InvariantCulture),
            ["description"] = data.Description,
            ["calls"] = BuildSpecialResolutionCallPayloads(data.Calls)
        };
    }

    private static object[] BuildSpecialResolutionCallPayloads(SpecialResolutionCall[] calls)
    {
        if (calls == null || calls.Length == 0)
            return Array.Empty<object>();

        return calls.Select(call => new Dictionary<string, object?>
        {
            ["module"] = call.Module,
            ["module_id"] = call.ModuleId,
            ["method"] = call.Method,
            ["method_id"] = call.MethodId,
            ["arguments"] = call.Arguments,
            ["calls"] = BuildSpecialResolutionCallPayloads(call.Calls ?? Array.Empty<SpecialResolutionCall>())
        }).ToArray();
    }

    private async Task ApplyInfusionAsync(MainDbContext databaseContext, Chain chain, Nft? nft,
        string infusedSymbol, string infusedValueRaw)
    {
        if (nft == null)
        {
            Log.Warning("[{Name}][Blocks] Infusion event without target NFT", Name);
            return;
        }

        var infusedToken = await TokenMethods.GetAsync(databaseContext, chain, infusedSymbol);
        if (infusedToken == null)
        {
            Log.Warning("[{Name}][Blocks] Token {Symbol} not found for infusion on chain {Chain}", Name, infusedSymbol,
                chain.NAME);
            return;
        }

        var infusedValue = CommonsUtils.ToDecimal(infusedValueRaw, infusedToken.DECIMALS);

        if (infusedToken.FUNGIBLE)
        {
            if (!decimal.TryParse(infusedValue, NumberStyles.Any, CultureInfo.InvariantCulture,
                    out var infusedValueDecimal))
            {
                Log.Warning("[{Name}][Blocks] Cannot parse infused value {Value} for token {Symbol}", Name, infusedValue,
                    infusedSymbol);
                return;
            }

            var infusion = databaseContext.Infusions.FirstOrDefault(x =>
                x.Nft == nft && x.KEY == infusedToken.SYMBOL) ??
                           DbHelper.GetTracked<Infusion>(databaseContext)
                               .FirstOrDefault(x => x.Nft == nft && x.KEY == infusedToken.SYMBOL);

            if (infusion == null)
            {
                infusion = new Infusion { Nft = nft, KEY = infusedToken.SYMBOL, Token = infusedToken, VALUE = "0" };
                databaseContext.Infusions.Add(infusion);
            }

            if (!decimal.TryParse(infusion.VALUE, NumberStyles.Any, CultureInfo.InvariantCulture,
                    out var currentValue))
            {
                currentValue = 0;
            }

            infusion.VALUE = (currentValue + infusedValueDecimal).ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            var infusion = databaseContext.Infusions.FirstOrDefault(x =>
                x.Nft == nft && x.KEY == infusedToken.SYMBOL && x.VALUE == infusedValueRaw) ??
                           DbHelper.GetTracked<Infusion>(databaseContext)
                               .FirstOrDefault(x =>
                                   x.Nft == nft && x.KEY == infusedToken.SYMBOL && x.VALUE == infusedValueRaw);

            if (infusion == null)
            {
                infusion = new Infusion { Nft = nft, KEY = infusedToken.SYMBOL, VALUE = infusedValueRaw };
                databaseContext.Infusions.Add(infusion);
            }

            var infusedNft = await databaseContext.Nfts.FirstOrDefaultAsync(x => x.TOKEN_ID == infusedValueRaw) ??
                             DbHelper.GetTracked<Nft>(databaseContext)
                                 .FirstOrDefault(x => x.TOKEN_ID == infusedValueRaw);

            if (infusedNft == null)
            {
                Log.Warning("[{Name}][Blocks] NFT {TokenId} infused into {TargetId} not found", Name, infusedValueRaw,
                    nft.TOKEN_ID);
            }
            else
            {
                infusedNft.InfusedInto = nft;
            }
        }
    }

    private readonly record struct TokenCreateFlags(
        bool fungible,
        bool transferable,
        bool finite,
        bool divisible,
        bool fuel,
        bool stakable,
        bool fiat,
        bool swappable,
        bool burnable,
        bool mintable);

    private static (TokenCreateFlags flags, string? tokenName) ParseTokenCreateFlags(TokenCreateData tokenCreateData,
        bool defaultFungible)
    {
        var metadata = tokenCreateData.Metadata ?? new Dictionary<string, string>();
        string? tokenName = null;

        tokenName = metadata.FirstOrDefault(kv =>
                kv.Key.Equals("name", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("token_name", StringComparison.OrdinalIgnoreCase))
            .Value;

        var flagsString = metadata.FirstOrDefault(kv =>
                kv.Key.Equals("flags", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("token_flags", StringComparison.OrdinalIgnoreCase))
            .Value;

        bool HasFlag(string name)
        {
            if (string.IsNullOrEmpty(flagsString)) return false;
            return flagsString.Split(new[] { ',', '|', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(f => f.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        var hasFlags = !string.IsNullOrEmpty(flagsString);

        var fungible = hasFlags ? HasFlag("Fungible") : defaultFungible;
        var transferable = hasFlags ? HasFlag("Transferable") : true;
        // Legacy fallback used a "-1" sentinel, but chain payloads provide numeric maxSupply.
        // Treat token as finite only when maxSupply is strictly positive.
        var finite = hasFlags ? HasFlag("Finite") : CommonsUtils.HasPositiveMaxSupply(tokenCreateData.MaxSupply);
        var divisible = hasFlags ? HasFlag("Divisible") : fungible;
        var fuel = hasFlags && HasFlag("Fuel");
        var stakable = hasFlags && HasFlag("Stakable");
        var fiat = hasFlags && HasFlag("Fiat");
        var swappable = hasFlags && HasFlag("Swappable");
        var burnable = hasFlags && HasFlag("Burnable");
        var mintable = hasFlags ? HasFlag("Mintable") : true;

        return (new TokenCreateFlags(fungible, transferable, finite, divisible, fuel, stakable, fiat, swappable,
            burnable, mintable), tokenName);
    }

    private static string? ParseSeriesMode(Dictionary<string, string>? metadata)
    {
        if (metadata == null)
            return null;

        if (metadata.TryGetValue("mode", out var modeValue))
        {
            if (int.TryParse(modeValue, out var parsedMode))
                return parsedMode == 0 ? "Unique" : "Duplicated";

            if (!string.IsNullOrWhiteSpace(modeValue))
                return modeValue;
        }

        if (metadata.TryGetValue("seriesMode", out var seriesMode) && !string.IsNullOrWhiteSpace(seriesMode))
            return seriesMode;

        return null;
    }

    private async Task FetchBlocksRange(string chainName, BigInteger fromHeight, BigInteger toHeight,
        bool allowBalanceSync)
    {
        await using (MainDbContext databaseContext = new())
        {
            _balanceRefetchDate = await GlobalVariableMethods.GetLongAsync(databaseContext, _balanceRefetchTimestampKey);
            if (_balanceRefetchDate == 0)
            {
                Log.Information("[{Name}][Blocks] Marking all addresses for balance refresh", Name);
                var chainEntry = await ChainMethods.GetAsync(databaseContext, chainName);
                await MarkAllBalancesDirtyAsync(databaseContext, chainEntry.ID);
                await GlobalVariableMethods.UpsertAsync(databaseContext, _balanceRefetchTimestampKey, UnixSeconds.Now(),
                    false);
                await databaseContext.SaveChangesAsync();
                Log.Information("[{Name}][Blocks] Finished marking addresses for balance refresh", Name);
            }
        }

        Log.Information("[{Name}][Blocks] Fetching blocks ({From}-{To}) for chain {Chain}...",
                Name,
                fromHeight,
                toHeight,
                chainName);

        BigInteger i;
        await using (MainDbContext databaseContext = new())
        {
            i = ChainMethods.GetLastProcessedBlock(databaseContext, chainName) + 1;
        }

        if (i < fromHeight) i = fromHeight;

        var processedAny = false;
        while (i <= toHeight)
        {
            var fetchPerIteration = BigInteger.Min(FetchBlocksPerIterationMax, toHeight - i + 1);
            Log.Information("[{Name}][Blocks] Fetching batch of {Count} blocks starting from {StartHeight} for chain {Chain}...",
                Name,
                fetchPerIteration,
                i,
                chainName);

            processedAny = true;
            var startTime = DateTime.Now;
            var blocks = await GetBlockRange(chainName, i, fetchPerIteration);
            var fetchTime = DateTime.Now - startTime;

            startTime = DateTime.Now;
            foreach (var block in blocks)
            {
                // Log.Information("PROCESSING HEIGHT " + blockHeight);
                await ProcessBlock(block, chainName);
            }
            var processTime = DateTime.Now - startTime;

            Log.Information("[{Name}][Blocks] {Count} blocks loaded ({From}-{To}) in {FetchTime} sec, processed in {ProcessTime} sec, {BlocksPerSecond} bps",
                Name,
                blocks.Count,
                i,
                i + blocks.Count - 1,
                Math.Round(fetchTime.TotalSeconds, 3),
                Math.Round(processTime.TotalSeconds, 3),
                Math.Round(blocks.Count / (fetchTime.TotalSeconds + processTime.TotalSeconds), 2));

            if (allowBalanceSync)
            {
                RequestBalanceSync(chainName);
            }

            i += fetchPerIteration;
        }

        if (allowBalanceSync && !processedAny)
        {
            RequestBalanceSync(chainName);
        }
    }

    private static double _kilobyte = 1024.0;
    private static double _megabyte = _kilobyte * 1024.0;
    private static string BytesToSizeString(long byteCount)
    {
        double mb = byteCount / _megabyte;
        if (mb >= 0.01)
        {
            return $"{mb:F2} MB";
        }

        double kb = byteCount / _kilobyte;
        return $"{kb:F2} kb";
    }

    private async Task<RpcBlockResult> GetBlockAsync(string chainName, BigInteger blockHeight)
    {
        var url = $"{Settings.Default.GetRest()}/api/v1/getBlockByHeight?chainInput={chainName}&height={blockHeight}";

        var startTime = DateTime.Now;
        var (response, blockLength) = await Client.ApiRequestAsync<RpcBlockResult>(url, 10);
        if (response == default)
        {
            throw new Exception($"getBlockByHeight call failed: {url}");
        }

        if (response.Height != (uint)blockHeight)
        {
            throw new($"Error: Query {url} returned block {response.Height} / {response.Hash}");
        }

        var requestTime = (DateTime.Now - startTime).TotalMilliseconds;
        if (blockLength > _megabyte)
        {
            Log.Warning("[{Name}][Blocks] Large block #{index} | {size} received in {time} ms", Name, blockHeight, BytesToSizeString(blockLength), Math.Round(requestTime, 2));
        }
        else if (requestTime > 200) // TODO Set through config
        {
            Log.Information("[{Name}][Blocks] Block #{index} | {size} received in {time} ms", Name, blockHeight, BytesToSizeString(blockLength), Math.Round(requestTime, 2));
        }

        return response;
    }


    private async Task<List<RpcBlockResult>> GetBlockRange(string chainName, BigInteger fromHeight, BigInteger blockCount)
    {
        // Log.Information("FETCHING RANGE " + fromHeight + " - " + (fromHeight + blockCount - 1));
        var tasks = new List<Task<RpcBlockResult>>();
        var taskGroup = new List<Task<RpcBlockResult>>();
        for (var i = fromHeight; i < fromHeight + blockCount; i++)
        {
            var task = GetBlockAsync(chainName, i);
            tasks.Add(task);
            taskGroup.Add(task);

            if (taskGroup.Count == 50)
            {
                await Task.WhenAll(taskGroup.ToArray());

                foreach (var t in taskGroup)
                {
                    if (t.IsFaulted)
                    {
                        throw new($"Task failed: {t.Exception?.Flatten().Message}");
                    }
                    if (t.Result == default)
                    {
                        throw new($"Task failed, no result");
                    }
                }

                taskGroup.Clear();
                await Task.Delay(100);
            }
        }

        if (taskGroup.Count > 0)
        {
            await Task.WhenAll(taskGroup.ToArray());

            foreach (var t in taskGroup)
            {
                if (t.IsFaulted)
                {
                    throw new($"Task failed: {t.Exception?.Flatten().Message}");
                }
                if (t.Result == default)
                {
                    throw new($"Task failed, no result");
                }
            }
        }

        await Task.WhenAll(tasks.ToArray());

        foreach (var t in tasks)
        {
            if (t.IsFaulted)
            {
                throw new($"Task failed: {t.Exception?.Flatten().Message}");
            }
            if (t.Result == default)
            {
                throw new($"Task failed, no result");
            }
        }

        return tasks.Select(task => task.Result).OrderBy(b => b.Height).ToList();
    }

    private void AddAddress(ref List<string> addresses, string address)
    {
        var normalizedAddress = NormalizeAddress(address);

        // Skip placeholder/null-like addresses to avoid invalid storage and balance queries.
        if (normalizedAddress.Equals("NULL", StringComparison.OrdinalIgnoreCase))
            return;

        addresses.Add(normalizedAddress);
    }

    private async Task ProcessBlock(RpcBlockResult block, string chainName)
    {
        var startTime = DateTime.Now;

        var eventsAddedCount = 0;

        // We cache NFTs in this block to speed up code
        // and avoid some unpleasant situations leading to bugs.
        Dictionary<string, Nft> nftsInThisBlock = new();
        Dictionary<string, bool> symbolsFungibility = new();

        // TODO Hack until explorer can process events properly
        List<string> addressesToUpdate = new();

        await using MainDbContext databaseContext = new();

        var connectionString = MainDbContext.GetConnectionString();
        using var dbConnection = new NpgsqlConnection(connectionString);
        dbConnection.Open();

        AddAddress(ref addressesToUpdate, block.ChainAddress);
        AddAddress(ref addressesToUpdate, block.ValidatorAddress);

        var chainEntry = await ChainMethods.GetAsync(databaseContext, chainName);
        var chainId = chainEntry.ID;

        // Block in main database
        Log.Information("[{Name}][Blocks] Storing block #{BlockHeight} / {Hash}", Name, block.Height, block.Hash);

        var blockEntity = await Database.Main.BlockMethods.UpsertAsync(databaseContext, chainEntry, block.Height,
            block.Timestamp, block.Hash, block.PreviousHash, block.Protocol, block.ChainAddress, block.ValidatorAddress, block.Reward);

        if (block.Oracles?.Length > 0)
        {
            var oracles = block.Oracles.Select(oracle =>
                new Tuple<string, string>(oracle.Url,
                    oracle.Content)).ToList();
            BlockOracleMethods.InsertIfNotExists(databaseContext, oracles, blockEntity);
        }

        if (block.Txs?.Length > 0)
        {
            Log.Verbose("[{Name}][Blocks] Block #{BlockHeight} Found {Count} txes in the block", Name, block.Height,
                block.Txs.Length);

            block.ParseData();
            var contractHashes = block.GetContracts();
            // Log.Verbose("[{Name}][Blocks] Contracts in block: " + string.Join(",", contractHashes));

            var contracts = ContractMethods.BatchUpsert(dbConnection,
                contractHashes.Select(c => (c, chainId, c, c)).ToList());

            for (var txIndex = 0; txIndex < block.Txs.Length; txIndex++)
            {
                var tx = block.Txs[txIndex];
                AddAddress(ref addressesToUpdate, tx.Sender);
                AddAddress(ref addressesToUpdate, tx.GasPayer);
                AddAddress(ref addressesToUpdate, tx.GasTarget);

                var transaction = await TransactionMethods.UpsertAsync(databaseContext, blockEntity, txIndex,
                    tx.Hash, tx.Timestamp,
                    tx.Payload, tx.Script, tx.Result,
                    tx.Fee, tx.Expiration,
                    tx.GasPrice, tx.GasLimit,
                    tx.State.ToString(), tx.Sender,
                    tx.GasPayer, tx.GasTarget,
                    tx.CarbonTxType, tx.CarbonTxData);

                TokenSchemas? carbonSchemasForTx = null;
                SeriesInfo? carbonSeriesInfo = null;
                TxMsgMintNonFungible? carbonMintTx = null;
                byte[] carbonTokenSchemasRaw = Array.Empty<byte>();

                if (!string.IsNullOrWhiteSpace(tx.CarbonTxData))
                {
                    var carbonContext = $"type:{tx.CarbonTxType}";

                    try
                    {
                        var carbonBytes = Base16.Decode(tx.CarbonTxData);
                        if (carbonBytes == null)
                        {
                            Log.Warning("[{Name}][Metadata] Failed to decode carbon tx data for {TxHash}: invalid hex payload",
                                Name, tx.Hash);
                            continue;
                        }

                        var carbonType = (TxTypes)tx.CarbonTxType;
                        carbonContext = carbonType.ToString();

                        switch (carbonType)
                        {
                            case TxTypes.Call:
                                {
                                    var call = CarbonBlob.New<TxMsgCall>(carbonBytes);
                                    carbonContext = $"Call module:{call.moduleId} method:{call.methodId}";
                                    if (call.moduleId == (uint)ModuleId.Token)
                                    {
                                        if (call.methodId == (uint)TokenContract_Methods.CreateToken)
                                        {
                                            var tokenInfo = CarbonBlob.New<TokenInfo>(call.args);
                                            if (tokenInfo.tokenSchemas == null || tokenInfo.tokenSchemas.Length == 0)
                                            {
                                                Log.Warning(
                                                    "[{Name}][Metadata] Carbon token create without schemas (tx {TxHash}, symbol {Symbol}, chain {Chain})",
                                                    Name, tx.Hash, tokenInfo.symbol.data, chainEntry.NAME);
                                            }
                                            else if (TryParseCarbonTokenSchemas(tokenInfo.tokenSchemas, tokenInfo.symbol.data,
                                                         chainEntry.NAME, out var parsedSchemas))
                                            {
                                                carbonTokenSchemasRaw = tokenInfo.tokenSchemas;
                                                carbonSchemasForTx = parsedSchemas;
                                                _carbonTokenSchemasCache.TryAdd(
                                                    BuildTokenCacheKey(chainId, tokenInfo.symbol.data), parsedSchemas);
                                            }
                                            else
                                            {
                                                carbonTokenSchemasRaw = tokenInfo.tokenSchemas;
                                            }
                                        }
                                        else if (call.methodId == (uint)TokenContract_Methods.CreateTokenSeries)
                                        {
                                            using var argsStream = new MemoryStream(call.args);
                                            using var reader = new BinaryReader(argsStream);
                                            reader.Read8(out ulong _);
                                            carbonSeriesInfo = CarbonBlob.New<SeriesInfo>(reader);
                                        }
                                    }

                                    break;
                                }
                            case TxTypes.MintNonFungible:
                                {
                                    carbonMintTx = CarbonBlob.New<TxMsgMintNonFungible>(carbonBytes);
                                    break;
                                }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Warning("[{Name}][Metadata] Failed to decode carbon tx data for {TxHash} ({Context}): {Message}",
                            Name, tx.Hash, carbonContext, e.Message);
                    }
                }

                if (tx.Signatures?.Length > 0)
                {
                    var signatures = tx.Signatures
                        .Select(signature => new Tuple<string, string>(signature.Kind, signature.Data))
                        .ToList();

                    SignatureMethods.InsertIfNotExists(databaseContext, signatures, transaction);
                }

                var eventNodes = tx.Events?.ToList() ?? new List<EventResult>();

                // Synthesize TokenSeriesCreate event from extended data (RPC does not emit legacy event).
                var seriesCreateDataTx = ExtendedEventParser.GetTokenSeriesCreateData(tx.ExtendedEvents);
                var tokenMintDataTx = ExtendedEventParser.GetTokenMintData(tx.ExtendedEvents);
                SpecialResolutionData? specialResolutionDataTx = null;

                if (tx.ExtendedEvents is { Length: > 0 })
                    specialResolutionDataTx = ExtendedEventParser.GetSpecialResolutionData(tx.ExtendedEvents);

                if (specialResolutionDataTx != null &&
                     !eventNodes.Any(e =>
                         string.Equals(e.Kind, EventKind.SpecialResolution.ToString(), StringComparison.OrdinalIgnoreCase)))
                {
                    var specialResolutionData = specialResolutionDataTx.Value;
                    eventNodes.Add(new EventResult
                    {
                        Address = tx.GasPayer,
                        Contract = "governance",
                        Kind = EventKind.SpecialResolution.ToString(),
                        Data = Convert.ToHexString(BitConverter.GetBytes(specialResolutionData.ResolutionId))
                    });

                    var governanceKey = new ContractMethods.ChainHashKey(chainId, "governance");
                    if (!contracts.ContainsKey(governanceKey))
                    {
                        var contract = await ContractMethods.UpsertAsync(databaseContext, "governance", chainEntry,
                            "governance", "governance");
                        contracts[governanceKey] = contract.ID;
                    }
                }

                if (seriesCreateDataTx != null)
                {
                    if (string.IsNullOrWhiteSpace(seriesCreateDataTx.Value.Symbol))
                    {
                        Log.Error("[{Name}][Blocks] TokenSeriesCreate extended event missing symbol (tx {TxHash}, block {BlockHeight})",
                            Name, tx.Hash, block.Height);
                        throw new Exception("TokenSeriesCreate extended event missing symbol");
                    }

                    eventNodes.Add(new EventResult
                    {
                        Address = seriesCreateDataTx.Value.Owner,
                        Contract = seriesCreateDataTx.Value.Symbol,
                        Kind = EventKind.TokenSeriesCreate.ToString(),
                        Data = ""
                    });

                    var contractKey = new ContractMethods.ChainHashKey(chainId, seriesCreateDataTx.Value.Symbol);
                    if (!contracts.ContainsKey(contractKey))
                    {
                        var contract = await ContractMethods.UpsertAsync(databaseContext, seriesCreateDataTx.Value.Symbol,
                            chainEntry, seriesCreateDataTx.Value.Symbol, seriesCreateDataTx.Value.Symbol);
                        contracts[contractKey] = contract.ID;
                    }
                }

                if (eventNodes.Count == 0)
                    continue;

                for (var eventIndex = 0; eventIndex < eventNodes.Count; eventIndex++)
                {
                    var eventNode = eventNodes[eventIndex];
                    Database.Main.Event? eventEntry = null;
                    Dictionary<string, object?>? payload = null;

                    try
                    {
                        var parsedKind = eventNode.GetParsedKind();
                        if (parsedKind == null)
                        {
                            Log.Error($"Unsupported event kind {eventNode.Kind}");
                            continue;
                        }

                        var kind = parsedKind.Value;

                        var eventKindId = _eventKinds.GetId(chainId, kind);

                        var contract = eventNode.Contract;
                        var addressString = NormalizeAddress(eventNode.Address);
                        AddAddress(ref addressesToUpdate, addressString);
                        payload = InitPayload(kind.ToString(), chainEntry.NAME, contract, addressString);

                        //create here the event, and below update the data if needed
                        var contractId = contracts.GetId(chainId, contract);

                        var addressEntry = await AddressMethods.UpsertAsync(databaseContext, chainEntry, addressString);

                        eventEntry = EventMethods.Upsert(databaseContext, out var eventAdded,
                            block.Timestamp, eventIndex + 1, chainEntry, transaction, contractId,
                            eventKindId, addressEntry);

                        if (eventAdded) eventsAddedCount++;

                        switch (kind)
                        {
                            case EventKind.Infusion:
                                {
                                    var infusionEventData = eventNode.GetParsedData<InfusionEventData>();

                                    bool fungible;
                                    if (symbolsFungibility.ContainsKey(infusionEventData.BaseSymbol))
                                        fungible = symbolsFungibility.GetValueOrDefault(infusionEventData.BaseSymbol);
                                    else
                                    {
                                        var baseToken = await TokenMethods.GetAsync(databaseContext, chainEntry,
                                            infusionEventData.BaseSymbol);
                                        if (baseToken == null)
                                            throw new Exception($"Token {infusionEventData.BaseSymbol} not found on chain {chainEntry.NAME}");

                                        fungible = baseToken.FUNGIBLE;

                                        symbolsFungibility.Add(infusionEventData.BaseSymbol, fungible);
                                    }

                                    // TODO(legacy): Remove NormalizeTokenId once we stop seeing negative TOKEN_ID values
                                    // from decoded events and all legacy DB rows are normalized.
                                    var tokenId = NormalizeTokenId(infusionEventData.TokenID);
                                    var infusedValueRaw = infusionEventData.InfusedValue.ToString();

                                    Nft? nft = null;
                                    if (!fungible)
                                    {
                                        // Searching for corresponding NFT.
                                        // If it's available, we will set up relation.
                                        // If not, we will create it first.
                                        if (nftsInThisBlock.TryGetValue(tokenId, out var cachedNft))
                                            nft = cachedNft;
                                        else
                                        {
                                            (nft, var newNftCreated) = await NftMethods.UpsertAsync(databaseContext, chainEntry,
                                                tokenId, null, contracts.GetId(chainId, infusionEventData.BaseSymbol));

                                            if (newNftCreated) nftsInThisBlock.Add(tokenId, nft);
                                        }
                                    }


                                    //parse also a new contract, just in case
                                    var eventUpdated = await EventMethods.UpdateValuesAsync(databaseContext,
                                        eventEntry, nft, tokenId, chainEntry, kind, eventKindId, contracts.GetId(chainId, infusionEventData.BaseSymbol));

                                    await ApplyInfusionAsync(databaseContext, chainEntry, nft, infusionEventData.InfusedSymbol,
                                        infusedValueRaw);
                                    payload["token_id"] = tokenId;
                                    payload["infusion_event"] = new Dictionary<string, object?>
                                    {
                                        ["token_id"] = tokenId,
                                        ["base_token"] = infusionEventData.BaseSymbol,
                                        ["infused_token"] = infusionEventData.InfusedSymbol,
                                        ["infused_value"] = infusedValueRaw
                                    };
                                    break;
                                }
                            case EventKind.TokenMint or EventKind.TokenClaim or EventKind.TokenBurn
                                or EventKind.TokenSend or EventKind.TokenReceive or EventKind.TokenStake
                                or EventKind.CrownRewards or EventKind.Inflation:
                                {
                                    var tokenEventData = eventNode.GetParsedData<TokenEventData>();

                                    bool fungible;
                                    if (symbolsFungibility.ContainsKey(tokenEventData.Symbol))
                                        fungible = symbolsFungibility.GetValueOrDefault(tokenEventData.Symbol);
                                    else
                                    {
                                        var token = await TokenMethods.GetAsync(databaseContext, chainEntry, tokenEventData.Symbol);

                                        if (token == null)
                                            throw new Exception($"Token {tokenEventData.Symbol} not found on chain {chainEntry.NAME}");

                                        fungible = token.FUNGIBLE;

                                        symbolsFungibility.Add(tokenEventData.Symbol, fungible);
                                    }

                                    var tokenValueRaw = tokenEventData.Value.ToString();
                                    // TODO(legacy): Remove NormalizeTokenId once we stop seeing negative TOKEN_ID values
                                    // from decoded events and all legacy DB rows are normalized.
                                    var tokenValue = fungible ? tokenValueRaw : NormalizeTokenId(tokenEventData.Value);

                                    Nft? nft = null;
                                    if (!fungible)
                                    {
                                        if (nftsInThisBlock.TryGetValue(tokenValue, out var cachedNft))
                                            nft = cachedNft;
                                        else
                                        {
                                            (nft, var newNftCreated) = await NftMethods.UpsertAsync(databaseContext, chainEntry,
                                                tokenValue, null, contracts.GetId(chainId, tokenEventData.Symbol));

                                            if (newNftCreated) nftsInThisBlock.Add(tokenValue, nft);
                                        }

                                        // We should always properly check mint event and update mint date,
                                        // because nft can be created by auction thread without mint date,
                                        // so we can't update dates only for just created NFTs using newNftCreated flag.
                                        if (kind == EventKind.TokenMint && nft != null)
                                            nft.MINT_DATE_UNIX_SECONDS = block.Timestamp;
                                    }

                                    //parse also a new contract, just in case
                                    var eventUpdated = await EventMethods.UpdateValuesAsync(databaseContext,
                                        eventEntry, nft, tokenValue, chainEntry, kind, eventKindId, contracts.GetId(chainId, tokenEventData.Symbol));

                                    if (kind == EventKind.TokenMint && !fungible && nft != null &&
                                         tokenMintDataTx.HasValue)
                                    {
                                        var mintData = tokenMintDataTx.Value;
                                        if (string.Equals(mintData.Symbol, tokenEventData.Symbol, StringComparison.OrdinalIgnoreCase) &&
                                             string.Equals(mintData.TokenId, tokenValue, StringComparison.OrdinalIgnoreCase))
                                        {
                                            if (mintData.MintNumber > 0)
                                            {
                                                var mintNumber = mintData.MintNumber > int.MaxValue
                                                    ? int.MaxValue
                                                    : (int)mintData.MintNumber;
                                                nft.MINT_NUMBER = mintNumber;
                                                nft.DM_UNIX_SECONDS = UnixSeconds.Now();
                                            }

                                            if (!string.IsNullOrWhiteSpace(mintData.SeriesId))
                                            {
                                                var seriesEntry = SeriesMethods.Upsert(databaseContext,
                                                    contracts.GetId(chainId, tokenEventData.Symbol), mintData.SeriesId,
                                                    seriesCreatedUnixSeconds: block.Timestamp);
                                                nft.Series = seriesEntry;
                                                nft.SeriesId = seriesEntry.ID;
                                            }

                                            payload["token_mint_extended"] = new Dictionary<string, object?>
                                            {
                                                ["token_id"] = mintData.TokenId,
                                                ["series_id"] = mintData.SeriesId,
                                                ["mint_number"] = mintData.MintNumber.ToString(),
                                                ["carbon_token_id"] = mintData.CarbonTokenId.ToString(),
                                                ["carbon_series_id"] = mintData.CarbonSeriesId.ToString(),
                                                ["carbon_instance_id"] = mintData.CarbonInstanceId.ToString(),
                                                ["owner"] = mintData.Owner
                                            };
                                        }
                                    }

                                    //update ntf related things if it is not null
                                    if (nft != null)
                                        // Update NFTs owner address on new event.
                                        NftMethods.ProcessOwnershipChange(databaseContext, chainEntry, nft,
                                            block.Timestamp, addressEntry, false);

                                    payload["token_id"] = tokenValue;
                                    payload["token_event"] = new Dictionary<string, object?>
                                    {
                                        ["token"] = tokenEventData.Symbol,
                                        ["value"] = tokenValue,
                                        ["value_raw"] = tokenValueRaw,
                                        ["chain_name"] = tokenEventData.ChainName
                                    };

                                    if (kind == EventKind.TokenMint && carbonMintTx.HasValue && nft != null)
                                    {
                                        if (carbonSchemasForTx.HasValue)
                                            _carbonTokenSchemasCache.TryAdd(
                                                BuildTokenCacheKey(chainId, tokenEventData.Symbol), carbonSchemasForTx.Value);

                                        var tokenSchemas =
                                            carbonSchemasForTx ??
                                            await GetCarbonTokenSchemasAsync(databaseContext, chainEntry,
                                                tokenEventData.Symbol);
                                        if (tokenSchemas.HasValue)
                                        {
                                            ProcessCarbonMint(nft, tokenEventData.Symbol, chainId, carbonMintTx.Value,
                                                tokenSchemas.Value);
                                        }
                                    }

                                    if (nft != null && (kind == EventKind.TokenMint || kind == EventKind.TokenBurn))
                                        ApplySeriesSupplyFromNftLifecycle(databaseContext, nft, kind);

                                    break;
                                }
                            case EventKind.TokenSeriesCreate:
                                {
                                    if (seriesCreateDataTx == null)
                                    {
                                        Log.Warning("[{Name}][Blocks] TokenSeriesCreate without extended data, skipping", Name);
                                        break;
                                    }

                                    var symbol = seriesCreateDataTx.Value.Symbol;

                                    if (string.IsNullOrWhiteSpace(symbol))
                                    {
                                        Log.Warning("[{Name}][Blocks] TokenSeriesCreate payload missing symbol, skipping", Name);
                                        break;
                                    }

                                    string seriesId = seriesCreateDataTx.Value.SeriesId;

                                    if (string.IsNullOrWhiteSpace(seriesId)
                                         && seriesCreateDataTx.Value.Metadata != null
                                         && seriesCreateDataTx.Value.Metadata.TryGetValue("seriesId", out var metaSeriesId)
                                         && !string.IsNullOrWhiteSpace(metaSeriesId))
                                    {
                                        seriesId = metaSeriesId;
                                    }

                                    if (string.IsNullOrWhiteSpace(seriesId) && seriesCreateDataTx.Value.CarbonSeriesId > 0)
                                        seriesId = seriesCreateDataTx.Value.CarbonSeriesId.ToString();

                                    // use pre-created contractId for this event
                                    var contractIdForSeries = contracts.GetId(chainId, symbol);

                                    await EventMethods.UpdateValuesAsync(databaseContext,
                                        eventEntry, null, seriesId, chainEntry, kind, eventKindId, contractIdForSeries);

                                    if (!string.IsNullOrWhiteSpace(seriesId))
                                    {
                                        var modeName = ParseSeriesMode(seriesCreateDataTx.Value.Metadata);
                                        var maxSupply = seriesCreateDataTx.Value.MaxSupply;
                                        var maxSupplyInt = maxSupply > int.MaxValue ? int.MaxValue : (int)maxSupply;

                                        var seriesEntry = SeriesMethods.Upsert(databaseContext, contractIdForSeries, seriesId,
                                            addressEntry?.ID, null, maxSupplyInt, modeName,
                                            seriesCreatedUnixSeconds: block.Timestamp);

                                        var carbonSeriesId = seriesCreateDataTx.Value.CarbonSeriesId > 0
                                            ? (uint)seriesCreateDataTx.Value.CarbonSeriesId
                                            : (uint?)null;

                                        if (string.IsNullOrWhiteSpace(seriesEntry.NAME) && carbonSeriesId.HasValue)
                                            seriesEntry.NAME =
                                                $"Series #{carbonSeriesId.Value} for {seriesCreateDataTx.Value.Symbol}";

                                        if (carbonSeriesId.HasValue)
                                        {
                                            if (carbonSchemasForTx.HasValue)
                                                _carbonTokenSchemasCache.TryAdd(
                                                    BuildTokenCacheKey(chainId, seriesCreateDataTx.Value.Symbol),
                                                    carbonSchemasForTx.Value);

                                            var tokenSchemas =
                                                carbonSchemasForTx ??
                                                await GetCarbonTokenSchemasAsync(databaseContext, chainEntry,
                                                    seriesCreateDataTx.Value.Symbol);

                                            if (tokenSchemas.HasValue && carbonSeriesInfo.HasValue)
                                            {
                                                ProcessCarbonSeriesMetadata(databaseContext, seriesEntry, chainId,
                                                    seriesCreateDataTx.Value.Symbol, carbonSeriesId.Value, tokenSchemas.Value,
                                                    carbonSeriesInfo.Value.metadata ?? Array.Empty<byte>());
                                            }
                                        }

                                        if (seriesCreateDataTx.Value.Metadata != null)
                                            UpdateSeriesMetadata(seriesEntry,
                                                ConvertDictionaryToMetadata(seriesCreateDataTx.Value.Metadata));
                                    }

                                    payload["token_id"] = seriesId ?? seriesCreateDataTx.Value.CarbonSeriesId.ToString();
                                    payload["token_series_event"] = new Dictionary<string, object?>
                                    {
                                        ["token"] = symbol,
                                        ["series_id"] = seriesId ?? seriesCreateDataTx.Value.CarbonSeriesId.ToString(),
                                        ["max_mint"] = seriesCreateDataTx.Value.MaxMint.ToString(),
                                        ["max_supply"] = seriesCreateDataTx.Value.MaxSupply.ToString(),
                                        ["owner"] = seriesCreateDataTx.Value.Owner ?? addressString,
                                        ["carbon_token_id"] = seriesCreateDataTx.Value.CarbonTokenId.ToString(),
                                        ["carbon_series_id"] = seriesCreateDataTx.Value.CarbonSeriesId.ToString(),
                                        ["metadata"] = seriesCreateDataTx.Value.Metadata
                                    };

                                    break;
                                }
                            case EventKind.OrderCancelled or EventKind.OrderClosed or EventKind.OrderCreated
                                or EventKind.OrderFilled or EventKind.OrderBid:
                                {
                                    var marketEventData = eventNode.GetParsedData<MarketEventData>();

                                    bool fungible;
                                    if (symbolsFungibility.ContainsKey(marketEventData.BaseSymbol))
                                        fungible = symbolsFungibility.GetValueOrDefault(marketEventData.BaseSymbol);
                                    else
                                    {
                                        fungible = (await TokenMethods.GetAsync(databaseContext, chainEntry,
                                            marketEventData.BaseSymbol)).FUNGIBLE;
                                        symbolsFungibility.Add(marketEventData.BaseSymbol, fungible);
                                    }

                                    var tokenId = marketEventData.ID.ToString();

                                    Nft? nft = null;
                                    if (!fungible)
                                    {
                                        var ntfStartTime = DateTime.Now;

                                        // Searching for corresponding NFT.
                                        // If it's available, we will set up relation.
                                        // If not, we will create it first.
                                        if (nftsInThisBlock.TryGetValue(tokenId, out var cachedNft))
                                            nft = cachedNft;
                                        else
                                        {
                                            (nft, var newNftCreated) = await NftMethods.UpsertAsync(databaseContext, chainEntry,
                                                tokenId, null, contracts.GetId(chainId, marketEventData.BaseSymbol));

                                            if (newNftCreated) nftsInThisBlock.Add(tokenId, nft);
                                        }

                                        if (kind == EventKind.OrderFilled)
                                            // Update NFTs owner address on new sale event.
                                            NftMethods.ProcessOwnershipChange(databaseContext, chainEntry, nft,
                                                block.Timestamp, addressEntry, false);
                                    }

                                    //parse also a new contract, just in case
                                    var eventUpdated = await EventMethods.UpdateValuesAsync(databaseContext,
                                        eventEntry, nft, tokenId, chainEntry, kind, eventKindId, contracts.GetId(chainId, marketEventData.BaseSymbol));

                                    payload["token_id"] = tokenId;
                                    payload["market_event"] = new Dictionary<string, object?>
                                    {
                                        ["base_token"] = marketEventData.BaseSymbol,
                                        ["quote_token"] = marketEventData.QuoteSymbol,
                                        ["market_event_kind"] = marketEventData.Type.ToString(),
                                        ["market_id"] = marketEventData.ID.ToString(),
                                        ["price"] = marketEventData.Price.ToString(),
                                        ["end_price"] = marketEventData.EndPrice.ToString()
                                    };

                                    break;
                                }
                            case EventKind.ChainCreate or EventKind.TokenCreate or EventKind.ContractUpgrade
                                or EventKind.AddressRegister or EventKind.ContractDeploy or EventKind.PlatformCreate
                                or EventKind.OrganizationCreate or EventKind.Log or EventKind.AddressUnregister:
                                {
                                    switch (kind)
                                    {
                                        case EventKind.TokenCreate:
                                            {
                                                var tokenEventData = eventNode.GetParsedData<TokenEventData>();
                                                var tokenCreateDataNullable =
                                                    ExtendedEventParser.GetTokenCreateData(tx.ExtendedEvents);

                                                var symbol = !string.IsNullOrWhiteSpace(tokenEventData.Symbol)
                                                    ? tokenEventData.Symbol
                                                    : tokenCreateDataNullable?.Symbol;

                                                if (string.IsNullOrWhiteSpace(symbol))
                                                {
                                                    Log.Warning("[{Name}][Blocks] TokenCreate payload missing symbol, skipping", Name);
                                                    break;
                                                }

                                                if (tokenCreateDataNullable != null)
                                                {
                                                    var tokenCreateData = tokenCreateDataNullable.Value;
                                                    var (flags, tokenNameFromMetadata) =
                                                        ParseTokenCreateFlags(tokenCreateData, !tokenCreateData.IsNonFungible);

                                                    var tokenName = tokenNameFromMetadata ?? symbol;

                                                    var maxSupply = tokenCreateData.MaxSupply.ToString(CultureInfo.InvariantCulture);
                                                    var decimalsString = tokenCreateData.Decimals.ToString(CultureInfo.InvariantCulture);
                                                    var carbonTokenId = tokenCreateData.CarbonTokenId.ToString(CultureInfo.InvariantCulture);

                                                    var token = await TokenMethods.UpsertAsync(databaseContext, chainEntry,
                                                        contract, tokenName, symbol, (int)tokenCreateData.Decimals,
                                                        flags.fungible, flags.transferable, flags.finite, flags.divisible,
                                                        flags.fuel, flags.stakable, flags.fiat, flags.swappable, flags.burnable,
                                                        flags.mintable, addressString, addressString, "0",
                                                        maxSupply, "0", null, carbonTokenSchemasRaw);

                                                    if (token != null && eventEntry != null)
                                                    {
                                                        token.CreateEvent = eventEntry;
                                                    }

                                                    var tokenCreatePayload = new Dictionary<string, object?>
                                                    {
                                                        ["symbol"] = symbol,
                                                        ["max_supply"] = maxSupply,
                                                        ["decimals"] = decimalsString,
                                                        ["is_non_fungible"] = tokenCreateData.IsNonFungible,
                                                        ["carbon_token_id"] = carbonTokenId,
                                                        ["metadata"] = tokenCreateData.Metadata
                                                    };
                                                    // keep legacy key and new consistent key for API mapping
                                                    payload["token_create"] = tokenCreatePayload;
                                                    payload["token_create_event"] = tokenCreatePayload;
                                                }
                                                else if (eventEntry != null)
                                                {
                                                    var token = await TokenMethods.GetAsync(databaseContext, chainEntry, symbol);
                                                    if (token != null)
                                                        token.CreateEvent = eventEntry;
                                                }

                                                break;
                                            }
                                        case EventKind.ContractUpgrade:
                                            {
                                                var stringData = eventNode.GetParsedData<string>();

                                                payload["string_event"] = new Dictionary<string, object?>
                                                {
                                                    ["string_value"] = stringData
                                                };

                                                var queueTuple = new Tuple<string, string, long>(stringData, chainName,
                                                    block.Timestamp);
                                                if (!_methodQueue.Contains(queueTuple))
                                                {
                                                    _methodQueue.Enqueue(queueTuple);
                                                }

                                                break;
                                            }
                                        case EventKind.PlatformCreate:
                                            {
                                                var stringData = eventNode.GetParsedData<string>();

                                                payload["string_event"] = new Dictionary<string, object?>
                                                {
                                                    ["string_value"] = stringData
                                                };

                                                var platform = PlatformMethods.Get(databaseContext, stringData);
                                                if (platform != null)
                                                {
                                                    platform.CreateEvent = eventEntry;
                                                }

                                                break;
                                            }
                                        case EventKind.ContractDeploy:
                                            {
                                                var stringData = eventNode.GetParsedData<string>();

                                                payload["string_event"] = new Dictionary<string, object?>
                                                {
                                                    ["string_value"] = stringData
                                                };

                                                //we might have to create the contract here, better be sure
                                                var contractItem = await ContractMethods.UpsertAsync(databaseContext, stringData,
                                                    chainEntry, stringData,
                                                    null);
                                                //we do it like this, to be sure it is only set here
                                                contractItem.CreateEvent = eventEntry;

                                                break;
                                            }
                                        case EventKind.OrganizationCreate:
                                            {
                                                var stringData = eventNode.GetParsedData<string>();

                                                payload["string_event"] = new Dictionary<string, object?>
                                                {
                                                    ["string_value"] = stringData
                                                };

                                                var organization = OrganizationMethods.Get(databaseContext, stringData);
                                                if (organization != null)
                                                {
                                                    organization.CreateEvent = eventEntry;
                                                }

                                                break;
                                            }
                                        case EventKind.ChainCreate:
                                            {
                                                var stringData = eventNode.GetParsedData<string>();

                                                payload["string_event"] = new Dictionary<string, object?>
                                                {
                                                    ["string_value"] = stringData
                                                };

                                                //fill data
                                                break;
                                            }
                                        default:
                                            {
                                                var stringData = eventNode.GetParsedData<string>();

                                                payload["string_event"] = new Dictionary<string, object?>
                                                {
                                                    ["string_value"] = stringData
                                                };

                                                Log.Verbose("[{Name}][Blocks] Block #{BlockHeight} {UnixSeconds} got something (not supported) {Kind}",
                                                    Name, block.Height, UnixSeconds.Log(block.Timestamp), kind);
                                                break;
                                            }
                                    }
                                    break;
                                }
                            case EventKind.GovernanceSetGasConfig:
                                {
                                    var gasConfig = eventNode.GetParsedData<GasConfig>();
                                    payload["governance_gas_config_event"] = BuildGasConfigPayload(gasConfig);
                                    break;
                                }
                            case EventKind.GovernanceSetChainConfig:
                                {
                                    var chainConfig = eventNode.GetParsedData<ChainConfig>();
                                    payload["governance_chain_config_event"] = BuildChainConfigPayload(chainConfig);
                                    break;
                                }
                            case EventKind.SpecialResolution:
                                {
                                    if (specialResolutionDataTx != null)
                                    {
                                        payload["special_resolution_event"] =
                                            BuildSpecialResolutionPayload(specialResolutionDataTx.Value);
                                    }
                                    else
                                    {
                                        Log.Warning("[{Name}][Blocks] SpecialResolution event without data (tx {TxHash})",
                                            Name, tx.Hash);
                                    }

                                    break;
                                }
                            case EventKind.Crowdsale:
                                {
                                    var saleEventData = eventNode.GetParsedData<SaleEventData>();

                                    var hash = saleEventData.saleHash.ToString();
                                    var saleKind = saleEventData.kind.ToString(); //handle sale kinds 

                                    //databaseEvent we need it here, so check it
                                    payload["sale_event"] = new Dictionary<string, object?>
                                    {
                                        ["hash"] = hash,
                                        ["sale_event_kind"] = saleKind
                                    };

                                    break;
                                }
                            case EventKind.ChainSwap:
                                {
                                    var transactionSettleEventData =
                                        eventNode.GetParsedData<TransactionSettleEventData>();

                                    var hash = transactionSettleEventData.Hash.ToString();
                                    var platform = transactionSettleEventData.Platform;
                                    var chain = transactionSettleEventData.Chain;

                                    payload["transaction_settle_event"] = new Dictionary<string, object?>
                                    {
                                        ["hash"] = hash,
                                        ["platform"] = platform,
                                        ["chain"] = chain
                                    };

                                    break;
                                }
                            case EventKind.ValidatorElect or EventKind.ValidatorPropose:
                                {
                                    var address = eventNode.GetParsedData<Address>().ToString();
                                    AddAddress(ref addressesToUpdate, address);

                                    //databaseEvent we need it here, so check it
                                    if (eventEntry != null)
                                        eventEntry.TargetAddress = await AddressMethods.UpsertAsync(databaseContext, chainEntry, address);
                                    payload["address_event"] = new Dictionary<string, object?>
                                    {
                                        ["address"] = address
                                    };

                                    break;
                                }
                            //TODO
                            case EventKind.ValidatorSwitch:
                                {
                                    Log.Verbose("[{Name}][Blocks] Block #{BlockHeight} getting nothing for {Kind}", Name, block.Height, kind);

                                    break;
                                }
                            case EventKind.ValueCreate or EventKind.ValueUpdate:
                                {
                                    var chainValueEventData = eventNode.GetParsedData<ChainValueEventData>();

                                    var valueEventName = chainValueEventData.Name;
                                    var value = chainValueEventData.Value.ToString();

                                    payload["chain_event"] = new Dictionary<string, object?>
                                    {
                                        ["name"] = valueEventName,
                                        ["value"] = value,
                                        ["chain"] = chainEntry.NAME
                                    };

                                    break;
                                }
                            case EventKind.GasEscrow or EventKind.GasPayment:
                                {
                                    var gasEventData = eventNode.GetParsedData<GasEventData>();

                                    var address = gasEventData.address.ToString();
                                    AddAddress(ref addressesToUpdate, address);
                                    var price = gasEventData.price.ToString();
                                    var amount = gasEventData.amount.ToString();
                                    var hasUnlimitedGas = amount == ulong.MaxValue.ToString();
                                    var amountForPayload = hasUnlimitedGas ? null : amount;

                                    payload["gas_event"] = new Dictionary<string, object?>
                                    {
                                        ["price"] = price,
                                        ["amount"] = amountForPayload,
                                        ["address"] = address
                                    };

                                    break;
                                }
                            case EventKind.FileCreate or EventKind.FileDelete:
                                {
                                    var hash = eventNode.GetParsedData<Hash>().ToString();

                                    payload["hash_event"] = new Dictionary<string, object?>
                                    {
                                        ["hash"] = hash
                                    };

                                    break;
                                }
                            case EventKind.OrganizationAdd or EventKind.OrganizationRemove:
                                {
                                    var organizationEventData = eventNode.GetParsedData<OrganizationEventData>();

                                    var organization = organizationEventData.Organization;
                                    var memberAddress = organizationEventData.MemberAddress.ToString();
                                    AddAddress(ref addressesToUpdate, memberAddress);

                                    payload["organization_event"] = new Dictionary<string, object?>
                                    {
                                        ["organization"] = organization,
                                        ["address"] = memberAddress
                                    };

                                    break;
                                }
                            //TODO
                            case EventKind.LeaderboardCreate or EventKind.Custom:
                                {
                                    Log.Verbose("[{Name}][Blocks] Block #{BlockHeight} Currently not processing EventKind {Kind} in Block #{Block}",
                                        Name, block.Height, kind, block.Height);
                                    break;
                                }
                            default:
                                Log.Warning("[{Name}][Blocks] Block #{BlockHeight} Currently not processing EventKind {Kind} in Block #{Block}",
                                    Name, block.Height, kind, block.Height);
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "[{Name}][Blocks] Block #{BlockHeight} {UnixSeconds} event processing", Name, block.Height,
                            UnixSeconds.Log(block.Timestamp));

                        try
                        {
                            Log.Information("[{Name}][Blocks] eventNode on exception: {@EventNode}", Name, eventNode);
                        }
                        catch (Exception e2)
                        {
                            Log.Information("[{Name}][Blocks] Cannot print eventNode: {Exception}", Name, e2.Message);
                        }

                        try
                        {
                            Log.Information("[{Name}][Blocks] eventNode data on exception: {Exception}", Name,
                                eventNode.Data.Decode());
                        }
                        catch (Exception e2)
                        {
                            Log.Information("[{Name}][Blocks] Cannot print eventNode data: {Exception}", Name,
                                e2.Message);
                        }
                    }
                    finally
                    {
                        FinalizePayload(eventEntry, payload, eventNode.Data);
                    }
                }
            }
        }

        Log.Verbose("[{Name}][Blocks] Block #{BlockHeight} Found {Count} addresses to mark dirty", Name, block.Height,
            addressesToUpdate.Count);

        MarkAddressesDirty(databaseContext, chainEntry, addressesToUpdate, (long)block.Height);
        ChainMethods.SetLastProcessedBlock(databaseContext, chainName, block.Height, false);

        await databaseContext.SaveChangesAsync();

        var processingTime = DateTime.Now - startTime;
        if (processingTime.TotalSeconds > 1) // Log only if processing of the block took > 1 second
        {
            Log.Information(
                "[{Name}][Blocks] Block #{BlockHeight} processed in {ProcessingTime} sec, {EventsAddedCount} events, {NftsInThisBlock} NFTs",
                Name, block.Height, Math.Round(processingTime.TotalSeconds, 3),
                eventsAddedCount, nftsInThisBlock.Count);
        }

    }
}
