using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Backend.Api;
using Backend.Commons;
using Backend.PluginEngine;
using Database.ApiCache;
using Database.Main;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Serilog;
using Address = Phantasma.Core.Cryptography.Address;
using BlockMethods = Database.ApiCache.BlockMethods;
using ChainMethods = Database.Main.ChainMethods;
using ContractMethods = Database.Main.ContractMethods;
using Event = Phantasma.Core.Domain.Event;
using EventKind = Phantasma.Core.Domain.EventKind;
using Nft = Database.Main.Nft;
using NftMethods = Database.Main.NftMethods;

namespace Backend.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    // When we reach this number of new loaded events, we report it in log.
    private const int MaxEventsForOneSession = 1000;
    private static int _overallEventsLoadedCount;


    private void FetchBlocks(int chainId, string chainName)
    {
        //not needed anymore, normally 
        CheckData(chainId);

        do
        {
            var startTime = DateTime.Now;


            BigInteger i;
            using ( MainDbContext databaseContext = new() )
            {
                i = ChainMethods.GetLastProcessedBlock(databaseContext, chainId) + 1;
            }

            if ( i == 1 )
                // 16664: First CROWN NFT minted on Phantasma blockchain at 2019-12-30 19:11:09.
                // 21393: First NFT minted on Phantasma blockchain at 2020-02-14 16:03:50.
                // 31172: First NFT traded on Phantasma blockchain at 2020-02-28 12:15:51.
                i = Settings.Default.FirstBlock;

            _overallEventsLoadedCount = 0;
            while ( FetchByHeight(i, chainId, chainName) && _overallEventsLoadedCount < MaxEventsForOneSession ) i++;

            var fetchTime = DateTime.Now - startTime;
            Log.Information(
                "[{Name}] Events load took {FetchTime} sec, {OverallEventsLoadedCount} events added",
                Name, Math.Round(fetchTime.TotalSeconds, 3), _overallEventsLoadedCount);
        } while ( _overallEventsLoadedCount > 0 );
    }


    private bool FetchByHeight(BigInteger blockHeight, int chainId, string chainName)
    {
        var startTime = DateTime.Now;

        JsonDocument blockData = null;
        TimeSpan downloadTime = default;

        using ( ApiCacheDbContext databaseApiCacheContext = new() )
        {
            var highestApiBlock =
                Database.ApiCache.ChainMethods.GetLastProcessedBlock(databaseApiCacheContext, chainId);

            //just to be sure
            if ( highestApiBlock != null )
            {
                var useCache = highestApiBlock > blockHeight;

                Log.Information(
                    "[{Name}] Highest Block in Cache {CacheHeight}, Current need {Height}, Should use Cache {Cache}",
                    Name, highestApiBlock, blockHeight, useCache);

                if ( useCache )
                {
                    var block = BlockMethods.GetByHeight(databaseApiCacheContext, chainId, blockHeight.ToString());
                    blockData = block.DATA;
                }
            }
        }

        if ( blockData == null )
        {
            var url =
                $"{Settings.Default.GetRest()}/api/v1/getBlockByHeight?chainInput={chainName}&height={blockHeight}";

            var response = Client.ApiRequest<JsonDocument>(url, out var stringResponse, null, 10);
            if ( response == null ) return false;

            downloadTime = DateTime.Now - startTime;
            startTime = DateTime.Now;

            var error = "";
            if ( response.RootElement.TryGetProperty("error", out var errorProperty) )
                error = errorProperty.GetString();

            if ( error == "block not found" )
            {
                Log.Debug("[{Name}] getBlockByHeight(): Block {BlockHeight} not found", Name, blockHeight);
                return false;
            }

            if ( !string.IsNullOrEmpty(error) )
            {
                Log.Error("[{Name}] getBlockByHeight() error: {Error}", Name, error);
                return false;
            }

            blockData = response;
        }

        var eventsAddedCount = 0;

        Log.Information("[{Name}] Block #{BlockHeight}: {@Response}", Name, blockHeight, blockData);


        // We cache NFTs in this block to speed up code
        // and avoid some unpleasant situations leading to bugs.
        Dictionary<string, Nft> nftsInThisBlock = new();
        Dictionary<string, bool> symbolFungible = new();

        using MainDbContext databaseContext = new();
        try
        {
            var timestampUnixSeconds = blockData.RootElement.GetProperty("timestamp").GetUInt32();
            var blockHash = blockData.RootElement.GetProperty("hash").GetString();
            var blockPreviousHash = blockData.RootElement.GetProperty("previousHash").GetString();
            var chainAddress = blockData.RootElement.GetProperty("chainAddress").GetString();
            var protocol = blockData.RootElement.GetProperty("protocol").GetInt32();
            var validatorAddress = blockData.RootElement.GetProperty("validatorAddress").GetString();
            var reward = blockData.RootElement.GetProperty("reward").GetString();

            var chainEntry = ChainMethods.Get(databaseContext, chainId);

            // Block in main database
            var block = Database.Main.BlockMethods.Upsert(databaseContext, chainEntry, blockHeight,
                timestampUnixSeconds, blockHash, blockPreviousHash, protocol, chainAddress, validatorAddress, reward,
                false);

            using ( ApiCacheDbContext databaseApiCacheContext = new() )
            {
                BlockMethods.Upsert(databaseApiCacheContext, blockHeight.ToString(), timestampUnixSeconds, blockData,
                    Database.ApiCache.ChainMethods.Get(databaseApiCacheContext, chainName));
            }

            DateTime transactionStart;
            TimeSpan transactionEnd;
            if ( blockData.RootElement.TryGetProperty("oracles", out var oracleProperty) )
            {
                transactionStart = DateTime.Now;
                var oracles = oracleProperty.EnumerateArray().Select(oracle =>
                    new Tuple<string, string>(oracle.GetProperty("url").GetString(),
                        oracle.GetProperty("content").GetString())).ToList();
                BlockOracleMethods.InsertIfNotExists(databaseContext, oracles, block, false);

                transactionEnd = DateTime.Now - transactionStart;
                Log.Verbose("[{Name}] Processed {Count} Oracles in {Time} sec", Name,
                    oracles.Count, Math.Round(transactionEnd.TotalSeconds, 3));
            }


            if ( blockData.RootElement.TryGetProperty("txs", out var txsProperty) )
            {
                var txs = txsProperty.EnumerateArray();
                for ( var txIndex = 0; txIndex < txs.Count(); txIndex++ )
                {
                    var tx = txs.ElementAt(txIndex);

                    JsonElement.ArrayEnumerator events = new();
                    if ( tx.TryGetProperty("events", out var eventsProperty) )
                    {
                        events = eventsProperty.EnumerateArray();
                        Log.Debug(
                            "[{Name}] got {Count} Events for tx #{TxIndex} in block #{BlockHeight}: {@EventsProperty}",
                            Name, events.Count(), txIndex, blockHeight, eventsProperty);
                    }
                    else
                        Log.Debug("[{Name}] No events for tx #{TxIndex} in block #{BlockHeight}", Name, txIndex,
                            blockHeight);

                    // Current transaction
                    var scriptRaw = tx.GetProperty("script").GetString();

                    var transaction = TransactionMethods.Upsert(databaseContext, block, txIndex,
                        tx.GetProperty("hash").GetString(), tx.GetProperty("timestamp").GetUInt32(),
                        tx.GetProperty("payload").GetString(), scriptRaw, tx.GetProperty("result").GetString(),
                        tx.GetProperty("fee").GetString(), tx.GetProperty("expiration").GetUInt32(), false);

                    if ( tx.TryGetProperty("signatures", out var signaturesProperty) )
                    {
                        transactionStart = DateTime.Now;
                        var signatures = signaturesProperty.EnumerateArray().Select(signature =>
                            new Tuple<string, string>(signature.GetProperty("kind").GetString(),
                                signature.GetProperty("data").GetString())).ToList();

                        SignatureMethods.InsertIfNotExists(databaseContext, signatures, transaction, false);

                        transactionEnd = DateTime.Now - transactionStart;
                        Log.Verbose("[{Name}] Processed {Count} Signatures in {Time} sec", Name,
                            signatures.Count, Math.Round(transactionEnd.TotalSeconds, 3));
                    }

                    for ( var eventIndex = 0; eventIndex < events.Count(); eventIndex++ )
                    {
                        var eventNode = events.ElementAt(eventIndex);

                        try
                        {
                            var kindSerialized = eventNode.GetProperty("kind").GetString();
                            var kind = Enum.Parse<EventKind>(kindSerialized);

                            transactionStart = DateTime.Now;
                            var eventKindEntry =
                                EventKindMethods.GetByName(databaseContext, chainEntry, kind.ToString());
                            if ( eventKindEntry == null )
                            {
                                using MainDbContext context = new();
                                var chain = ChainMethods.Get(context, chainId);
                                var kindId = EventKindMethods.Upsert(context, chain, kind.ToString());

                                eventKindEntry = EventKindMethods.GetById(databaseContext, kindId);
                            }

                            transactionEnd = DateTime.Now - transactionStart;
                            Log.Verbose("[{Name}] Added/Got EventKindEntry {Kind} in {Time} sec",
                                Name, kind, Math.Round(transactionEnd.TotalSeconds, 3));

                            Log.Verbose("[{Name}] Processing EventKind {Kind} in Block #{Block}, Index {Index}", Name,
                                kind, blockHeight, eventIndex + 1);

                            var contract = eventNode.GetProperty("contract").GetString();
                            var addressString = eventNode.GetProperty("address").GetString();
                            var addr = Address.FromText(addressString);
                            var data = eventNode.GetProperty("data").GetString().Decode();

                            Event evnt = new(kind, addr, contract, data);

                            //create here the event, and below update the data if needed
                            var contractEntry =
                                ContractMethods.Get(databaseContext, chainEntry, contract, evnt.Contract);
                            if ( contractEntry == null )
                            {
                                transactionStart = DateTime.Now;
                                contractEntry = ContractMethods.Upsert(databaseContext, contract, chainEntry,
                                    evnt.Contract, null, false);
                                transactionEnd = DateTime.Now - transactionStart;
                                Log.Verbose("[{Name}] Added Contract {Contract} in {Time} sec",
                                    Name, contract, Math.Round(transactionEnd.TotalSeconds, 3));
                            }

                            transactionStart = DateTime.Now;
                            var addressEntry = AddressMethods.Upsert(databaseContext, chainEntry, addressString, false);
                            transactionEnd = DateTime.Now - transactionStart;
                            Log.Verbose("[{Name}] Added Address {Address} in {Time} sec",
                                Name, addressString, Math.Round(transactionEnd.TotalSeconds, 3));

                            transactionStart = DateTime.Now;
                            var eventEntry = EventMethods.Upsert(databaseContext, out var eventAdded,
                                timestampUnixSeconds, eventIndex + 1, chainEntry, transaction, contractEntry,
                                eventKindEntry, addressEntry, false);
                            transactionEnd = DateTime.Now - transactionStart;
                            Log.Verbose(
                                "[{Name}] Added Base Event {Kind} Index {Index} in {Time} sec, going on with Processing EventData",
                                Name, kind, eventIndex + 1, Math.Round(transactionEnd.TotalSeconds, 3));

                            if ( eventAdded ) eventsAddedCount++;

                            _overallEventsLoadedCount++;

                            transactionStart = DateTime.Now;
                            switch ( kind )
                            {
                                case EventKind.Infusion:
                                {
                                    Log.Verbose("[{Name}] getting InfusionEventData for {Kind}", Name, kind);

                                    var infusionEventData = evnt.GetContent<InfusionEventData>();

                                    Log.Debug(
                                        "[{Name}] New infusion into NFT {TokenID} {BaseSymbol}, infused {InfusedValue} {InfusedSymbol}, address: {AddressString} contract: {Contract}",
                                        Name, infusionEventData.TokenID, infusionEventData.BaseSymbol,
                                        infusionEventData.InfusedValue, infusionEventData.InfusedSymbol, addressString,
                                        contract);

                                    bool fungible;
                                    if ( symbolFungible.ContainsKey(infusionEventData.BaseSymbol) )
                                        fungible = symbolFungible.GetValueOrDefault(infusionEventData.BaseSymbol);
                                    else
                                    {
                                        fungible = TokenMethods.Get(databaseContext, chainEntry,
                                            infusionEventData.BaseSymbol).FUNGIBLE;
                                        symbolFungible.Add(infusionEventData.BaseSymbol, fungible);
                                    }

                                    contractEntry = ContractMethods.Upsert(databaseContext, contract, chainEntry,
                                        infusionEventData.BaseSymbol.ToUpper(), infusionEventData.BaseSymbol.ToUpper(),
                                        false);

                                    var tokenId = infusionEventData.TokenID.ToString();

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
                                            nft = NftMethods.Upsert(databaseContext, out var newNftCreated, chainEntry,
                                                tokenId, null, contractEntry, false);
                                            Log.Verbose(
                                                "[{Name}] using NFT with internal Id {Id}, Token {Token}, newNFT {New}",
                                                Name, nft.ID, nft.TOKEN_ID, newNftCreated);
                                            if ( newNftCreated ) nftsInThisBlock.Add(tokenId, nft);
                                        }

                                        var nftUpdateTime = DateTime.Now - ntfStartTime;
                                        Log.Verbose("NTF processed in {Time} sec",
                                            Math.Round(nftUpdateTime.TotalSeconds, 3));
                                    }


                                    //parse also a new contract, just in case
                                    eventEntry = EventMethods.UpdateValues(databaseContext, out var eventUpdated,
                                        eventEntry, nft, tokenId, chainEntry, eventKindEntry, contractEntry);

                                    Log.Verbose("[{Name}] Updated event {Kind} with {Updated}", Name, kind,
                                        eventUpdated);

                                    InfusionEventMethods.Upsert(databaseContext, infusionEventData.TokenID.ToString(),
                                        infusionEventData.BaseSymbol, infusionEventData.InfusedSymbol,
                                        infusionEventData.InfusedValue.ToString(), chainEntry, eventEntry, false);
                                    break;
                                }
                                case EventKind.TokenMint or EventKind.TokenClaim or EventKind.TokenBurn
                                    or EventKind.TokenSend or EventKind.TokenReceive or EventKind.TokenStake
                                    or EventKind.CrownRewards or EventKind.Inflation:
                                {
                                    var tokenEventData = evnt.GetContent<TokenEventData>();

                                    Log.Verbose("[{Name}] getting TokenEventData for {Kind}, Chain {Chain}", Name,
                                        kind, tokenEventData.ChainName);

                                    bool fungible;
                                    if ( symbolFungible.ContainsKey(tokenEventData.Symbol) )
                                        fungible = symbolFungible.GetValueOrDefault(tokenEventData.Symbol);
                                    else
                                    {
                                        fungible = TokenMethods.Get(databaseContext, chainEntry, tokenEventData.Symbol)
                                            .FUNGIBLE;
                                        symbolFungible.Add(tokenEventData.Symbol, fungible);
                                    }

                                    contractEntry = ContractMethods.Upsert(databaseContext, contract, chainEntry,
                                        tokenEventData.Symbol.ToUpper(), tokenEventData.Symbol.ToUpper(), false);

                                    var tokenId = tokenEventData.Value.ToString();

                                    Nft nft = null;
                                    if ( !fungible )
                                    {
                                        var ntfStartTime = DateTime.Now;
                                        Log.Debug(
                                            "[{Name}] New event on {TimestampUnixSeconds}: kind: {Kind} symbol: {Symbol} value: {Value} address: {AddressString} contract: {Contract}",
                                            Name, UnixSeconds.Log(timestampUnixSeconds), kind, tokenEventData.Symbol,
                                            tokenEventData.Value, addressString, contract);

                                        if ( nftsInThisBlock.ContainsKey(tokenId) )
                                            nft = nftsInThisBlock.GetValueOrDefault(tokenId);
                                        else
                                        {
                                            nft = NftMethods.Upsert(databaseContext, out var newNftCreated, chainEntry,
                                                tokenId, null, contractEntry, false);
                                            Log.Verbose(
                                                "[{Name}] using NFT with internal Id {Id}, Token {Token}, newNFT {New}",
                                                Name, nft.ID, nft.TOKEN_ID, newNftCreated);
                                            if ( newNftCreated ) nftsInThisBlock.Add(tokenId, nft);
                                        }

                                        // We should always properly check mint event and update mint date,
                                        // because nft can be created by auction thread without mint date,
                                        // so we can't update dates only for just created NFTs using newNftCreated flag.
                                        if ( kind == EventKind.TokenMint )
                                            nft.MINT_DATE_UNIX_SECONDS = timestampUnixSeconds;

                                        var nftUpdateTime = DateTime.Now - ntfStartTime;
                                        Log.Verbose("NTF processed in {Time} sec",
                                            Math.Round(nftUpdateTime.TotalSeconds, 3));
                                    }

                                    //parse also a new contract, just in case
                                    eventEntry = EventMethods.UpdateValues(databaseContext, out var eventUpdated,
                                        eventEntry, nft, tokenId, chainEntry, eventKindEntry, contractEntry);

                                    Log.Verbose("[{Name}] Updated event {Kind} with {Updated}", Name, kind,
                                        eventUpdated);

                                    //update ntf related things if it is not null
                                    if ( nft != null )
                                        // Update NFTs owner address on new event.
                                        NftMethods.ProcessOwnershipChange(databaseContext, chainEntry, nft,
                                            timestampUnixSeconds, addressEntry, false);

                                    TokenEventMethods.Upsert(databaseContext, tokenEventData.Symbol,
                                        tokenEventData.ChainName, tokenId, chainEntry, eventEntry, false);

                                    break;
                                }
                                case EventKind.OrderCancelled or EventKind.OrderClosed or EventKind.OrderCreated
                                    or EventKind.OrderFilled or EventKind.OrderBid:
                                {
                                    Log.Verbose("[{Name}] getting MarketEventData for {Kind}", Name, kind);

                                    var marketEventData = evnt.GetContent<MarketEventData>();

                                    bool fungible;
                                    if ( symbolFungible.ContainsKey(marketEventData.BaseSymbol) )
                                        fungible = symbolFungible.GetValueOrDefault(marketEventData.BaseSymbol);
                                    else
                                    {
                                        fungible = TokenMethods.Get(databaseContext, chainEntry,
                                            marketEventData.BaseSymbol).FUNGIBLE;
                                        symbolFungible.Add(marketEventData.BaseSymbol, fungible);
                                    }

                                    Log.Debug(
                                        "[{Name}] New event on {TimestampUnixSeconds}: kind: {Kind} baseSymbol: {BaseSymbol} quoteSymbol: {QuoteSymbol} price: {Price} endPrice: {EndPrice} id: {ID} address: {AddressString} contract: {Contract} type: {Type}",
                                        Name, UnixSeconds.Log(timestampUnixSeconds), kind, marketEventData.BaseSymbol,
                                        marketEventData.QuoteSymbol, marketEventData.Price, marketEventData.EndPrice,
                                        marketEventData.ID, addressString, contract, marketEventData.Type);


                                    contractEntry = ContractMethods.Upsert(databaseContext, contract, chainEntry,
                                        marketEventData.BaseSymbol.ToUpper(), marketEventData.BaseSymbol.ToUpper(),
                                        false);

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
                                            nft = NftMethods.Upsert(databaseContext, out var newNftCreated, chainEntry,
                                                tokenId, null, contractEntry, false);
                                            Log.Verbose(
                                                "[{Name}] using NFT with internal Id {Id}, Token {Token}, newNFT {New}",
                                                Name, nft.ID, nft.TOKEN_ID, newNftCreated);
                                            if ( newNftCreated ) nftsInThisBlock.Add(tokenId, nft);
                                        }

                                        if ( kind == EventKind.OrderFilled )
                                            // Update NFTs owner address on new sale event.
                                            NftMethods.ProcessOwnershipChange(databaseContext, chainEntry, nft,
                                                timestampUnixSeconds, addressEntry, false);
                                        var nftUpdateTime = DateTime.Now - ntfStartTime;
                                        Log.Verbose("NTF processed in {Time} sec",
                                            Math.Round(nftUpdateTime.TotalSeconds, 3));
                                    }

                                    //parse also a new contract, just in case
                                    eventEntry = EventMethods.UpdateValues(databaseContext, out var eventUpdated,
                                        eventEntry, nft, tokenId, chainEntry, eventKindEntry, contractEntry);

                                    Log.Verbose("[{Name}] Updated event {Kind} with {Updated}", Name, kind,
                                        eventUpdated);

                                    MarketEventMethods.Upsert(databaseContext, marketEventData.Type.ToString(),
                                        marketEventData.BaseSymbol, marketEventData.QuoteSymbol,
                                        marketEventData.Price.ToString(), marketEventData.EndPrice.ToString(),
                                        marketEventData.ID.ToString(), chainEntry, eventEntry, false);

                                    break;
                                }
                                case EventKind.ChainCreate or EventKind.TokenCreate or EventKind.ContractUpgrade
                                    or EventKind.AddressRegister or EventKind.ContractDeploy or EventKind.PlatformCreate
                                    or EventKind.OrganizationCreate or EventKind.Log or EventKind.AddressUnregister:
                                {
                                    var stringData = evnt.GetContent<string>();

                                    Log.Verbose("[{Name}] getting string for {Kind}, string {String}", Name, kind,
                                        stringData);

                                    //databaseEvent we need it here, so check it
                                    if ( eventEntry != null )
                                        StringEventMethods.Upsert(databaseContext, stringData, eventEntry, false);

                                    switch ( kind )
                                    {
                                        case EventKind.ContractUpgrade:
                                        {
                                            var queueTuple = new Tuple<string, int, long>(stringData, chainId,
                                                timestampUnixSeconds);
                                            if ( !_methodQueue.Contains(queueTuple) )
                                            {
                                                Log.Verbose("[{Name}] got {Kind} adding Contract {Contract} to Queue",
                                                    Name, kind, stringData);
                                                _methodQueue.Enqueue(queueTuple);
                                            }

                                            break;
                                        }
                                        case EventKind.TokenCreate:
                                        {
                                            var token = TokenMethods.Get(databaseContext, chainEntry, stringData);
                                            if ( token != null )
                                            {
                                                Log.Verbose("[{Name}] Linking Event to Token {Token}", Name,
                                                    token.SYMBOL);
                                                token.CreateEvent = eventEntry;
                                            }

                                            break;
                                        }
                                        case EventKind.PlatformCreate:
                                        {
                                            var platform = PlatformMethods.Get(databaseContext, stringData);
                                            if ( platform != null )
                                            {
                                                Log.Verbose("[{Name}] Linking Event to Platform {Platform}", Name,
                                                    platform.NAME);
                                                platform.CreateEvent = eventEntry;
                                            }

                                            break;
                                        }
                                        case EventKind.ContractDeploy:
                                        {
                                            //we might have to create the contract here, better be sure
                                            var contractItem = ContractMethods.Upsert(databaseContext, stringData,
                                                chainEntry, stringData,
                                                null, false);
                                            //we do it like this, to be sure it is only set here
                                            contractItem.CreateEvent = eventEntry;

                                            Log.Verbose("[{Name}] Linked Event to Contract {Contract}", Name,
                                                contractItem.NAME);

                                            break;
                                        }
                                        case EventKind.OrganizationCreate:
                                        {
                                            var organization = OrganizationMethods.Get(databaseContext, stringData);
                                            if ( organization != null )
                                            {
                                                Log.Verbose("[{Name}] Linking Event to Organization {Organization}",
                                                    Name, organization.NAME);
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
                                    var saleEventData = evnt.GetContent<SaleEventData>();

                                    var hash = saleEventData.saleHash.ToString();
                                    var saleKind = saleEventData.kind.ToString(); //handle sale kinds 

                                    Log.Verbose(
                                        "[{Name}] getting SaleEventData for {Kind}, hash {Hash}, saleKind {SaleKind}",
                                        Name, kind, hash, saleKind);

                                    //databaseEvent we need it here, so check it
                                    if ( eventEntry != null )
                                        SaleEventMethods.Upsert(databaseContext, saleKind, hash, chainEntry, eventEntry,
                                            false);

                                    break;
                                }
                                case EventKind.ChainSwap:
                                {
                                    var transactionSettleEventData =
                                        evnt.GetContent<TransactionSettleEventData>();

                                    var hash = transactionSettleEventData.Hash.ToString();
                                    var platform = transactionSettleEventData.Platform;
                                    var chain = transactionSettleEventData.Chain;

                                    Log.Verbose(
                                        "[{Name}] getting TransactionSettleEventData for {Kind}, hash {Hash}, platform {Platform}, chain {Chain}",
                                        Name, kind, hash, platform, chain);

                                    //databaseEvent we need it here, so check it
                                    if ( eventEntry != null )
                                        TransactionSettleEventMethods.Upsert(databaseContext, hash, platform, chain,
                                            eventEntry, false);

                                    break;
                                }
                                case EventKind.ValidatorElect or EventKind.ValidatorPropose:
                                {
                                    var address = evnt.GetContent<Address>().ToString();

                                    Log.Verbose("[{Name}] getting Address for {Kind}, Address {Address}", Name,
                                        kind, address);

                                    //databaseEvent we need it here, so check it
                                    if ( eventEntry != null )
                                        AddressEventMethods.Upsert(databaseContext, address, eventEntry, chainEntry,
                                            false);

                                    break;
                                }
                                //TODO
                                case EventKind.ValidatorSwitch:
                                {
                                    Log.Verbose("[{Name}] getting nothing for {Kind}", Name, kind);

                                    break;
                                }
                                case EventKind.ValueCreate or EventKind.ValueUpdate:
                                {
                                    var chainValueEventData = evnt.GetContent<ChainValueEventData>();

                                    var valueEventName = chainValueEventData.Name;
                                    var value = chainValueEventData.Value.ToString();

                                    Log.Verbose(
                                        "[{Name}] getting ChainValueEventData for {Kind}, Name {ValueEventName}, Value {Value}",
                                        Name, kind, valueEventName, value);

                                    //databaseEvent we need it here, so check it
                                    if ( eventEntry != null )
                                        ChainEventMethods.Upsert(databaseContext, valueEventName, value, chainEntry,
                                            eventEntry, false);

                                    break;
                                }
                                case EventKind.GasEscrow or EventKind.GasPayment:
                                {
                                    var gasEventData = evnt.GetContent<GasEventData>();

                                    var address = gasEventData.address.ToString();
                                    var price = gasEventData.price.ToString();
                                    var amount = gasEventData.amount.ToString();

                                    Log.Verbose(
                                        "[{Name}] getting GasEventData for {Kind}, Address {Address}, price {Price}, amount {Amount}",
                                        Name, kind, address, price, amount);

                                    //databaseEvent we need it here, so check it
                                    if ( eventEntry != null )
                                        GasEventMethods.Upsert(databaseContext, address, price, amount, eventEntry,
                                            chainEntry, false);

                                    break;
                                }
                                case EventKind.FileCreate or EventKind.FileDelete:
                                {
                                    var hash = evnt.GetContent<Hash>().ToString();

                                    Log.Verbose("[{Name}] getting Hash for {Kind}, Hash {Hash}", Name, kind, hash);

                                    //databaseEvent we need it here, so check it
                                    if ( eventEntry != null )
                                        HashEventMethods.Upsert(databaseContext, hash, eventEntry, false);

                                    break;
                                }
                                case EventKind.OrganizationAdd or EventKind.OrganizationRemove:
                                {
                                    var organizationEventData = evnt.GetContent<OrganizationEventData>();

                                    var organization = organizationEventData.Organization;
                                    var memberAddress = organizationEventData.MemberAddress.ToString();

                                    Log.Verbose(
                                        "[{Name}] getting OrganizationEventData for {Kind}, Organization {Organization}, MemberAddress {Address}",
                                        Name, kind, organization, memberAddress);

                                    //databaseEvent we need it here, so check it
                                    if ( eventEntry != null )
                                        OrganizationEventMethods.Upsert(databaseContext, organization, memberAddress,
                                            eventEntry, chainEntry, false);

                                    break;
                                }
                                //TODO
                                case EventKind.LeaderboardCreate or EventKind.Custom:
                                {
                                    Log.Verbose("[{Name}] Currently not processing EventKind {Kind} in Block #{Block}",
                                        Name, kind, blockHeight);

                                    break;
                                }
                                default:
                                    Log.Warning("[{Name}] Currently not processing EventKind {Kind} in Block #{Block}",
                                        Name, kind, blockHeight);
                                    break;
                            }

                            transactionEnd = DateTime.Now - transactionStart;
                            Log.Verbose("[{Name}] Processed Event {Kind} Index {Index} in {Time} sec", Name, kind,
                                eventIndex + 1, Math.Round(transactionEnd.TotalSeconds, 3));
                        }
                        catch ( Exception e )
                        {
                            Log.Error(e, "[{Name}] {UnixSeconds} event processing", Name,
                                UnixSeconds.Log(timestampUnixSeconds));

                            try
                            {
                                Log.Information("[{Name}] eventNode on exception: {@EventNode}", Name, eventNode);
                            }
                            catch ( Exception e2 )
                            {
                                Log.Information("[{Name}] Cannot print eventNode: {Exception}", Name, e2.Message);
                            }

                            try
                            {
                                Log.Information("[{Name}] eventNode data on exception: {Exception}", Name,
                                    eventNode.GetProperty("data").GetString().Decode());
                            }
                            catch ( Exception e2 )
                            {
                                Log.Information("[{Name}] Cannot print eventNode data: {Exception}", Name,
                                    e2.Message);
                            }
                        }
                    }
                }
            }

            ChainMethods.SetLastProcessedBlock(databaseContext, chainId, blockHeight, false);

            transactionStart = DateTime.Now;
            databaseContext.SaveChanges();
            transactionEnd = DateTime.Now - transactionStart;
            Log.Verbose("[{Name}] Commit took {Time} sec, after Process of Block {Height}", Name,
                Math.Round(transactionEnd.TotalSeconds, 3), blockHeight);
        }
        catch ( Exception ex )
        {
            ExceptionDispatchInfo.Capture(ex).Throw();
        }

        var processingTime = DateTime.Now - startTime;
        Log.Information(
            "[{Name}] Block {BlockHeight} loaded in {DownloadTime} sec, processed in {ProcessingTime} sec, {EventsAddedCount} events added, {NftsInThisBlock} NFTs processed",
            Name, blockHeight, Math.Round(downloadTime.TotalSeconds, 3), Math.Round(processingTime.TotalSeconds, 3),
            eventsAddedCount, nftsInThisBlock.Count);

        return true;
    }
}
