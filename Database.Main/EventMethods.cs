using System;
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
        bool saveChanges = true)
    {
        newEventCreated = false;

        var addressEntry = AddressMethods.Upsert(databaseContext, chainId, address, saveChanges);

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
                Address = addressEntry
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
        }

        return evnt;
    }


    public static void UpdateOnEventMerge(MainDbContext databaseContext, int id, int eventKindId, int sourceAddressId,
        bool hidden)
    {
        /*var evnt = databaseContext.Events.Single(x => x.ID == id);

        evnt.EventKindId = eventKindId;
        evnt.SourceAddressId = sourceAddressId;
        evnt.HIDDEN = hidden;

        evnt.DM_UNIX_SECONDS = UnixSeconds.Now();*/
    }


    public static void DeleteByNftId(MainDbContext databaseContext, int nftId, bool saveChanges = true)
    {
        var tokenEvents = databaseContext.Events.Where(x => x.NftId == nftId);
        foreach ( var tokenEvent in tokenEvents ) databaseContext.Entry(tokenEvent).State = EntityState.Deleted;

        if ( saveChanges ) databaseContext.SaveChanges();
    }


    public static Event UpdateValues(MainDbContext databaseContext, out bool eventUpdated, Event eventItem, Nft nft,
        string tokenId, int chainId, int eventKindId, int contractId)
    {
        eventUpdated = false;
        var startTime = DateTime.Now;

        var updateTime = DateTime.Now - startTime;
        Log.Verbose("Loaded Ids for Quote and Infused Symbol and Source Address processed in {Time} sec",
            Math.Round(updateTime.TotalSeconds, 3));


        if ( eventItem == null ) return null;

        eventItem.ChainId = chainId;
        eventItem.ContractId = contractId;
        eventItem.EventKindId = eventKindId;
        eventItem.TOKEN_ID = tokenId;
        eventItem.Nft = nft;

        eventUpdated = true;

        var burnEvent = databaseContext.EventKinds
            .FirstOrDefault(x => x.NAME == "TokenBurn" && x.ChainId == chainId);
        if ( burnEvent == null || eventKindId != burnEvent.ID || nft == null ) return eventItem;

        //TODO check if always needed
        // For burns we must release all infused nfts.

        startTime = DateTime.Now;
        ProcessBurnedNft(databaseContext, nft);
        updateTime = DateTime.Now - startTime;
        Log.Verbose("Process Burned, processed in {Time} sec", Math.Round(updateTime.TotalSeconds, 3));

        return eventItem;
    }


    private static void ProcessBurnedNft(MainDbContext databaseContext, Nft nft)
    {
        var nftList = databaseContext.Nfts.Where(x =>
            x.InfusedInto == nft && x.TOKEN_ID == databaseContext.Nfts.Where(y => y.TOKEN_ID == x.TOKEN_ID)
                .Select(y => y.TOKEN_ID).First());

        Log.Verbose("Got {Count} Ntfs to defuse", nftList.Count());

        foreach ( var item in nftList )
        {
            item.InfusedInto = null;
            if ( nft != null )
                Log.Information("NFT defused: {DefusedNft} from NFT {Nft}", item.TOKEN_ID, nft.TOKEN_ID);
        }
    }


    public static Event GetNextId(MainDbContext dbContext, int skip)
    {
        return dbContext.Events.OrderByDescending(x => x.ID).Skip(skip).FirstOrDefault();
    }


    public static Event GetById(MainDbContext dbContext, int id)
    {
        return dbContext.Events.FirstOrDefault(x => x.ID == id);
    }
}
