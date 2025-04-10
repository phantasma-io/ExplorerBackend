using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Backend.Api;
using Backend.Blockchain.Responses;
using Backend.Commons;
using Backend.PluginEngine;
using Database.Main;
using Npgsql;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Contract.Sale.Structs;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Numerics;
using Serilog;
using Address = Phantasma.Core.Cryptography.Structs.Address;
using ChainMethods = Database.Main.ChainMethods;
using ContractMethods = Database.Main.ContractMethods;
using EventKind = Phantasma.Core.Domain.Events.Structs.EventKind;
using Nft = Database.Main.Nft;
using NftMethods = Database.Main.NftMethods;

namespace Backend.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    private const int FetchBlocksPerIterationMax = 100;
    private long _balanceRefetchDate = 0;
    private string _balanceRefetchTimestampKey = "BALANCE_REFETCH_TIMESTAMP";

    private async Task FetchBlocksRange(string chainName, BigInteger fromHeight, BigInteger toHeight)
    {
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
            foreach ( var (blockHeight, block) in blocks )
            {
                // Log.Information("PROCESSING HEIGHT " + blockHeight);
                addressesToUpdate.AddRange(await ProcessBlock(blockHeight, block, chainName));
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
                var chainEntry = await ChainMethods.GetAsync(databaseContext, chainName);
                await UpdateAddressesBalancesAsync(databaseContext, chainEntry, addressesToUpdate,
                    100);
                
                if(_balanceRefetchDate == 0)
                {
                    // We just finished refetching all balances, saving timestamp
                    // to the database which will tell us when this process was done.
                    await GlobalVariableMethods.UpsertAsync(databaseContext, _balanceRefetchTimestampKey, UnixSeconds.Now());
                }

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

            i += fetchPerIteration;
        }
    }

    private static double _megabyte = 1024.0 * 1024.0;
    private static string ToMegabytesString(long byteCount)
    {
        double mb = byteCount / _megabyte;
        return $"{mb:F2} MB";
    }
    private async Task<(BigInteger, BlockResult)> GetBlockAsync(string chainName, BigInteger blockHeight)
    {
        var url = $"{Settings.Default.GetRest()}/api/v1/getBlockByHeight?chainInput={chainName}&height={blockHeight}";

        var startTime = DateTime.Now;
        var (response, blockLength) = await Client.ApiRequestAsync<BlockResult>(url, 10);

        var requestTime = (DateTime.Now - startTime).TotalMilliseconds;
        if(blockLength > _megabyte)
        {
            Log.Warning("[{Name}][Blocks] Large block #{index} | {size} received in {time} ms", Name, blockHeight, ToMegabytesString(blockLength), Math.Round(requestTime, 2));
        }
        else if(requestTime > 200) // TODO Set through config
        {
            Log.Information("[{Name}][Blocks] Block #{index} | {size} received in {time} ms", Name, blockHeight, ToMegabytesString(blockLength), Math.Round(requestTime, 2));
        }

        if (!string.IsNullOrEmpty(response.error))
        {
            throw new Exception(response.error);
        }
        
        return (blockHeight, response);
    }


    private async Task<List<(BigInteger, BlockResult)>> GetBlockRange(string chainName, BigInteger fromHeight, BigInteger blockCount)
    {
        // Log.Information("FETCHING RANGE " + fromHeight + " - " + (fromHeight + blockCount - 1));
        var tasks = new List<Task<(BigInteger, BlockResult)>>();
        var smallTasks = new List<Task<(BigInteger, BlockResult)>>();
        for (var i = fromHeight; i < fromHeight + blockCount; i++)
        {
            var task = GetBlockAsync(chainName, i);
            tasks.Add(task);
            smallTasks.Add(task);

            if (smallTasks.Count == 50)
            {
                await Task.WhenAll(smallTasks.ToArray());
                smallTasks = new List<Task<(BigInteger, BlockResult)>>();
                await Task.Delay(1000);
            }
        }

        if (smallTasks.Count > 0)
            await Task.WhenAll(smallTasks.ToArray());

        return tasks.Select(task => task.Result).ToList();
    }

    private void AddAddress(ref List<string> addresses, string address)
    {
        if(string.IsNullOrEmpty(address))
        {
            throw new("Trying to add empty address");
        }

        addresses.Add(address);
    }
    
    private async Task<List<string>> ProcessBlock(BigInteger blockHeight, BlockResult block, string chainName)
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

        AddAddress(ref addressesToUpdate, block.chainAddress);
        AddAddress(ref addressesToUpdate, block.validatorAddress);

        var chainEntry = await ChainMethods.GetAsync(databaseContext, chainName);
        var chainId = chainEntry.ID;

        // Block in main database
        var blockEntity = await Database.Main.BlockMethods.UpsertAsync(databaseContext, chainEntry, blockHeight,
            block.timestamp, block.hash, block.previousHash, block.protocol, block.chainAddress, block.validatorAddress, block.reward);

        if (block.oracles?.Length > 0)
        {
            var oracles = block.oracles.Select(oracle =>
                new Tuple<string, string>(oracle.url,
                    oracle.content)).ToList();
            BlockOracleMethods.InsertIfNotExists(databaseContext, oracles, blockEntity);
        }

        if (block.txs?.Length > 0)
        {
            Log.Verbose("[{Name}][Blocks] Block #{BlockHeight} Found {Count} txes in the block", Name, blockHeight,
                block.txs.Length);
            
            block.ParseData();
            var contractHashes = block.GetContracts();
            // Log.Verbose("[{Name}][Blocks] Contracts in block: " + string.Join(",", contractHashes));

            var contracts = ContractMethods.BatchUpsert(dbConnection,
                contractHashes.Select(c => (c, chainId, c, c)).ToList());

            for (var txIndex = 0; txIndex < block.txs.Length; txIndex++)
            {
                var tx = block.txs[txIndex];

                var transaction = await TransactionMethods.UpsertAsync(databaseContext, blockEntity, txIndex,
                    tx.hash, tx.timestamp,
                    tx.payload, tx.script, tx.result,
                    tx.fee, tx.expiration,
                    tx.gasPrice, tx.gasLimit,
                    tx.state, tx.sender,
                    tx.gasPayer, tx.gasTarget);

                if (tx.signatures?.Length > 0)
                {
                    var signatures = tx.signatures
                        .Select(signature => new Tuple<string, string>(signature.kind, signature.data))
                        .ToList();

                    SignatureMethods.InsertIfNotExists(databaseContext, signatures, transaction);
                }

                if (tx.events == null || tx.events.Length == 0)
                {
                    continue;
                }

                for ( var eventIndex = 0; eventIndex < tx.events.Length; eventIndex++ )
                {
                    var eventNode = tx.events[eventIndex];

                    try
                    {
                        var kindSerialized = eventNode.Kind;
                        var kind = Enum.Parse<EventKind>(kindSerialized);

                        var eventKindId = _eventKinds.GetId(chainId, kind);

                        var contract = eventNode.Contract;
                        var addressString = eventNode.Address;
                        AddAddress(ref addressesToUpdate, addressString);

                        //create here the event, and below update the data if needed
                        var contractId = contracts.GetId(chainId, contract);

                        var addressEntry = await AddressMethods.UpsertAsync(databaseContext, chainEntry, addressString);

                        var eventEntry = EventMethods.Upsert(databaseContext, out var eventAdded,
                            block.timestamp, eventIndex + 1, chainEntry, transaction, contractId,
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
                                    fungible = (await TokenMethods.GetAsync(databaseContext, chainEntry,
                                        infusionEventData.BaseSymbol)).FUNGIBLE;
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
                                    fungible = (await TokenMethods.GetAsync(databaseContext, chainEntry, tokenEventData.Symbol))
                                        .FUNGIBLE;
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
                                        nft.MINT_DATE_UNIX_SECONDS = block.timestamp;
                                }

                                //parse also a new contract, just in case
                                var eventUpdated = await EventMethods.UpdateValuesAsync(databaseContext,
                                    eventEntry, nft, tokenValue, chainEntry, kind, eventKindId, contracts.GetId(chainId, tokenEventData.Symbol));

                                //update ntf related things if it is not null
                                if ( nft != null )
                                    // Update NFTs owner address on new event.
                                    NftMethods.ProcessOwnershipChange(databaseContext, chainEntry, nft,
                                        block.timestamp, addressEntry, false);

                                await TokenEventMethods.UpsertAsync(databaseContext, tokenEventData.Symbol,
                                    tokenEventData.ChainName, tokenValue, chainEntry, eventEntry);

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
                                            block.timestamp, addressEntry, false);
                                }

                                //parse also a new contract, just in case
                                var eventUpdated = await EventMethods.UpdateValuesAsync(databaseContext,
                                    eventEntry, nft, tokenId, chainEntry, kind, eventKindId, contracts.GetId(chainId, marketEventData.BaseSymbol));

                                await MarketEventMethods.InsertAsync(databaseContext, marketEventData.Type.ToString(),
                                    marketEventData.BaseSymbol, marketEventData.QuoteSymbol,
                                    marketEventData.Price.ToString(), marketEventData.EndPrice.ToString(),
                                    marketEventData.ID.ToString(), chainEntry, eventEntry);

                                break;
                            }
                            case EventKind.ChainCreate or EventKind.TokenCreate or EventKind.ContractUpgrade
                                or EventKind.AddressRegister or EventKind.ContractDeploy or EventKind.PlatformCreate
                                or EventKind.OrganizationCreate or EventKind.Log or EventKind.AddressUnregister:
                                //or EventKind.Error:
                            {
                                var stringData = eventNode.GetParsedData<string>();

                                //databaseEvent we need it here, so check it
                                if ( eventEntry != null )
                                    StringEventMethods.Upsert(databaseContext, stringData, eventEntry, false);

                                switch ( kind )
                                {
                                    case EventKind.ContractUpgrade:
                                    {
                                        var queueTuple = new Tuple<string, string, long>(stringData, chainName,
                                            block.timestamp);
                                        if ( !_methodQueue.Contains(queueTuple) )
                                        {
                                            _methodQueue.Enqueue(queueTuple);
                                        }

                                        break;
                                    }
                                    case EventKind.TokenCreate:
                                    {
                                        var token = await TokenMethods.GetAsync(databaseContext, chainEntry, stringData);
                                        if ( token != null )
                                        {
                                            token.CreateEvent = eventEntry;
                                        }

                                        break;
                                    }
                                    case EventKind.PlatformCreate:
                                    {
                                        var platform = PlatformMethods.Get(databaseContext, stringData);
                                        if ( platform != null )
                                        {
                                            platform.CreateEvent = eventEntry;
                                        }

                                        break;
                                    }
                                    case EventKind.ContractDeploy:
                                    {
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
                                        var organization = OrganizationMethods.Get(databaseContext, stringData);
                                        if ( organization != null )
                                        {
                                            organization.CreateEvent = eventEntry;
                                        }

                                        break;
                                    }
                                    case EventKind.ChainCreate:
                                    {
                                        //fill data
                                        break;
                                    }
                                }

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

                                break;
                            }
                            case EventKind.ValidatorElect or EventKind.ValidatorPropose:
                            {
                                var address = eventNode.GetParsedData<Address>().ToString();
                                AddAddress(ref addressesToUpdate, address);

                                //databaseEvent we need it here, so check it
                                if ( eventEntry != null )
                                    eventEntry.TargetAddress = await AddressMethods.UpsertAsync(databaseContext, chainEntry, address);

                                break;
                            }
                            //TODO
                            case EventKind.ValidatorSwitch:
                            {
                                Log.Verbose("[{Name}][Blocks] Block #{BlockHeight} getting nothing for {Kind}", Name, blockHeight, kind);

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

                                break;
                            }
                            case EventKind.FileCreate or EventKind.FileDelete:
                            {
                                var hash = eventNode.GetParsedData<Hash>().ToString();

                                //databaseEvent we need it here, so check it
                                if ( eventEntry != null )
                                    HashEventMethods.Upsert(databaseContext, hash, eventEntry);

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

                                break;
                            }
                            //TODO
                            case EventKind.LeaderboardCreate or EventKind.Custom:
                            {
                                Log.Verbose("[{Name}][Blocks] Block #{BlockHeight} Currently not processing EventKind {Kind} in Block #{Block}",
                                    Name, blockHeight, kind, blockHeight);
                                break;
                            }
                            default:
                                Log.Warning("[{Name}][Blocks] Block #{BlockHeight} Currently not processing EventKind {Kind} in Block #{Block}",
                                    Name, blockHeight, kind, blockHeight);
                                break;
                        }
                    }
                    catch ( Exception e )
                    {
                        Log.Error(e, "[{Name}][Blocks] Block #{BlockHeight} {UnixSeconds} event processing", Name, blockHeight,
                            UnixSeconds.Log(block.timestamp));

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
                }
            }
        }

        _balanceRefetchDate = await GlobalVariableMethods.GetLongAsync(databaseContext, _balanceRefetchTimestampKey);
        if(_balanceRefetchDate == 0)
        {
            // Reprocess balances of ALL known addresses
            // to fix issues
            addressesToUpdate = databaseContext.Addresses.Select(x => x.ADDRESS).Where(x => x.ToUpper() != "NULL").Distinct().ToList();
        }

        Log.Verbose("[{Name}][Blocks] Block #{BlockHeight} Found {Count} addresses to reload balances", Name, blockHeight,
            addressesToUpdate.Count);

        ChainMethods.SetLastProcessedBlock(databaseContext, chainName, blockHeight, false);

        await databaseContext.SaveChangesAsync();

        var processingTime = DateTime.Now - startTime;
        if ( processingTime.TotalSeconds > 1 ) // Log only if processing of the block took > 1 second
        {
            Log.Information(
                "[{Name}][Blocks] Block #{BlockHeight} processed in {ProcessingTime} sec, {EventsAddedCount} events, {NftsInThisBlock} NFTs",
                Name, blockHeight, Math.Round(processingTime.TotalSeconds, 3),
                eventsAddedCount, nftsInThisBlock.Count);
        }
        
        return addressesToUpdate.Distinct().ToList();
    }
}
