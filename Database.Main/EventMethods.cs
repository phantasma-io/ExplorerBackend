using GhostDevs.Commons;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Linq;

namespace Database.Main
{
    public static class EventMethods
    {
        // Checks if "Events" table has entry with given name,
        // and adds new entry, if there's no entry available.
        // Returns new or existing entry's Id.
        public static Event Upsert(MainDbContext databaseContext,
            out bool newEventCreated,
            Nft nft,
            Int64 timestampUnixSeconds,
            int index,
            int chainId,
            Transaction transaction,
            int contractId,
            int eventKindId,
            string contractAuctionId,
            string quoteContractHash,
            string quoteSymbol,
            string price,
            decimal priceUsd,
            string infusedContractHash,
            string infusedSymbol,
            string infusedValue,
            string tokenId,
            string address,
            string sourceAddress,
            bool hidden,
            int tokenAmount = 1)
        {
            newEventCreated = false;

            int? quoteSymbolId = null;
            if(!string.IsNullOrEmpty(quoteSymbol))
                quoteSymbolId = TokenMethods.Upsert(databaseContext, chainId, quoteContractHash, quoteSymbol);

            int? infusedSymbolId = null;
            if (!string.IsNullOrEmpty(infusedSymbol))
                infusedSymbolId = TokenMethods.Upsert(databaseContext, chainId, infusedContractHash, infusedSymbol);

            var addressEntry = AddressMethods.Upsert(databaseContext, chainId, address, false);
            Address sourceAddressEntry = null;
            if (!string.IsNullOrEmpty(sourceAddress))
                sourceAddressEntry = AddressMethods.Upsert(databaseContext, chainId, sourceAddress, false);

            var evnt = databaseContext.Events.Where(x => x.ChainId == chainId && x.Transaction == transaction && x.INDEX == index).FirstOrDefault();

            if (evnt == null)
            {
                evnt = new Event
                {
                    DM_UNIX_SECONDS = UnixSeconds.Now(),
                    TIMESTAMP_UNIX_SECONDS = timestampUnixSeconds,
                    DATE_UNIX_SECONDS = UnixSeconds.GetDate(timestampUnixSeconds),
                    INDEX = index,
                    ChainId = chainId,
                    Transaction = transaction,
                    ContractId = contractId,
                    EventKindId = eventKindId,
                    QuoteSymbolId = quoteSymbolId,
                    PRICE = price,
                    PRICE_USD = priceUsd,
                    InfusedSymbolId = infusedSymbolId,
                    INFUSED_VALUE = infusedValue,
                    TOKEN_ID = tokenId,
                    Address = addressEntry,
                    SourceAddress = sourceAddressEntry,
                    Nft = nft,
                    HIDDEN = hidden,
                    TOKEN_AMOUNT = tokenAmount
                };

                databaseContext.Events.Add(evnt);

                newEventCreated = true;
            }
            else
            {
                evnt.DM_UNIX_SECONDS = UnixSeconds.Now();
                evnt.TIMESTAMP_UNIX_SECONDS = timestampUnixSeconds;
                evnt.DATE_UNIX_SECONDS = UnixSeconds.GetDate(timestampUnixSeconds);
                evnt.INDEX = index;
                evnt.ChainId = chainId;
                evnt.Transaction = transaction;
                evnt.ContractId = contractId;
                evnt.EventKindId = eventKindId;
                evnt.QuoteSymbolId = quoteSymbolId;
                evnt.PRICE = price;
                evnt.PRICE_USD = priceUsd;
                evnt.InfusedSymbolId = infusedSymbolId;
                evnt.INFUSED_VALUE = infusedValue;
                evnt.TOKEN_ID = tokenId;
                evnt.Address = addressEntry;
                evnt.SourceAddress = sourceAddressEntry;
                evnt.Nft = nft;
                evnt.HIDDEN = hidden;
                evnt.TOKEN_AMOUNT = tokenAmount;
            }

            evnt.CONTRACT_AUCTION_ID = contractAuctionId;

            if (!string.IsNullOrEmpty(infusedSymbol) && TokenMethods.Get(databaseContext, chainId, infusedSymbol).FUNGIBLE == false)
            {
                // NFT was infused, we should mark it as infused.
                var infusedNft = databaseContext.Nfts.Where(x => x.TOKEN_ID == infusedValue).First();
                infusedNft.InfusedInto = nft;

                Log.Information($"NFT infused: {infusedNft.TOKEN_ID} into NFT {nft.TOKEN_ID}");
            }

            var burnEvent = databaseContext.EventKinds.Where(x => x.NAME == "TokenBurn" && x.ChainId == chainId).FirstOrDefault();
            if (burnEvent != null && eventKindId == burnEvent.ID)
            {
                // For burns we must release all infused nfts.
                var infusedNftIDs = databaseContext.Nfts.Where(x => x.InfusedInto == nft).Select(x => x.TOKEN_ID).ToArray();
                foreach(var id in infusedNftIDs)
                {
                    var defusedNft = databaseContext.Nfts.Where(x => x.TOKEN_ID == id).First();
                    defusedNft.InfusedInto = null;

                    Log.Information($"NFT defused: {defusedNft.TOKEN_ID} from NFT {nft.TOKEN_ID}");
                }
            }

            return evnt;
        }
        public static void UpdateOnEventMerge(MainDbContext databaseContext, int id, int eventKindId, int sourceAddressId, bool hidden)
        {
            var evnt = databaseContext.Events.Where(x => x.ID == id).Single();

            evnt.EventKindId = eventKindId;
            evnt.SourceAddressId = sourceAddressId;
            evnt.HIDDEN = hidden;

            evnt.DM_UNIX_SECONDS = UnixSeconds.Now();
        }
        public static void DeleteByNftId(MainDbContext databaseContext, int nftId, bool saveChanges = true)
        {
            var tokenEvents = databaseContext.Events.Where(x => x.NftId == nftId);
            foreach (var tokenEvent in tokenEvents)
            {
                databaseContext.Entry(tokenEvent).State = EntityState.Deleted;
            }

            if (saveChanges)
                databaseContext.SaveChanges();
        }
    }
}
