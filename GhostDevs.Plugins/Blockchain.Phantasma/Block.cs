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
using BlockMethods = Database.Main.BlockMethods;
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
    private static readonly int maxEventsForOneSession = 1000;
    private static int OverallEventsLoadedCount;


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

            OverallEventsLoadedCount = 0;
            while ( FetchByHeight(i, chainId, chainName) && OverallEventsLoadedCount < maxEventsForOneSession ) i++;

            var fetchTime = DateTime.Now - startTime;
            Log.Information(
                "[{Name}] Events load took {FetchTime} sec, {OverallEventsLoadedCount} events added",
                Name, Math.Round(fetchTime.TotalSeconds, 3), OverallEventsLoadedCount);
        } while ( OverallEventsLoadedCount > 0 );
    }


    private bool FetchByHeight(BigInteger blockHeight, int chainId, string chainName)
    {
        //TODO add check if we can get block from apicache
        var startTime = DateTime.Now;

        var url = $"{Settings.Default.GetRest()}/api/getBlockByHeight?chainInput={chainName}&height={blockHeight}";

        var response = Client.APIRequest<JsonDocument>(url, out var stringResponse, null, 10);
        if ( response == null ) return false;

        var downloadTime = DateTime.Now - startTime;

        startTime = DateTime.Now;
        var eventsAddedCount = 0;

        var error = "";
        if ( response.RootElement.TryGetProperty("error", out var errorProperty) ) error = errorProperty.GetString();

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

        Log.Information("[{Name}] Block #{BlockHeight}: {@Response}", Name, blockHeight, response);

        // We cache NFTs in this block to speed up code
        // and avoid some unpleasant situations leading to bugs.
        Dictionary<string, Nft> nftsInThisBlock = new();

        using MainDbContext databaseContext = new();
        try
        {
            var timestampUnixSeconds = response.RootElement.GetProperty("timestamp").GetUInt32();

            // Block in main database
            var block = BlockMethods.Upsert(databaseContext, chainId, blockHeight, timestampUnixSeconds, false);

            // Block in caching database
            // Currently only stored. TODO add reuse to speed up resync
            using ( ApiCacheDbContext databaseApiCacheContext = new() )
            {
                Database.ApiCache.BlockMethods.Upsert(databaseApiCacheContext,
                    chainName, blockHeight.ToString(), timestampUnixSeconds, response);
            }

            if ( response.RootElement.TryGetProperty("txs", out var txsProperty) )
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

                    // Current transaction.
                    var transaction = TransactionMethods.Upsert(databaseContext, block, txIndex,
                        tx.GetProperty("hash").GetString(), false);

                    for ( var eventIndex = 0; eventIndex < events.Count(); eventIndex++ )
                    {
                        var eventNode = events.ElementAt(eventIndex);

                        try
                        {
                            var kindSerialized = eventNode.GetProperty("kind").GetString();
                            var kind = Enum.Parse<EventKind>(kindSerialized);
                            //just works if we process every event, otherwise we would have data in the database we do not really need
                            var eventKindId = EventKindMethods.Upsert(databaseContext, chainId,
                                kind.ToString());

                            Log.Verbose("[{Name}] Processing EventKind {Kind} in Block #{Block}", Name, kind,
                                blockHeight);

                            var contract = eventNode.GetProperty("contract").GetString();
                            var addressString = eventNode.GetProperty("address").GetString();
                            var addr = Address.FromText(addressString);
                            var data = eventNode.GetProperty("data").GetString().Decode();

                            Event evnt = new(kind, addr, contract, data);

                            var added = false;

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

                                    /*if (infusionEventData.ChainName == "main" &&
                                            Settings.Default.NFTs.Any(x =>
                                                x.Symbol == infusionEventData.BaseSymbol))
                                        {*/

                                    if ( infusionEventData.ChainName == "main" )
                                    {
                                        var contractId = ContractMethods.Upsert(databaseContext, contract,
                                            chainId,
                                            infusionEventData.BaseSymbol.ToUpper(),
                                            infusionEventData.BaseSymbol.ToUpper());

                                        var tokenId = infusionEventData.TokenID.ToString();

                                        var nftConfig = Settings.Default.NFTs
                                            .FirstOrDefault(x => string.Equals(x.Symbol.ToUpper(),
                                                infusionEventData.BaseSymbol.ToUpper()));

                                        if ( nftConfig != null )
                                        {
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

                                                if ( newNftCreated ) nftsInThisBlock.Add(tokenId, nft);
                                            }

                                            EventMethods.Upsert(databaseContext,
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
                                            OverallEventsLoadedCount++;
                                        }
                                    }

                                    break;
                                }
                                /*case EventKind.TokenMint or EventKind.TokenClaim or EventKind.TokenBurn
                                    or EventKind.TokenSend or EventKind.TokenReceive:*/
                                case EventKind.TokenMint or EventKind.TokenClaim or EventKind.TokenBurn
                                    or EventKind.TokenSend or EventKind.TokenReceive or EventKind.TokenStake
                                    or EventKind.CrownRewards:
                                {
                                    var tokenEventData = evnt.GetContent<TokenEventData>();

                                    Log.Verbose("[{Name}] getting TokenEventData for {Kind}, Chain {Chain}", Name,
                                        kind, tokenEventData.ChainName);

                                    //currently checks if the value is configured in json
                                    //change to if we have it in the database, if not create it with the info we have
                                    //if we have that we can also re move the configurations

                                    /*if (tokenEventData.ChainName == "main" && Settings.Default.NFTs.Any(x =>
                                                string.Equals(x.Symbol, tokenEventData.Symbol,
                                                    StringComparison.InvariantCultureIgnoreCase)))*/

                                    if ( tokenEventData.ChainName == "main" )
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

                                        //fix that, we want to get rid of that configuration
                                        var nftConfig = Settings.Default.NFTs.FirstOrDefault(x =>
                                            string.Equals(x.Symbol.ToUpper(),
                                                tokenEventData.Symbol.ToUpper()));

                                        //if we have an ntf, create everything we need for it
                                        Nft nft = null;
                                        if ( nftConfig != null )
                                        {
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

                                                if ( newNftCreated ) nftsInThisBlock.Add(tokenId, nft);
                                            }

                                            // We should always properly check mint event and update mint date,
                                            // because nft can be created by auction thread without mint date,
                                            // so we can't update dates only for just created NFTs using newNftCreated flag.
                                            if ( kind == EventKind.TokenMint )
                                                nft.MINT_DATE_UNIX_SECONDS = timestampUnixSeconds;
                                        }

                                        EventMethods.Upsert(databaseContext,
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
                                            // Update NFT's owner address on new event.
                                            NftMethods.ProcessOwnershipChange(databaseContext,
                                                chainId,
                                                nft,
                                                timestampUnixSeconds,
                                                addressString);

                                        if ( eventAdded ) eventsAddedCount++;

                                        added = eventAdded;
                                        OverallEventsLoadedCount++;

                                        //for the backend we do not only need nfts, we need everything
                                        //in the explorer we do now
                                        /*if (nftConfig != null)
                                        {
                                            Nft nft;
                                            // Searching for corresponding NFT.
                                            // If it's available, we will set up relation.
                                            // If not, we will create it first.
                                            if (nftsInThisBlock.ContainsKey(tokenId))
                                            {
                                                nft = nftsInThisBlock.GetValueOrDefault(tokenId);
                                            }
                                            else
                                            {
                                                nft = NftMethods.Upsert(databaseContext,
                                                    out var newNftCreated,
                                                    ChainId,
                                                    tokenId,
                                                    null, // tokenUri
                                                    contractId);

                                                if (newNftCreated)
                                                {
                                                    nftsInThisBlock.Add(tokenId, nft);
                                                }
                                            }

                                            // We should always properly check mint event and update mint date,
                                            // because nft can be created by auction thread without mint date,
                                            // so we can't update dates only for just created NFTs using newNftCreated flag.
                                            if (kind == EventKind.TokenMint)
                                            {
                                                nft.MINT_DATE_UNIX_SECONDS = timestampUnixSeconds;
                                            }

                                            EventMethods.Upsert(databaseContext,
                                                out var eventAdded,
                                                nft,
                                                timestampUnixSeconds,
                                                eventIndex + 1,
                                                ChainId,
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
                                                kind != EventKind.TokenMint);

                                            // Update NFT's owner address on new event.
                                            NftMethods.ProcessOwnershipChange(databaseContext,
                                                ChainId,
                                                nft,
                                                timestampUnixSeconds,
                                                addressString);

                                            if (eventAdded)
                                            {
                                                eventsAddedCount++;
                                            }

                                            OverallEventsLoadedCount++;
                                        }*/
                                    }
                                    /*else if (kind is EventKind.TokenSend or EventKind.TokenReceive)
                                    {
                                    }*/
                                    /*else
                                    {
                                        Log.Warning(
                                            "[{Name}] New UNREGISTERED event on {TimestampUnixSeconds}: kind: {Kind} symbol: {Symbol} value: {Value} address: {AddressString} contract: {Contract}",
                                            Name, UnixSeconds.Log(timestampUnixSeconds), kind,
                                            tokenEventData.Symbol, tokenEventData.Value, addressString,
                                            contract);
                                    }*/

                                    break;
                                }
                                case EventKind.OrderCancelled or EventKind.OrderClosed
                                    or EventKind.OrderCreated or EventKind.OrderFilled or EventKind.OrderBid:
                                {
                                    Log.Verbose("[{Name}] getting MarketEventData for {Kind}", Name, kind);

                                    var marketEventData = evnt.GetContent<MarketEventData>();

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

                                    var nftConfig = Settings.Default.NFTs
                                        .FirstOrDefault(x => string.Equals(x.Symbol.ToUpper(),
                                            marketEventData.BaseSymbol.ToUpper()));

                                    if ( nftConfig != null )
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

                                            if ( newNftCreated ) nftsInThisBlock.Add(tokenId, nft);
                                        }

                                        EventMethods.Upsert(databaseContext,
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
                                            // Update NFT's owner address on new sale event.
                                            NftMethods.ProcessOwnershipChange(databaseContext,
                                                chainId,
                                                nft,
                                                timestampUnixSeconds,
                                                addressString);

                                        if ( eventAdded ) eventsAddedCount++;

                                        added = eventAdded;
                                        OverallEventsLoadedCount++;
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

                                    break;
                                }
                                case EventKind.Crowdsale:
                                {
                                    var saleEventData = evnt.GetContent<SaleEventData>();

                                    var hash = saleEventData.saleHash;
                                    var saleKind = saleEventData.kind; //handle sale kinds 

                                    Log.Verbose(
                                        "[{Name}] getting SaleEventData for {Kind}, hash {Hash}, saleKind {SaleKind}",
                                        Name, kind, hash.ToString(), saleKind);


                                    break;
                                }
                                case EventKind.ChainSwap:
                                {
                                    var transactionSettleEventData =
                                        evnt.GetContent<TransactionSettleEventData>();

                                    var hash = transactionSettleEventData.Hash;
                                    var platform = transactionSettleEventData.Platform;
                                    var chain = transactionSettleEventData.Chain;

                                    Log.Verbose(
                                        "[{Name}] getting TransactionSettleEventData for {Kind}, hash {Hash}, platform {Platform}, chain {Chain}",
                                        Name, kind, hash.ToString(), platform, chain);

                                    break;
                                }
                                case EventKind.ValidatorElect or EventKind.ValidatorPropose:
                                {
                                    var address = evnt.GetContent<Address>();

                                    Log.Verbose("[{Name}] getting Address for {Kind}, Address {Address}", Name,
                                        kind, address.ToString());

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
                                    var value = chainValueEventData.Value;

                                    Log.Verbose(
                                        "[{Name}] getting ChainValueEventData for {Kind}, Name {ValueEventName}, Value {Value}",
                                        Name, kind, valueEventName, value);

                                    break;
                                }
                                case EventKind.GasEscrow or EventKind.GasPayment:
                                {
                                    var gasEventData = evnt.GetContent<GasEventData>();

                                    var address = gasEventData.address;
                                    var price = gasEventData.price;
                                    var amount = gasEventData.amount;

                                    Log.Verbose(
                                        "[{Name}] getting GasEventData for {Kind}, Address {Address}, price {Price}, amount {Amount}",
                                        Name, kind, address.ToString(), price, amount);

                                    break;
                                }
                                case EventKind.FileCreate or EventKind.FileDelete:
                                {
                                    var hash = evnt.GetContent<Hash>();

                                    Log.Verbose("[{Name}] getting Hash for {Kind}, Hash {Hash}", Name, kind, hash);

                                    break;
                                }
                                case EventKind.OrganizationAdd or EventKind.OrganizationRemove:
                                {
                                    var organizationEventData =
                                        evnt.GetContent<OrganizationEventData>();

                                    var organization = organizationEventData.Organization;
                                    var memberAddress = organizationEventData.MemberAddress;

                                    Log.Verbose(
                                        "[{Name}] getting OrganizationEventData for {Kind}, Organization {Organization}, MemberAddress {Address}",
                                        Name, kind, organization, memberAddress.ToString());

                                    break;
                                }
                                //TODO
                                case EventKind.Inflation or EventKind.Log or EventKind.LeaderboardCreate
                                    or EventKind.Custom:
                                {
                                    Log.Verbose(
                                        "[{Name}] Currently not processing EventKind {Kind} in Block #{Block}",
                                        Name, kind,
                                        blockHeight);

                                    break;
                                }
                                default:
                                    Log.Warning(
                                        "[{Name}] Currently not processing EventKind {Kind} in Block #{Block}",
                                        Name, kind,
                                        blockHeight);
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

                                OverallEventsLoadedCount++;
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
