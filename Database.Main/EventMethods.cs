using System.Linq;
using GhostDevs.Commons;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Database.Main;

public static class EventMethods
{
    // Checks if "Events" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    public static Event Upsert(MainDbContext databaseContext,
        out bool newEventCreated,
        long timestampUnixSeconds,
        int index,
        int chainId,
        Transaction transaction,
        int contractId,
        int eventKindId,
        string address,
        int tokenAmount = 1)
    {
        newEventCreated = false;

        var addressEntry = AddressMethods.Upsert(databaseContext, chainId, address, false);

        var evnt = databaseContext.Events.FirstOrDefault(x =>
            x.ChainId == chainId && x.Transaction == transaction && x.INDEX == index);

        if ( evnt == null )
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
                Address = addressEntry,
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
            evnt.Address = addressEntry;
            evnt.TOKEN_AMOUNT = tokenAmount;
        }

        return evnt;
    }


    public static void UpdateOnEventMerge(MainDbContext databaseContext, int id, int eventKindId, int sourceAddressId,
        bool hidden)
    {
        var evnt = databaseContext.Events.Single(x => x.ID == id);

        evnt.EventKindId = eventKindId;
        evnt.SourceAddressId = sourceAddressId;
        evnt.HIDDEN = hidden;

        evnt.DM_UNIX_SECONDS = UnixSeconds.Now();
    }


    public static void DeleteByNftId(MainDbContext databaseContext, int nftId, bool saveChanges = true)
    {
        var tokenEvents = databaseContext.Events.Where(x => x.NftId == nftId);
        foreach ( var tokenEvent in tokenEvents ) databaseContext.Entry(tokenEvent).State = EntityState.Deleted;

        if ( saveChanges ) databaseContext.SaveChanges();
    }


    public static Event UpdateValues(MainDbContext databaseContext, out bool eventUpdated, Event eventItem, Nft nft,
        string contractAuctionId, string quoteContractHash, string quoteSymbol, string price, decimal priceUsd,
        string infusedContractHash, string infusedSymbol, string infusedValue, string tokenId, string sourceAddress,
        int chainId, int eventKindId, int contractId, int tokenAmount = 1)
    {
        eventUpdated = false;
        //just to check

        var quoteSymbolId = GetTokenId(databaseContext, chainId, quoteSymbol);
        var infusedSymbolId = GetTokenId(databaseContext, chainId, infusedSymbol);
        var sourceAddressEntry = GetSourceAddress(databaseContext, sourceAddress, chainId, false);

        if ( eventItem == null ) return null;

        eventItem.ChainId = chainId;
        eventItem.ContractId = contractId;
        eventItem.EventKindId = eventKindId;
        eventItem.QuoteSymbolId = quoteSymbolId;
        eventItem.PRICE = price;
        eventItem.PRICE_USD = priceUsd;
        eventItem.InfusedSymbolId = infusedSymbolId;
        eventItem.INFUSED_VALUE = infusedValue;
        eventItem.TOKEN_ID = tokenId;
        eventItem.SourceAddress = sourceAddressEntry;
        eventItem.Nft = nft;
        eventItem.CONTRACT_AUCTION_ID = contractAuctionId;
        eventItem.TOKEN_AMOUNT = tokenAmount;

        MarkNtfInfused(databaseContext, chainId, infusedSymbol, infusedValue, nft);

        var burnEvent = databaseContext.EventKinds
            .FirstOrDefault(x => x.NAME == "TokenBurn" && x.ChainId == chainId);
        if ( burnEvent == null || eventKindId != burnEvent.ID ) return eventItem;

        //TODO check if always needed
        // For burns we must release all infused nfts.
        ProcessBurnedNft(databaseContext, nft);

        return eventItem;
    }


    private static Address GetSourceAddress(MainDbContext databaseContext, string sourceAddress, int chainId,
        bool saveChanges)
    {
        Address sourceAddressEntry = null;
        if ( !string.IsNullOrEmpty(sourceAddress) )
            sourceAddressEntry = AddressMethods.Upsert(databaseContext, chainId, sourceAddress, saveChanges);
        return sourceAddressEntry;
    }


    private static int? GetTokenId(MainDbContext databaseContext, int chainId, string symbol)
    {
        int? id = null;
        if ( !string.IsNullOrEmpty(symbol) )
            id = TokenMethods.Get(databaseContext, chainId, symbol).ID;
        return id;
    }


    private static void MarkNtfInfused(MainDbContext databaseContext, int chainId, string infusedSymbol,
        string infusedValue, Nft nft)
    {
        if ( string.IsNullOrEmpty(infusedSymbol) ||
             TokenMethods.Get(databaseContext, chainId, infusedSymbol).FUNGIBLE ) return;

        // NFT was infused, we should mark it as infused.
        var infusedNft = databaseContext.Nfts.FirstOrDefault(x => x.TOKEN_ID == infusedValue);
        if ( infusedNft == null )
            Log.Warning("NFT infused, could not find a Nft for Value {Infused}, Symbol {Symbol}", infusedValue,
                infusedSymbol);
        else
        {
            infusedNft.InfusedInto = nft;

            Log.Information("NFT infused: {InfusedNft} into NFT {Nft}", infusedNft.TOKEN_ID, nft.TOKEN_ID);
        }
    }


    private static void ProcessBurnedNft(MainDbContext databaseContext, Nft nft)
    {
        // For burns we must release all infused nfts.
        var infusedNftIDs =
            databaseContext.Nfts.Where(x => x.InfusedInto == nft).Select(x => x.TOKEN_ID).ToArray();
        foreach ( var id in infusedNftIDs )
        {
            var defusedNft = databaseContext.Nfts.First(x => x.TOKEN_ID == id);
            defusedNft.InfusedInto = null;

            if ( nft != null )
                Log.Information("NFT defused: {DefusedNft} from NFT {Nft}", defusedNft.TOKEN_ID, nft.TOKEN_ID);
        }
    }
}
