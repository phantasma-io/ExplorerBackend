using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Backend.Api;
using Backend.Commons;
using Backend.PluginEngine;
using Database.Main;
using Npgsql;
using PhantasmaPhoenix.Cryptography;
using PhantasmaPhoenix.Protocol;
using PhantasmaPhoenix.Protocol.Carbon.Blockchain;
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
        if ( string.IsNullOrWhiteSpace(address) )
            return "NULL";

        if ( address.Equals("[Null address]", StringComparison.OrdinalIgnoreCase) )
            return "NULL";

        return address;
    }

    private static void FinalizePayload(Database.Main.Event eventEntry, Dictionary<string, object?> payload, string rawData)
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

    private static (TokenCreateFlags flags, string tokenName) ParseTokenCreateFlags(TokenCreateData tokenCreateData,
        bool defaultFungible)
    {
        var metadata = tokenCreateData.Metadata ?? new Dictionary<string, string>();
        string tokenName = null;

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
            if ( string.IsNullOrEmpty(flagsString) ) return false;
            return flagsString.Split(new[] {',', '|', ';', ' '}, StringSplitOptions.RemoveEmptyEntries)
                .Any(f => f.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        var hasFlags = !string.IsNullOrEmpty(flagsString);

        var fungible = hasFlags ? HasFlag("Fungible") : defaultFungible;
        var transferable = hasFlags ? HasFlag("Transferable") : true;
        var finite = hasFlags ? HasFlag("Finite") : tokenCreateData.MaxSupply != "-1";
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

    private static string ParseSeriesMode(Dictionary<string, string> metadata)
    {
        if ( metadata == null )
            return null;

        if ( metadata.TryGetValue("mode", out var modeValue) )
        {
            if ( int.TryParse(modeValue, out var parsedMode) )
                return parsedMode == 0 ? "Unique" : "Duplicated";

            if ( !string.IsNullOrWhiteSpace(modeValue) )
                return modeValue;
        }

        if ( metadata.TryGetValue("seriesMode", out var seriesMode) && !string.IsNullOrWhiteSpace(seriesMode) )
            return seriesMode;

        return null;
    }

    private async Task FetchBlocksRange(string chainName, BigInteger fromHeight, BigInteger toHeight)
    {
        await using ( MainDbContext databaseContext = new() )
        {
            List<string> addressesForInitialUpdate = [];
            _balanceRefetchDate = await GlobalVariableMethods.GetLongAsync(databaseContext, _balanceRefetchTimestampKey);
            if (_balanceRefetchDate == 0)
            {
                // Reprocess balances of ALL known addresses
                // to fix issues
                addressesForInitialUpdate = databaseContext.Addresses.Select(x => x.ADDRESS).Where(x => x.ToUpper() != "NULL").Distinct().ToList();
            }

            if (addressesForInitialUpdate.Count() > 0)
            {
                Log.Information("[{Name}][Blocks] Starting {Count} INITIAL addresses update",
                    Name, addressesForInitialUpdate.Count());

                var chainEntry = await ChainMethods.GetAsync(databaseContext, chainName);
                await UpdateAddressesBalancesAsync(databaseContext, chainEntry, addressesForInitialUpdate,
                    100);

                if (_balanceRefetchDate == 0)
                {
                    // We just finished refetching all balances, saving timestamp
                    // to the database which will tell us when this process was done.
                    await GlobalVariableMethods.UpsertAsync(databaseContext, _balanceRefetchTimestampKey, UnixSeconds.Now());
                }

                await databaseContext.SaveChangesAsync();

                Log.Information("[{Name}][Blocks] Finished INITIAL addresses update", Name);
            }
        }

        Log.Information("[{Name}][Blocks] Fetching blocks ({From}-{To}) for chain {Chain}...",
                Name,
                fromHeight,
                toHeight,
                chainName);

        BigInteger i;
        await using ( MainDbContext databaseContext = new() )
        {
            i = ChainMethods.GetLastProcessedBlock(databaseContext, chainName) + 1;
        }
        
        if ( i < fromHeight ) i = fromHeight;

        while ( i <= toHeight )
        {
            var fetchPerIteration = BigInteger.Min(FetchBlocksPerIterationMax, toHeight - i + 1);
            Log.Information("[{Name}][Blocks] Fetching batch of {Count} blocks starting from {StartHeight} for chain {Chain}...",
                Name,
                fetchPerIteration,
                i,
                chainName);

            var startTime = DateTime.Now;
            var blocks = await GetBlockRange(chainName, i, fetchPerIteration);
            var fetchTime = DateTime.Now - startTime;
            
            List<string> addressesToUpdate = new();
            
            startTime = DateTime.Now;
            foreach ( var block in blocks )
            {
                // Log.Information("PROCESSING HEIGHT " + blockHeight);
                addressesToUpdate.AddRange(await ProcessBlock(block, chainName));
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
            
            // Updating balances for affected addresses
            startTime = DateTime.Now;
            addressesToUpdate = addressesToUpdate.Distinct().ToList();
            await using ( MainDbContext databaseContext = new() )
            {
                Log.Information("[{Name}][Blocks] Starting {Count} addresses update",
                    Name, addressesToUpdate.Count());

                var chainEntry = await ChainMethods.GetAsync(databaseContext, chainName);
                await UpdateAddressesBalancesAsync(databaseContext, chainEntry, addressesToUpdate,
                    100);

                await databaseContext.SaveChangesAsync();
            }
            processTime = DateTime.Now - startTime;
            Log.Information("[{Name}][Blocks] {Count} addresses updated for blocks ({From}-{To}) in {ProcessTime} sec. Sync speed: {AddressesPerSecond} addresses per second",
                Name,
                addressesToUpdate.Count,
                i,
                i + blocks.Count - 1,
                Math.Round(processTime.TotalSeconds, 3),
                Math.Round(addressesToUpdate.Count / processTime.TotalSeconds, 2));

            // Updating SM count and stakers count
            startTime = DateTime.Now;
            await using ( MainDbContext databaseContext = new() )
            {
                var chainEntry = await ChainMethods.GetAsync(databaseContext, chainName);
                OrganizationMethods.UpdateStakeCounts(databaseContext, chainEntry);

                await databaseContext.SaveChangesAsync();
            }
            processTime = DateTime.Now - startTime;
            Log.Information("[{Name}][Blocks] Updated SM and stakers counts in {ProcessTime} sec",
                Name,
                Math.Round(processTime.TotalSeconds, 3));

            i += fetchPerIteration;
        }
    }

    private static double _kilobyte = 1024.0;
    private static double _megabyte = _kilobyte * 1024.0;
    private static string BytesToSizeString(long byteCount)
    {
        double mb = byteCount / _megabyte;
        if(mb >= 0.01)
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

        if(response.Height != (uint)blockHeight)
        {
            throw new($"Error: Query {url} returned block {response.Height} / {response.Hash}");
        }

        var requestTime = (DateTime.Now - startTime).TotalMilliseconds;
        if(blockLength > _megabyte)
        {
            Log.Warning("[{Name}][Blocks] Large block #{index} | {size} received in {time} ms", Name, blockHeight, BytesToSizeString(blockLength), Math.Round(requestTime, 2));
        }
        else if(requestTime > 200) // TODO Set through config
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
        if(string.IsNullOrEmpty(address))
        {
            throw new("Trying to add empty address");
        }

        // Skip placeholder address to avoid invalid balance queries later.
        if ( address.Equals("NULL", StringComparison.OrdinalIgnoreCase) )
            return;

        addresses.Add(address);
    }
    
    private async Task<List<string>> ProcessBlock(RpcBlockResult block, string chainName)
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

                var transaction = await TransactionMethods.UpsertAsync(databaseContext, blockEntity, txIndex,
                    tx.Hash, tx.Timestamp,
                    tx.Payload, tx.Script, tx.Result,
                    tx.Fee, tx.Expiration,
                    tx.GasPrice, tx.GasLimit,
                    tx.State.ToString(), tx.Sender,
                    tx.GasPayer, tx.GasTarget);

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
                if ( seriesCreateDataTx != null )
                {
                    if ( string.IsNullOrWhiteSpace(seriesCreateDataTx.Value.Symbol) )
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
                    if ( !contracts.ContainsKey(contractKey) )
                    {
                        var contract = await ContractMethods.UpsertAsync(databaseContext, seriesCreateDataTx.Value.Symbol,
                            chainEntry, seriesCreateDataTx.Value.Symbol, seriesCreateDataTx.Value.Symbol);
                        contracts[contractKey] = contract.ID;
                    }
                }

                if ( eventNodes.Count == 0 )
                    continue;

                for ( var eventIndex = 0; eventIndex < eventNodes.Count; eventIndex++ )
                {
                    var eventNode = eventNodes[eventIndex];
                    Database.Main.Event eventEntry = null;
                    Dictionary<string, object?> payload = null;

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

                        if ( eventAdded ) eventsAddedCount++;

                        switch ( kind )
                        {
                            case EventKind.Infusion:
                            {
                                var infusionEventData = eventNode.GetParsedData<InfusionEventData>();

                                bool fungible;
                                if ( symbolsFungibility.ContainsKey(infusionEventData.BaseSymbol) )
                                    fungible = symbolsFungibility.GetValueOrDefault(infusionEventData.BaseSymbol);
                                else
                                {
                                    var baseToken = await TokenMethods.GetAsync(databaseContext, chainEntry,
                                        infusionEventData.BaseSymbol);
                                    if ( baseToken == null )
                                        throw new Exception($"Token {infusionEventData.BaseSymbol} not found on chain {chainEntry.NAME}");

                                    fungible = baseToken.FUNGIBLE;

                                    symbolsFungibility.Add(infusionEventData.BaseSymbol, fungible);
                                }

                                var tokenId = infusionEventData.TokenID.ToString();

                                Nft nft = null;
                                if ( !fungible )
                                {
                                    // Searching for corresponding NFT.
                                    // If it's available, we will set up relation.
                                    // If not, we will create it first.
                                    if ( nftsInThisBlock.ContainsKey(tokenId) )
                                        nft = nftsInThisBlock.GetValueOrDefault(tokenId);
                                    else
                                    {
                                        (nft, var newNftCreated) = await NftMethods.UpsertAsync(databaseContext, chainEntry,
                                            tokenId, null, contracts.GetId(chainId, infusionEventData.BaseSymbol));

                                        if ( newNftCreated ) nftsInThisBlock.Add(tokenId, nft);
                                    }
                                }


                                //parse also a new contract, just in case
                                var eventUpdated = await EventMethods.UpdateValuesAsync(databaseContext,
                                    eventEntry, nft, tokenId, chainEntry, kind, eventKindId, contracts.GetId(chainId, infusionEventData.BaseSymbol));

                                await InfusionEventMethods.InsertAsync(databaseContext, infusionEventData.TokenID.ToString(),
                                    infusionEventData.BaseSymbol, infusionEventData.InfusedSymbol,
                                    infusionEventData.InfusedValue.ToString(), chainEntry, eventEntry);
                                payload["token_id"] = tokenId;
                                payload["infusion_event"] = new Dictionary<string, object?>
                                {
                                    ["token_id"] = tokenId,
                                    ["base_token"] = infusionEventData.BaseSymbol,
                                    ["infused_token"] = infusionEventData.InfusedSymbol,
                                    ["infused_value"] = infusionEventData.InfusedValue.ToString()
                                };
                                break;
                            }
                            case EventKind.TokenMint or EventKind.TokenClaim or EventKind.TokenBurn
                                or EventKind.TokenSend or EventKind.TokenReceive or EventKind.TokenStake
                                or EventKind.CrownRewards or EventKind.Inflation:
                            {
                                var tokenEventData = eventNode.GetParsedData<TokenEventData>();

                                bool fungible;
                                if ( symbolsFungibility.ContainsKey(tokenEventData.Symbol) )
                                    fungible = symbolsFungibility.GetValueOrDefault(tokenEventData.Symbol);
                                else
                                {
                                    var token = await TokenMethods.GetAsync(databaseContext, chainEntry, tokenEventData.Symbol);

                                    if ( token == null )
                                        throw new Exception($"Token {tokenEventData.Symbol} not found on chain {chainEntry.NAME}");

                                    fungible = token.FUNGIBLE;

                                    symbolsFungibility.Add(tokenEventData.Symbol, fungible);
                                }

                                var tokenValue = tokenEventData.Value.ToString();

                                Nft nft = null;
                                if ( !fungible )
                                {
                                    if ( nftsInThisBlock.ContainsKey(tokenValue) )
                                        nft = nftsInThisBlock.GetValueOrDefault(tokenValue);
                                    else
                                    {
                                        (nft, var newNftCreated) = await NftMethods.UpsertAsync(databaseContext, chainEntry,
                                            tokenValue, null, contracts.GetId(chainId, tokenEventData.Symbol));

                                        if ( newNftCreated ) nftsInThisBlock.Add(tokenValue, nft);
                                    }

                                    // We should always properly check mint event and update mint date,
                                    // because nft can be created by auction thread without mint date,
                                    // so we can't update dates only for just created NFTs using newNftCreated flag.
                                    if ( kind == EventKind.TokenMint )
                                        nft.MINT_DATE_UNIX_SECONDS = block.Timestamp;
                                }

                                //parse also a new contract, just in case
                                var eventUpdated = await EventMethods.UpdateValuesAsync(databaseContext,
                                    eventEntry, nft, tokenValue, chainEntry, kind, eventKindId, contracts.GetId(chainId, tokenEventData.Symbol));

                                //update ntf related things if it is not null
                                if ( nft != null )
                                    // Update NFTs owner address on new event.
                                    NftMethods.ProcessOwnershipChange(databaseContext, chainEntry, nft,
                                        block.Timestamp, addressEntry, false);

                                await TokenEventMethods.UpsertAsync(databaseContext, tokenEventData.Symbol,
                                    tokenEventData.ChainName, tokenValue, chainEntry, eventEntry);
                                payload["token_id"] = tokenValue;
                                payload["token_event"] = new Dictionary<string, object?>
                                {
                                    ["token"] = tokenEventData.Symbol,
                                    ["value"] = tokenValue,
                                    ["value_raw"] = tokenEventData.Value.ToString(),
                                    ["chain_name"] = tokenEventData.ChainName
                                };

                                break;
                            }
                            case EventKind.TokenSeriesCreate:
                            {
                                if ( seriesCreateDataTx == null )
                                {
                                    Log.Warning("[{Name}][Blocks] TokenSeriesCreate without extended data, skipping", Name);
                                    break;
                                }

                                var symbol = seriesCreateDataTx.Value.Symbol;

                                if ( string.IsNullOrWhiteSpace(symbol) )
                                {
                                    Log.Warning("[{Name}][Blocks] TokenSeriesCreate payload missing symbol, skipping", Name);
                                    break;
                                }

                                string seriesId = seriesCreateDataTx.Value.SeriesId;

                                if ( string.IsNullOrWhiteSpace(seriesId)
                                     && seriesCreateDataTx.Value.Metadata != null
                                     && seriesCreateDataTx.Value.Metadata.TryGetValue("seriesId", out var metaSeriesId)
                                     && !string.IsNullOrWhiteSpace(metaSeriesId) )
                                {
                                    seriesId = metaSeriesId;
                                }

                                if ( string.IsNullOrWhiteSpace(seriesId) && seriesCreateDataTx.Value.CarbonSeriesId > 0 )
                                    seriesId = seriesCreateDataTx.Value.CarbonSeriesId.ToString();

                                // use pre-created contractId for this event
                                var contractIdForSeries = contracts.GetId(chainId, symbol);

                                await EventMethods.UpdateValuesAsync(databaseContext,
                                    eventEntry, null, seriesId, chainEntry, kind, eventKindId, contractIdForSeries);

                                if ( !string.IsNullOrWhiteSpace(seriesId) )
                                {
                                    var modeName = ParseSeriesMode(seriesCreateDataTx.Value.Metadata);
                                    var maxSupply = seriesCreateDataTx.Value.MaxSupply;
                                    var maxSupplyInt = maxSupply > int.MaxValue ? int.MaxValue : ( int ) maxSupply;

                                    SeriesMethods.Upsert(databaseContext, contractIdForSeries, seriesId,
                                        addressEntry?.ID, null, maxSupplyInt, modeName);
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
                                if ( symbolsFungibility.ContainsKey(marketEventData.BaseSymbol) )
                                    fungible = symbolsFungibility.GetValueOrDefault(marketEventData.BaseSymbol);
                                else
                                {
                                    fungible = (await TokenMethods.GetAsync(databaseContext, chainEntry,
                                        marketEventData.BaseSymbol)).FUNGIBLE;
                                    symbolsFungibility.Add(marketEventData.BaseSymbol, fungible);
                                }

                                var tokenId = marketEventData.ID.ToString();

                                Nft nft = null;
                                if ( !fungible )
                                {
                                    var ntfStartTime = DateTime.Now;

                                    // Searching for corresponding NFT.
                                    // If it's available, we will set up relation.
                                    // If not, we will create it first.
                                    if ( nftsInThisBlock.ContainsKey(tokenId) )
                                        nft = nftsInThisBlock.GetValueOrDefault(tokenId);
                                    else
                                    {
                                        (nft, var newNftCreated) = await NftMethods.UpsertAsync(databaseContext, chainEntry,
                                            tokenId, null, contracts.GetId(chainId, marketEventData.BaseSymbol));

                                        if ( newNftCreated ) nftsInThisBlock.Add(tokenId, nft);
                                    }

                                    if ( kind == EventKind.OrderFilled )
                                        // Update NFTs owner address on new sale event.
                                        NftMethods.ProcessOwnershipChange(databaseContext, chainEntry, nft,
                                            block.Timestamp, addressEntry, false);
                                }

                                //parse also a new contract, just in case
                                var eventUpdated = await EventMethods.UpdateValuesAsync(databaseContext,
                                    eventEntry, nft, tokenId, chainEntry, kind, eventKindId, contracts.GetId(chainId, marketEventData.BaseSymbol));

                                await MarketEventMethods.InsertAsync(databaseContext, marketEventData.Type.ToString(),
                                    marketEventData.BaseSymbol, marketEventData.QuoteSymbol,
                                    marketEventData.Price.ToString(), marketEventData.EndPrice.ToString(),
                                    marketEventData.ID.ToString(), chainEntry, eventEntry);
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
                                switch ( kind )
                                {
                                    case EventKind.TokenCreate:
                                    {
                                        var tokenEventData = eventNode.GetParsedData<TokenEventData>();
                                        var tokenCreateDataNullable =
                                            ExtendedEventParser.GetTokenCreateData(tx.ExtendedEvents);

                                        var symbol = !string.IsNullOrWhiteSpace(tokenEventData.Symbol)
                                            ? tokenEventData.Symbol
                                            : tokenCreateDataNullable?.Symbol;

                                        if ( string.IsNullOrWhiteSpace(symbol) )
                                        {
                                            Log.Warning("[{Name}][Blocks] TokenCreate payload missing symbol, skipping", Name);
                                            break;
                                        }

                                        if ( eventEntry != null )
                                            StringEventMethods.Upsert(databaseContext, symbol, eventEntry, false);
                                        payload["string_event"] = new Dictionary<string, object?>
                                        {
                                            ["string_value"] = symbol
                                        };

                                        if ( tokenCreateDataNullable != null )
                                        {
                                            var tokenCreateData = tokenCreateDataNullable.Value;
                                            var (flags, tokenNameFromMetadata) =
                                                ParseTokenCreateFlags(tokenCreateData, !tokenCreateData.IsNonFungible);

                                            var tokenName = tokenNameFromMetadata ?? symbol;

                                            var token = await TokenMethods.UpsertAsync(databaseContext, chainEntry,
                                                contract, tokenName, symbol, ( int ) tokenCreateData.Decimals,
                                                flags.fungible, flags.transferable, flags.finite, flags.divisible,
                                                flags.fuel, flags.stakable, flags.fiat, flags.swappable, flags.burnable,
                                                flags.mintable, addressString, addressString, "0",
                                                tokenCreateData.MaxSupply, "0", null);

                                            if ( token != null && eventEntry != null )
                                            {
                                                token.CreateEvent = eventEntry;
                                            }

                                            var tokenCreatePayload = new Dictionary<string, object?>
                                            {
                                                ["symbol"] = symbol,
                                                ["max_supply"] = tokenCreateData.MaxSupply,
                                                ["decimals"] = tokenCreateData.Decimals,
                                                ["is_non_fungible"] = tokenCreateData.IsNonFungible,
                                                ["carbon_token_id"] = tokenCreateData.CarbonTokenId,
                                                ["metadata"] = tokenCreateData.Metadata
                                            };
                                            // keep legacy key and new consistent key for API mapping
                                            payload["token_create"] = tokenCreatePayload;
                                            payload["token_create_event"] = tokenCreatePayload;
                                        }
                                        else if ( eventEntry != null )
                                        {
                                            var token = await TokenMethods.GetAsync(databaseContext, chainEntry, symbol);
                                            if ( token != null )
                                                token.CreateEvent = eventEntry;
                                        }

                                        break;
                                    }
                                    case EventKind.ContractUpgrade:
                                    {
                                        var stringData = eventNode.GetParsedData<string>();

                                        if ( eventEntry != null )
                                            StringEventMethods.Upsert(databaseContext, stringData, eventEntry, false);
                                        payload["string_event"] = new Dictionary<string, object?>
                                        {
                                            ["string_value"] = stringData
                                        };

                                        var queueTuple = new Tuple<string, string, long>(stringData, chainName,
                                            block.Timestamp);
                                        if ( !_methodQueue.Contains(queueTuple) )
                                        {
                                            _methodQueue.Enqueue(queueTuple);
                                        }

                                        break;
                                    }
                                    case EventKind.PlatformCreate:
                                    {
                                        var stringData = eventNode.GetParsedData<string>();

                                        if ( eventEntry != null )
                                            StringEventMethods.Upsert(databaseContext, stringData, eventEntry, false);
                                        payload["string_event"] = new Dictionary<string, object?>
                                        {
                                            ["string_value"] = stringData
                                        };

                                        var platform = PlatformMethods.Get(databaseContext, stringData);
                                        if ( platform != null )
                                        {
                                            platform.CreateEvent = eventEntry;
                                        }

                                        break;
                                    }
                                    case EventKind.ContractDeploy:
                                    {
                                        var stringData = eventNode.GetParsedData<string>();

                                        if ( eventEntry != null )
                                            StringEventMethods.Upsert(databaseContext, stringData, eventEntry, false);
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

                                        if ( eventEntry != null )
                                            StringEventMethods.Upsert(databaseContext, stringData, eventEntry, false);
                                        payload["string_event"] = new Dictionary<string, object?>
                                        {
                                            ["string_value"] = stringData
                                        };

                                        var organization = OrganizationMethods.Get(databaseContext, stringData);
                                        if ( organization != null )
                                        {
                                            organization.CreateEvent = eventEntry;
                                        }

                                        break;
                                    }
                                    case EventKind.ChainCreate:
                                    {
                                        var stringData = eventNode.GetParsedData<string>();

                                        if ( eventEntry != null )
                                            StringEventMethods.Upsert(databaseContext, stringData, eventEntry, false);
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

                                        if ( eventEntry != null )
                                            StringEventMethods.Upsert(databaseContext, stringData, eventEntry, false);
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
                            case EventKind.Crowdsale:
                            {
                                var saleEventData = eventNode.GetParsedData<SaleEventData>();

                                var hash = saleEventData.saleHash.ToString();
                                var saleKind = saleEventData.kind.ToString(); //handle sale kinds 

                                //databaseEvent we need it here, so check it
                                if ( eventEntry != null )
                                    SaleEventMethods.Upsert(databaseContext, saleKind, hash, chainEntry, eventEntry,
                                        false);
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

                                //databaseEvent we need it here, so check it
                                if ( eventEntry != null )
                                    TransactionSettleEventMethods.Upsert(databaseContext, hash, platform, chain,
                                        eventEntry, false);
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
                                if ( eventEntry != null )
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

                                //databaseEvent we need it here, so check it
                                if ( eventEntry != null )
                                    ChainEventMethods.Upsert(databaseContext, valueEventName, value, chainEntry,
                                        eventEntry);
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

                                //databaseEvent we need it here, so check it
                                if ( eventEntry != null )
                                    await GasEventMethods.UpsertAsync(databaseContext, address, price, amount, eventEntry,
                                        chainEntry);
                                payload["gas_event"] = new Dictionary<string, object?>
                                {
                                    ["price"] = price,
                                    ["amount"] = amount,
                                    ["address"] = address
                                };

                                break;
                            }
                            case EventKind.FileCreate or EventKind.FileDelete:
                            {
                                var hash = eventNode.GetParsedData<Hash>().ToString();

                                //databaseEvent we need it here, so check it
                                if ( eventEntry != null )
                                    HashEventMethods.Upsert(databaseContext, hash, eventEntry);
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

                                //databaseEvent we need it here, so check it
                                if ( eventEntry != null )
                                    await OrganizationEventMethods.UpsertAsync(databaseContext, organization, memberAddress,
                                        eventEntry, chainEntry);
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
                    catch ( Exception e )
                        {
                            Log.Error(e, "[{Name}][Blocks] Block #{BlockHeight} {UnixSeconds} event processing", Name, block.Height,
                                UnixSeconds.Log(block.Timestamp));

                        try
                        {
                            Log.Information("[{Name}][Blocks] eventNode on exception: {@EventNode}", Name, eventNode);
                        }
                        catch ( Exception e2 )
                        {
                            Log.Information("[{Name}][Blocks] Cannot print eventNode: {Exception}", Name, e2.Message);
                        }

                        try
                        {
                            Log.Information("[{Name}][Blocks] eventNode data on exception: {Exception}", Name,
                                eventNode.Data.Decode());
                        }
                        catch ( Exception e2 )
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

        Log.Verbose("[{Name}][Blocks] Block #{BlockHeight} Found {Count} addresses to reload balances", Name, block.Height,
            addressesToUpdate.Count);

        ChainMethods.SetLastProcessedBlock(databaseContext, chainName, block.Height, false);

        await databaseContext.SaveChangesAsync();

        var processingTime = DateTime.Now - startTime;
        if ( processingTime.TotalSeconds > 1 ) // Log only if processing of the block took > 1 second
        {
            Log.Information(
                "[{Name}][Blocks] Block #{BlockHeight} processed in {ProcessingTime} sec, {EventsAddedCount} events, {NftsInThisBlock} NFTs",
                Name, block.Height, Math.Round(processingTime.TotalSeconds, 3),
                eventsAddedCount, nftsInThisBlock.Count);
        }
        
        return addressesToUpdate.Distinct().ToList();
    }
}
