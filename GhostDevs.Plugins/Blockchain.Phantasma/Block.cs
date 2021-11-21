using GhostDevs.Api;
using GhostDevs.Commons;
using Database.Main;
using GhostDevs.PluginEngine;
using Phantasma.Domain;
using Phantasma.Numerics;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace GhostDevs.Blockchain
{
    public partial class PhantasmaPlugin: Plugin, IBlockchainPlugin
    {
        // When we reach this number of new loaded events, we report it in log.
        private static readonly int maxEventsForOneSession = 1000;
        private static int OverallEventsLoadedCount;
        public void FetchBlocks()
        {
            do
            {
                DateTime startTime = DateTime.Now;

                System.Numerics.BigInteger i = 1;
                using (var databaseContext = new MainDatabaseContext())
                {
                    i = ChainMethods.GetLastProcessedBlock(databaseContext, ChainId) + 1;
                }

                if (i == 1)
                {
                    // 16664: First CROWN NFT minted on Phantasma blockchain at 2019-12-30 19:11:09.
                    // 21393: First NFT minted on Phantasma blockchain at 2020-02-14 16:03:50.
                    // 31172: First NFT traded on Phantasma blockchain at 2020-02-28 12:15:51.
                    i = Settings.Default.FirstBlock;
                }

                OverallEventsLoadedCount = 0;
                while (FetchByHeight(i) && OverallEventsLoadedCount < maxEventsForOneSession)
                {
                    i++;
                }

                TimeSpan fetchTime = DateTime.Now - startTime;
                Log.Information($"[{Name}] Events load took {Math.Round(fetchTime.TotalSeconds, 3)} sec, {OverallEventsLoadedCount} events added");
            }
            while (OverallEventsLoadedCount > 0);
        }
        public bool FetchByHeight(System.Numerics.BigInteger blockHeight)
        {
            DateTime startTime = DateTime.Now;

            var url = $"{Settings.Default.GetRest()}/api/getBlockByHeight?chainInput=main&height={blockHeight}";

            var response = Client.APIRequest<JsonDocument>(url, out var stringResponse, null, 10);
            if (response == null)
            {
                return false;
            }

            TimeSpan downloadTime = DateTime.Now - startTime;

            startTime = DateTime.Now;
            var eventsAddedCount = 0;

            var error = "";
            if (response.RootElement.TryGetProperty("error", out var errorProperty))
                error = errorProperty.GetString();
            if (error == "block not found")
            {
                Log.Debug($"[{Name}] getBlockByHeight(): Block {blockHeight} not found");
                return false;
            }
            else if (!string.IsNullOrEmpty(error))
            {
                Log.Error($"[{Name}] getBlockByHeight() error: " + error);
                return false;
            }

            Log.Information("[{Name}] Block #{blockHeight}: {@response}", Name, blockHeight, response);

            // We cache NFTs in this block to speed up code
            // and avoid some unpleasant situations leading to bugs.
            var nftsInThisBlock = new Dictionary<string, Nft>();

            using (var databaseContext = new MainDatabaseContext())
            {
                try
                {
                    var timestampUnixSeconds = response.RootElement.GetProperty("timestamp").GetUInt32();

                    // Block in main database
                    var block = BlockMethods.Upsert(databaseContext, ChainId, blockHeight, timestampUnixSeconds, false);

                    // Block in caching database
                    // Currently only stored. TODO add reuse to speed up resync
                    using (var databaseApiCacheContext = new Database.ApiCache.ApiCacheDatabaseContext())
                    {
                        Database.ApiCache.BlockMethods.Upsert(databaseApiCacheContext,
                            "main", blockHeight.ToString(), timestampUnixSeconds, response, true);
                    }

                    if (response.RootElement.TryGetProperty("txs", out var txsProperty))
                    {
                        var txs = txsProperty.EnumerateArray();
                        for (var txIndex = 0; txIndex < txs.Count(); txIndex++)
                        {
                            var tx = txs.ElementAt(txIndex);

                            var events = new System.Text.Json.JsonElement.ArrayEnumerator();
                            if (tx.TryGetProperty("events", out var eventsProperty))
                            {
                                events = eventsProperty.EnumerateArray();
                                Log.Debug("[{Name}] Events for tx #{txIndex} in block #{blockHeight}: {@eventsProperty}", Name, txIndex, blockHeight, eventsProperty);
                            }
                            else
                            {
                                Log.Debug("[{Name}] No events for tx #{txIndex} in block #{blockHeight}", Name, txIndex, blockHeight);
                            }

                            // Current transaction.
                            var transaction = TransactionMethods.Upsert(databaseContext, block, txIndex, tx.GetProperty("hash").GetString(), false);

                            for (var eventIndex = 0; eventIndex < events.Count(); eventIndex++)
                            {
                                var eventNode = events.ElementAt(eventIndex);

                                try
                                {
                                    var kindSerialized = eventNode.GetProperty("kind").GetString();
                                    var kind = Enum.Parse<Phantasma.Domain.EventKind>(kindSerialized);

                                    if (kind == Phantasma.Domain.EventKind.TokenMint ||
                                        kind == Phantasma.Domain.EventKind.TokenClaim ||
                                        kind == Phantasma.Domain.EventKind.TokenBurn ||
                                        kind == Phantasma.Domain.EventKind.TokenSend ||
                                        kind == Phantasma.Domain.EventKind.TokenReceive ||
                                        kind == Phantasma.Domain.EventKind.OrderCancelled ||
                                        kind == Phantasma.Domain.EventKind.OrderClosed ||
                                        kind == Phantasma.Domain.EventKind.OrderCreated ||
                                        kind == Phantasma.Domain.EventKind.OrderFilled ||
                                        kind == Phantasma.Domain.EventKind.OrderBid ||
                                        kind == Phantasma.Domain.EventKind.Infusion)
                                    {
                                        var contract = eventNode.GetProperty("contract").GetString();
                                        var addressString = eventNode.GetProperty("address").GetString();
                                        var addr = Phantasma.Cryptography.Address.FromText(addressString);
                                        var data = Base16.Decode(eventNode.GetProperty("data").GetString());

                                        if (kind == Phantasma.Domain.EventKind.Infusion)
                                        {
                                            var evnt = new Phantasma.Domain.Event(kind, addr, contract, data);

                                            var infusionEventData = evnt.GetContent<InfusionEventData>();

                                            Log.Debug($"[{Name}] New infusion into NFT {infusionEventData.TokenID} {infusionEventData.BaseSymbol}, infused {infusionEventData.InfusedValue} {infusionEventData.InfusedSymbol}, address: {addressString} contract: {contract}");

                                            if (infusionEventData.ChainName == "main" && Settings.Default.NFTs.Any(x => x.Symbol == infusionEventData.BaseSymbol))
                                            {
                                                var eventKindId = EventKindMethods.Upsert(databaseContext, ChainId, kind.ToString());
                                                var contractId = ContractMethods.Upsert(databaseContext, contract, ChainId, infusionEventData.BaseSymbol.ToUpper(), infusionEventData.BaseSymbol.ToUpper());

                                                var tokenId = infusionEventData.TokenID.ToString();

                                                var nftConfig = Settings.Default.NFTs.Where(x => x.Symbol.ToUpper() == infusionEventData.BaseSymbol.ToUpper()).FirstOrDefault();

                                                if (nftConfig != null)
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
                                                        infusionEventData.InfusedSymbol,
                                                        infusionEventData.InfusedSymbol,
                                                        infusionEventData.InfusedValue.ToString(),
                                                        tokenId,
                                                        addressString,
                                                        null,
                                                        false);

                                                    if(eventAdded)
                                                        eventsAddedCount++;
                                                    OverallEventsLoadedCount++;
                                                }
                                            }
                                        }
                                        else if (kind == Phantasma.Domain.EventKind.TokenMint ||
                                            kind == Phantasma.Domain.EventKind.TokenClaim ||
                                            kind == Phantasma.Domain.EventKind.TokenBurn ||
                                            kind == Phantasma.Domain.EventKind.TokenSend ||
                                            kind == Phantasma.Domain.EventKind.TokenReceive)
                                        {
                                            var evnt = new Phantasma.Domain.Event(kind, addr, contract, data);

                                            var tokenEventData = evnt.GetContent<TokenEventData>();

                                            if (tokenEventData.ChainName == "main" && Settings.Default.NFTs.Any(x => x.Symbol.ToUpperInvariant() == tokenEventData.Symbol.ToUpperInvariant()))
                                            {
                                                Log.Debug($"[{Name}] New event on {UnixSeconds.Log(timestampUnixSeconds)}: kind: {kind} symbol: {tokenEventData.Symbol} value: {tokenEventData.Value} address: {addressString} contract: {contract}");

                                                var eventKindId = EventKindMethods.Upsert(databaseContext, ChainId, kind.ToString());
                                                var contractId = ContractMethods.Upsert(databaseContext, contract, ChainId, tokenEventData.Symbol.ToUpper(), tokenEventData.Symbol.ToUpper());

                                                var tokenId = tokenEventData.Value.ToString();

                                                var nftConfig = Settings.Default.NFTs.Where(x => x.Symbol.ToUpper() == tokenEventData.Symbol.ToUpper()).FirstOrDefault();

                                                if (nftConfig != null)
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
                                                    if (kind == Phantasma.Domain.EventKind.TokenMint)
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
                                                        kind != Phantasma.Domain.EventKind.TokenMint);

                                                    // Update NFT's owner address on new event.
                                                    NftMethods.ProcessOwnershipChange(databaseContext,
                                                        ChainId,
                                                        nft,
                                                        timestampUnixSeconds,
                                                        addressString);

                                                    if (eventAdded)
                                                        eventsAddedCount++;
                                                    OverallEventsLoadedCount++;
                                                }
                                            }
                                            else if(kind == Phantasma.Domain.EventKind.TokenSend || kind == Phantasma.Domain.EventKind.TokenReceive)
                                            {
                                            }
                                            else
                                            {
                                                Log.Warning($"[{Name}] New UNREGISTERED event on {UnixSeconds.Log(timestampUnixSeconds)}: kind: {kind} symbol: {tokenEventData.Symbol} value: {tokenEventData.Value} address: {addressString} contract: {contract}");
                                            }
                                        }
                                        else if (kind == Phantasma.Domain.EventKind.OrderCancelled ||
                                            kind == Phantasma.Domain.EventKind.OrderClosed ||
                                            kind == Phantasma.Domain.EventKind.OrderCreated ||
                                            kind == Phantasma.Domain.EventKind.OrderFilled ||
                                            kind == Phantasma.Domain.EventKind.OrderBid)
                                        {
                                            var evnt = new Phantasma.Domain.Event(kind, addr, contract, data);

                                            var marketEventData = evnt.GetContent<MarketEventData>();

                                            Log.Debug($"[{Name}] New event on {UnixSeconds.Log(timestampUnixSeconds)}: kind: {kind} baseSymbol: {marketEventData.BaseSymbol} quoteSymbol: {marketEventData.QuoteSymbol} price: {marketEventData.Price} endPrice: {marketEventData.EndPrice} id: {marketEventData.ID} address: {addressString} contract: {contract} type: {marketEventData.Type}");

                                            var eventKindId = EventKindMethods.Upsert(databaseContext, ChainId, kind.ToString());
                                            var contractId = ContractMethods.Upsert(databaseContext, contract, ChainId, marketEventData.BaseSymbol.ToUpper(), marketEventData.BaseSymbol.ToUpper());

                                            var tokenId = marketEventData.ID.ToString();

                                            var nftConfig = Settings.Default.NFTs.Where(x => x.Symbol.ToUpper() == marketEventData.BaseSymbol.ToUpper()).FirstOrDefault();
                                            
                                            if (nftConfig != null)
                                            {
                                                string price;
                                                if (kind == Phantasma.Domain.EventKind.OrderBid ||
                                                    (kind == Phantasma.Domain.EventKind.OrderFilled && marketEventData.Type != TypeAuction.Fixed))
                                                    price = marketEventData.EndPrice.ToString();
                                                else
                                                    price = marketEventData.Price.ToString();

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

                                                if(kind == Phantasma.Domain.EventKind.OrderFilled)
                                                {
                                                    // Update NFT's owner address on new sale event.
                                                    NftMethods.ProcessOwnershipChange(databaseContext,
                                                        ChainId,
                                                        nft,
                                                        timestampUnixSeconds,
                                                        addressString);
                                                }

                                                if(eventAdded)
                                                    eventsAddedCount++;
                                                OverallEventsLoadedCount++;
                                            }
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    Log.Error(e, $"[{Name}] {UnixSeconds.Log(timestampUnixSeconds)} event processing");

                                    try
                                    {
                                        Log.Information("[{Name}] eventNode on exception: {@eventNode}", Name, eventNode);
                                    }
                                    catch (Exception e2)
                                    {
                                        Log.Information($"[{Name}] Cannot print eventNode: {e2.Message}");
                                    }
                                    try
                                    {
                                        Log.Information($"[{Name}] eventNode data on exception: {Base16.Decode(eventNode.GetProperty("data").GetString())}");
                                    }
                                    catch (Exception e2)
                                    {
                                        Log.Information($"[{Name}] Cannot print eventNode data: {e2.Message}");
                                    }
                                }
                            }
                        }
                    }

                    ChainMethods.SetLastProcessedBlock(databaseContext, ChainId, blockHeight, false);

                    databaseContext.SaveChanges();
                }
                catch (Exception ex)
                {
                    ExceptionDispatchInfo.Capture(ex).Throw();
                }
            }

            TimeSpan processingTime = DateTime.Now - startTime;
            Log.Information($"[{Name}] Block {blockHeight} loaded in {Math.Round(downloadTime.TotalSeconds, 3)} sec, processed in {Math.Round(processingTime.TotalSeconds, 3)} sec, {eventsAddedCount} events added, {nftsInThisBlock.Count} NFTs processed");

            return true;
        }
    }
}
