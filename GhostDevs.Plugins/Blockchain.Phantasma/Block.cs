using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Database.ApiCache;
using Database.Main;
using GhostDevs.Api;
using GhostDevs.Commons;
using GhostDevs.PluginEngine;
using Phantasma.Blockchain.Contracts;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Serilog;
using Address = Phantasma.Cryptography.Address;
using BigInteger = System.Numerics.BigInteger;
using BlockMethods = Database.ApiCache.BlockMethods;
using ChainMethods = Database.Main.ChainMethods;
using ContractMethods = Database.Main.ContractMethods;
using Event = Phantasma.Domain.Event;
using EventKind = Phantasma.Domain.EventKind;
using Nft = Database.Main.Nft;
using NftMethods = Database.Main.NftMethods;

namespace GhostDevs.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    // When we reach this number of new loaded events, we report it in log.
    private const int MaxEventsForOneSession = 1000;
    private static int _overallEventsLoadedCount;


    private void FetchBlocks(int chainId, string chainName)
    {
        do
        {
            var startTime = DateTime.Now;

            BigInteger i = 1;
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
            var useCache = highestApiBlock > blockHeight;

            Log.Information(
                "[{Name}] Highest Block in Cache {CacheHeight}, Current need {Height}, Should use Cache {Cache}", Name,
                highestApiBlock, blockHeight, useCache);

            if ( useCache )
            {
                var block = BlockMethods.GetByHeight(databaseApiCacheContext, chainId,
                    blockHeight.ToString());
                blockData = block.DATA;
            }
        }

        if ( blockData == null )
        {
            var url = $"{Settings.Default.GetRest()}/api/getBlockByHeight?chainInput={chainName}&height={blockHeight}";

            var response = Client.APIRequest<JsonDocument>(url, out var stringResponse, null, 10);
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

            // Block in main database
            var block = Database.Main.BlockMethods.Upsert(databaseContext, chainId, blockHeight, timestampUnixSeconds,
                blockHash, blockPreviousHash, protocol, chainAddress, validatorAddress, reward, false);

            using ( ApiCacheDbContext databaseApiCacheContext = new() )
            {
                BlockMethods.Upsert(databaseApiCacheContext, chainName, blockHeight.ToString(), timestampUnixSeconds,
                    blockData, chainId);
            }

            if ( blockData.RootElement.TryGetProperty("oracles", out var oracleProperty) )
            {
                var oracles = oracleProperty.EnumerateArray();
                foreach ( var oracle in oracles )
                {
                    var url = oracle.GetProperty("url").ToString();
                    var content = oracle.GetProperty("content").ToString();

                    Log.Debug("[{Name}] got Oracle, url {Url}, content {Content}", Name, url, content);
                }
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
                        tx.GetProperty("payload").GetString(), scriptRaw, false);

                    var entryCount =
                        TransactionScriptInstructionMethods.Upsert(databaseContext, transaction,
                            Utils.GetInstructionsFromScript(scriptRaw));

                    Log.Verbose("[{Name}] Transaction, added Script Instructions Count {Count}", Name, entryCount);

                    for ( var eventIndex = 0; eventIndex < events.Count(); eventIndex++ )
                    {
                        var eventNode = events.ElementAt(eventIndex);

                        try
                        {
                            var kindSerialized = eventNode.GetProperty("kind").GetString();
                            var kind = Enum.Parse<EventKind>(kindSerialized);
                            //just works if we process every event, otherwise we would have data in the database we do not really need
                            var eventKindId = EventKindMethods.Upsert(databaseContext, chainId, kind.ToString());

                            Log.Verbose("[{Name}] Processing EventKind {Kind} in Block #{Block}", Name, kind,
                                blockHeight);

                            var contract = eventNode.GetProperty("contract").GetString();
                            var addressString = eventNode.GetProperty("address").GetString();
                            var addr = Address.FromText(addressString);
                            var data = eventNode.GetProperty("data").GetString().Decode();

                            Event evnt = new(kind, addr, contract, data);

                            var added = false;

                            Database.Main.Event databaseEvent = null;
                            //currently still handled extra... for now
                            //create here the event, and below the data
                            if ( kind is not EventKind.Infusion or EventKind.TokenMint or EventKind.TokenClaim
                                or EventKind.TokenBurn or EventKind.TokenSend or EventKind.TokenReceive
                                or EventKind.TokenStake or EventKind.CrownRewards or EventKind.OrderCancelled
                                or EventKind.OrderClosed or EventKind.OrderCreated or EventKind.OrderFilled
                                or EventKind.OrderBid )
                            {
                                var contractId = ContractMethods.Upsert(databaseContext, contract,
                                    chainId,
                                    evnt.Contract,
                                    null);

                                databaseEvent = EventMethods.Upsert(databaseContext,
                                    out var eventAdded,
                                    null,
                                    timestampUnixSeconds,
                                    eventIndex + 1,
                                    chainId,
                                    transaction,
                                    contractId,
                                    eventKindId,
                                    null,
                                    null,
                                    null,
                                    null,
                                    0,
                                    null,
                                    null,
                                    null,
                                    null,
                                    addressString,
                                    null,
                                    false);

                                if ( eventAdded ) eventsAddedCount++;

                                added = eventAdded;
                                _overallEventsLoadedCount++;
                            }

                            switch ( kind )
                            {
                                case EventKind.Infusion:
                                {
                                    Log.Verbose("[{Name}] getting InfusionEventData for {Kind}", Name, kind);

                                    var infusionEventData = evnt.GetContent<InfusionEventData>();

                                    Log.Debug(
                                        "[{Name}] New infusion into NFT {TokenID} {BaseSymbol}, infused {InfusedValue} {InfusedSymbol}, address: {AddressString} contract: {Contract}",
                                        Name, infusionEventData.TokenID, infusionEventData.BaseSymbol,
                                        infusionEventData.InfusedValue, infusionEventData.InfusedSymbol,
                                        addressString, contract);

                                    bool fungible;
                                    if ( symbolFungible.ContainsKey(infusionEventData.BaseSymbol) )
                                        fungible = symbolFungible.GetValueOrDefault(infusionEventData.BaseSymbol);
                                    else
                                    {
                                        fungible = TokenMethods.Get(
                                            databaseContext, chainId,
                                            infusionEventData.BaseSymbol).FUNGIBLE;
                                        symbolFungible.Add(infusionEventData.BaseSymbol, fungible);
                                    }

                                    if ( !fungible )
                                    {
                                        var contractId = ContractMethods.Upsert(databaseContext, contract,
                                            chainId,
                                            infusionEventData.BaseSymbol.ToUpper(),
                                            infusionEventData.BaseSymbol.ToUpper());

                                        var tokenId = infusionEventData.TokenID.ToString();

                                        Nft nft;
                                        // Searching for corresponding NFT.
                                        // If it's available, we will set up relation.
                                        // If not, we will create it first.
                                        if ( nftsInThisBlock.ContainsKey(tokenId) )
                                            nft = nftsInThisBlock.GetValueOrDefault(tokenId);
                                        else
                                        {
                                            nft = NftMethods.Upsert(databaseContext,
                                                out var newNftCreated,
                                                chainId,
                                                tokenId,
                                                null, // tokenUri
                                                contractId);

                                            Log.Verbose(
                                                "[{Name}] using NFT with internal Id {Id}, Token {Token}, newNFT {New}",
                                                Name, nft.ID, nft.TOKEN_ID, newNftCreated);
                                            if ( newNftCreated ) nftsInThisBlock.Add(tokenId, nft);
                                        }

                                        databaseEvent = EventMethods.Upsert(databaseContext,
                                            out var eventAdded,
                                            nft,
                                            timestampUnixSeconds,
                                            eventIndex + 1,
                                            chainId,
                                            transaction,
                                            contractId,
                                            eventKindId,
                                            null,
                                            null,
                                            null,
                                            null,
                                            0,
                                            infusionEventData.InfusedSymbol,
                                            infusionEventData.InfusedSymbol,
                                            infusionEventData.InfusedValue.ToString(),
                                            tokenId,
                                            addressString,
                                            null,
                                            false);

                                        if ( eventAdded ) eventsAddedCount++;

                                        added = eventAdded;
                                        _overallEventsLoadedCount++;

                                        //TODO just add it here for now to check if the model works
                                        //data will still be used from Events table for the Endpoint atm
                                        if ( databaseEvent != null )
                                        {
                                            var infusionEvent = InfusionEventMethods.Upsert(databaseContext,
                                                infusionEventData.TokenID.ToString(), infusionEventData.BaseSymbol,
                                                infusionEventData.InfusedSymbol,
                                                infusionEventData.InfusedValue.ToString(), chainId, databaseEvent);

                                            Log.Verbose("[{Name}] added InfusionEventData with internal Id {Id}",
                                                Name, infusionEvent.ID);
                                        }
                                    }

                                    break;
                                }
                                case EventKind.TokenMint or EventKind.TokenClaim or EventKind.TokenBurn
                                    or EventKind.TokenSend or EventKind.TokenReceive or EventKind.TokenStake
                                    or EventKind.CrownRewards:
                                {
                                    var tokenEventData = evnt.GetContent<TokenEventData>();

                                    Log.Verbose("[{Name}] getting TokenEventData for {Kind}, Chain {Chain}", Name,
                                        kind, tokenEventData.ChainName);

                                    bool fungible;
                                    if ( symbolFungible.ContainsKey(tokenEventData.Symbol) )
                                        fungible = symbolFungible.GetValueOrDefault(tokenEventData.Symbol);
                                    else
                                    {
                                        fungible = TokenMethods.Get(
                                            databaseContext, chainId,
                                            tokenEventData.Symbol).FUNGIBLE;
                                        symbolFungible.Add(tokenEventData.Symbol, fungible);
                                    }

                                    if ( !fungible )
                                    {
                                        Log.Debug(
                                            "[{Name}] New event on {TimestampUnixSeconds}: kind: {Kind} symbol: {Symbol} value: {Value} address: {AddressString} contract: {Contract}",
                                            Name, UnixSeconds.Log(timestampUnixSeconds), kind,
                                            tokenEventData.Symbol, tokenEventData.Value, addressString,
                                            contract);

                                        var contractId = ContractMethods.Upsert(databaseContext, contract,
                                            chainId,
                                            tokenEventData.Symbol.ToUpper(),
                                            tokenEventData.Symbol.ToUpper());

                                        var tokenId = tokenEventData.Value.ToString();

                                        Nft nft;
                                        if ( nftsInThisBlock.ContainsKey(tokenId) )
                                            nft = nftsInThisBlock.GetValueOrDefault(tokenId);
                                        else
                                        {
                                            nft = NftMethods.Upsert(databaseContext,
                                                out var newNftCreated,
                                                chainId,
                                                tokenId,
                                                null, // tokenUri
                                                contractId);
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


                                        databaseEvent = EventMethods.Upsert(databaseContext,
                                            out var eventAdded,
                                            nft, //might be null due not having an ntf
                                            timestampUnixSeconds,
                                            eventIndex + 1,
                                            chainId,
                                            transaction,
                                            contractId,
                                            eventKindId,
                                            null,
                                            null,
                                            null,
                                            null,
                                            0,
                                            null,
                                            null,
                                            null,
                                            tokenId,
                                            addressString,
                                            null,
                                            false);
                                        //kind != EventKind.TokenMint);

                                        //update ntf related things if it is not null
                                        if ( nft != null )
                                            // Update NFTs owner address on new event.
                                            NftMethods.ProcessOwnershipChange(databaseContext,
                                                chainId,
                                                nft,
                                                timestampUnixSeconds,
                                                addressString);

                                        if ( eventAdded ) eventsAddedCount++;

                                        added = eventAdded;
                                        _overallEventsLoadedCount++;

                                        //TODO just add it here for now to check if the model works
                                        //data will still be used from Events table for the Endpoint atm
                                        if ( databaseEvent != null )
                                        {
                                            var tokenEvent = TokenEventMethods.Upsert(databaseContext,
                                                tokenEventData.Symbol, tokenEventData.ChainName, tokenId, chainId,
                                                databaseEvent);

                                            Log.Verbose("[{Name}] added TokenEventData with internal Id {Id}",
                                                Name, tokenEvent.ID);
                                        }
                                    }

                                    break;
                                }
                                case EventKind.OrderCancelled or EventKind.OrderClosed
                                    or EventKind.OrderCreated or EventKind.OrderFilled or EventKind.OrderBid:
                                {
                                    Log.Verbose("[{Name}] getting MarketEventData for {Kind}", Name, kind);

                                    var marketEventData = evnt.GetContent<MarketEventData>();

                                    bool fungible;
                                    if ( symbolFungible.ContainsKey(marketEventData.BaseSymbol) )
                                        fungible = symbolFungible.GetValueOrDefault(marketEventData.BaseSymbol);
                                    else
                                    {
                                        fungible = TokenMethods.Get(
                                            databaseContext, chainId,
                                            marketEventData.BaseSymbol).FUNGIBLE;
                                        symbolFungible.Add(marketEventData.BaseSymbol, fungible);
                                    }


                                    Log.Debug(
                                        "[{Name}] New event on {TimestampUnixSeconds}: kind: {Kind} baseSymbol: {BaseSymbol} quoteSymbol: {QuoteSymbol} price: {Price} endPrice: {EndPrice} id: {ID} address: {AddressString} contract: {Contract} type: {Type}",
                                        Name, UnixSeconds.Log(timestampUnixSeconds), kind,
                                        marketEventData.BaseSymbol, marketEventData.QuoteSymbol,
                                        marketEventData.Price, marketEventData.EndPrice, marketEventData.ID,
                                        addressString, contract, marketEventData.Type);

                                    var contractId = ContractMethods.Upsert(databaseContext, contract,
                                        chainId,
                                        marketEventData.BaseSymbol.ToUpper(),
                                        marketEventData.BaseSymbol.ToUpper());

                                    var tokenId = marketEventData.ID.ToString();

                                    if ( !fungible )
                                    {
                                        string price;
                                        if ( kind == EventKind.OrderBid ||
                                             kind == EventKind.OrderFilled &&
                                             marketEventData.Type != TypeAuction.Fixed )
                                            price = marketEventData.EndPrice.ToString();
                                        else
                                            price = marketEventData.Price.ToString();

                                        Nft nft;
                                        // Searching for corresponding NFT.
                                        // If it's available, we will set up relation.
                                        // If not, we will create it first.
                                        if ( nftsInThisBlock.ContainsKey(tokenId) )
                                            nft = nftsInThisBlock.GetValueOrDefault(tokenId);
                                        else
                                        {
                                            nft = NftMethods.Upsert(databaseContext,
                                                out var newNftCreated,
                                                chainId,
                                                tokenId,
                                                null, // tokenUri
                                                contractId);
                                            Log.Verbose(
                                                "[{Name}] using NFT with internal Id {Id}, Token {Token}, newNFT {New}",
                                                Name, nft.ID, nft.TOKEN_ID, newNftCreated);
                                            if ( newNftCreated ) nftsInThisBlock.Add(tokenId, nft);
                                        }

                                        databaseEvent = EventMethods.Upsert(databaseContext,
                                            out var eventAdded,
                                            nft,
                                            timestampUnixSeconds,
                                            eventIndex + 1,
                                            chainId, //ChainId,
                                            transaction,
                                            contractId,
                                            eventKindId,
                                            null,
                                            marketEventData.QuoteSymbol,
                                            marketEventData.QuoteSymbol,
                                            price,
                                            0,
                                            null,
                                            null,
                                            null,
                                            tokenId,
                                            addressString,
                                            null,
                                            false);

                                        if ( kind == EventKind.OrderFilled )
                                            // Update NFTs owner address on new sale event.
                                            NftMethods.ProcessOwnershipChange(databaseContext,
                                                chainId,
                                                nft,
                                                timestampUnixSeconds,
                                                addressString);

                                        if ( eventAdded ) eventsAddedCount++;

                                        added = eventAdded;
                                        _overallEventsLoadedCount++;

                                        //databaseEvent we need it here, so check it
                                        //TODO just add it here for now to check if the model works
                                        //data will still be used from Events table for the Endpoint atm
                                        if ( databaseEvent != null )
                                        {
                                            var marketEvent = MarketEventMethods.Upsert(databaseContext,
                                                marketEventData.Type.ToString(), marketEventData.BaseSymbol,
                                                marketEventData.QuoteSymbol, price, marketEventData.EndPrice.ToString(),
                                                marketEventData.ID.ToString(), chainId, databaseEvent);

                                            Log.Verbose("[{Name}] added MarketEventData with internal Id {Id}",
                                                Name, marketEvent.ID);
                                        }
                                    }

                                    break;
                                }
                                case EventKind.ChainCreate or EventKind.TokenCreate or EventKind.ContractUpgrade
                                    or EventKind.AddressRegister or EventKind.ContractDeploy or EventKind.PlatformCreate
                                    or EventKind.OrganizationCreate:
                                {
                                    var stringData = evnt.GetContent<string>();

                                    Log.Verbose("[{Name}] getting string for {Kind}, string {String}", Name, kind,
                                        stringData);

                                    //databaseEvent we need it here, so check it
                                    if ( databaseEvent != null )
                                    {
                                        var stringEvent = StringEventMethods.Upsert(databaseContext, stringData,
                                            databaseEvent);
                                        Log.Verbose("[{Name}] added String with internal Id {Id}",
                                            Name, stringEvent.ID);
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
                                    if ( databaseEvent != null )
                                    {
                                        var saleEvent = SaleEventMethods.Upsert(databaseContext, saleKind, hash,
                                            chainId, databaseEvent);

                                        Log.Verbose("[{Name}] added SaleEvent with internal Id {Id}",
                                            Name, saleEvent.ID);
                                    }

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
                                    if ( databaseEvent != null )
                                    {
                                        var transactionSettleEvent = TransactionSettleEventMethods.Upsert(
                                            databaseContext, hash, platform, chain, databaseEvent);

                                        Log.Verbose("[{Name}] added TransactionSettleEvent with internal Id {Id}",
                                            Name, transactionSettleEvent.ID);
                                    }

                                    break;
                                }
                                case EventKind.ValidatorElect or EventKind.ValidatorPropose:
                                {
                                    var address = evnt.GetContent<Address>().ToString();

                                    Log.Verbose("[{Name}] getting Address for {Kind}, Address {Address}", Name,
                                        kind, address);

                                    //databaseEvent we need it here, so check it
                                    if ( databaseEvent != null )
                                    {
                                        var addressEvent = AddressEventMethods.Upsert(databaseContext,
                                            address, databaseEvent, chainId);

                                        Log.Verbose("[{Name}] added Address with internal Id {Id}",
                                            Name, addressEvent.ID);
                                    }

                                    break;
                                }
                                case EventKind.ValidatorSwitch:
                                {
                                    Log.Verbose("[{Name}] getting nothing for {Kind}", Name, kind);

                                    break;
                                }
                                case EventKind.ValueCreate or EventKind.ValueUpdate:
                                {
                                    var chainValueEventData =
                                        evnt.GetContent<ChainValueEventData>();

                                    var valueEventName = chainValueEventData.Name;
                                    var value = chainValueEventData.Value.ToString();

                                    Log.Verbose(
                                        "[{Name}] getting ChainValueEventData for {Kind}, Name {ValueEventName}, Value {Value}",
                                        Name, kind, valueEventName, value);

                                    //databaseEvent we need it here, so check it
                                    if ( databaseEvent != null )
                                    {
                                        var chainEvent = ChainEventMethods.Upsert(databaseContext, valueEventName,
                                            value, chainId, databaseEvent);

                                        Log.Verbose("[{Name}] added ChainValueEvent with internal Id {Id}",
                                            Name, chainEvent.ID);
                                    }

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
                                    if ( databaseEvent != null )
                                    {
                                        var gasEvent = GasEventMethods.Upsert(databaseContext, address, price, amount,
                                            databaseEvent, chainId);

                                        Log.Verbose("[{Name}] added GasEvent with internal Id {Id}",
                                            Name, gasEvent.ID);
                                    }


                                    break;
                                }
                                case EventKind.FileCreate or EventKind.FileDelete:
                                {
                                    var hash = evnt.GetContent<Hash>().ToString();

                                    Log.Verbose("[{Name}] getting Hash for {Kind}, Hash {Hash}", Name, kind, hash);

                                    //databaseEvent we need it here, so check it
                                    if ( databaseEvent != null )
                                    {
                                        var hashEvent = HashEventMethods.Upsert(databaseContext, hash, databaseEvent);

                                        Log.Verbose("[{Name}] added Hash with internal Id {Id}",
                                            Name, hashEvent.ID);
                                    }

                                    break;
                                }
                                case EventKind.OrganizationAdd or EventKind.OrganizationRemove:
                                {
                                    var organizationEventData =
                                        evnt.GetContent<OrganizationEventData>();

                                    var organization = organizationEventData.Organization;
                                    var memberAddress = organizationEventData.MemberAddress.ToString();

                                    Log.Verbose(
                                        "[{Name}] getting OrganizationEventData for {Kind}, Organization {Organization}, MemberAddress {Address}",
                                        Name, kind, organization, memberAddress);

                                    //databaseEvent we need it here, so check it
                                    if ( databaseEvent != null )
                                    {
                                        var organizationEvent = OrganizationEventMethods.Upsert(databaseContext,
                                            organization, memberAddress,
                                            databaseEvent, chainId);

                                        Log.Verbose("[{Name}] added OrganizationEvent with internal Id {Id}",
                                            Name, organizationEvent.ID);
                                    }

                                    break;
                                }
                                //TODO
                                case EventKind.Inflation or EventKind.Log or EventKind.LeaderboardCreate
                                    or EventKind.Custom or EventKind.AddressUnregister:
                                {
                                    Log.Verbose(
                                        "[{Name}] Currently not processing EventKind {Kind} in Block #{Block}",
                                        Name, kind, blockHeight);

                                    break;
                                }
                                default:
                                    Log.Warning(
                                        "[{Name}] Currently not processing EventKind {Kind} in Block #{Block}",
                                        Name, kind, blockHeight);
                                    break;
                            }

                            if ( !added )
                            {
                                var contractId = ContractMethods.Upsert(databaseContext, contract,
                                    chainId,
                                    evnt.Contract,
                                    null);

                                EventMethods.Upsert(databaseContext,
                                    out var eventAdded,
                                    null,
                                    timestampUnixSeconds,
                                    eventIndex + 1,
                                    chainId,
                                    transaction,
                                    contractId,
                                    eventKindId,
                                    null,
                                    null,
                                    null,
                                    null,
                                    0,
                                    null,
                                    null,
                                    null,
                                    null,
                                    addressString,
                                    null,
                                    false);

                                if ( eventAdded ) eventsAddedCount++;

                                _overallEventsLoadedCount++;
                            }
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
                                Log.Information(
                                    "[{Name}] eventNode data on exception: {Exception}", Name,
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

            databaseContext.SaveChanges();
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
