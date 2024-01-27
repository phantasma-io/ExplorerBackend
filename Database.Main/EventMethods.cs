using System;
using System.Linq;
using System.Threading.Tasks;
using Backend.Commons;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Database.Main;

public static class EventMethods
{
    // Checks if "Events" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.


    public static void DeleteByNftId(MainDbContext databaseContext, int nftId)
    {
        var tokenEvents = databaseContext.Events.Where(x => x.NftId == nftId);
        foreach ( var tokenEvent in tokenEvents ) databaseContext.Entry(tokenEvent).State = EntityState.Deleted;
    }


    private static void ProcessBurnedNft(MainDbContext databaseContext, Nft nft)
    {
        //cache might need checking as well
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

    public static Event Upsert(MainDbContext databaseContext,
        out bool newEventCreated,
        long timestampUnixSeconds,
        int index,
        Chain chain,
        Transaction transaction,
        Contract contract,
        EventKind eventKind,
        Address address)
    {
        newEventCreated = false;

        var eventEntry = new Event
        {
            DM_UNIX_SECONDS = UnixSeconds.Now(),
            TIMESTAMP_UNIX_SECONDS = timestampUnixSeconds,
            DATE_UNIX_SECONDS = UnixSeconds.GetDate(timestampUnixSeconds),
            INDEX = index,
            Chain = chain,
            Transaction = transaction,
            Contract = contract,
            EventKind = eventKind,
            Address = address
        };

        databaseContext.Events.Add(eventEntry);

        newEventCreated = true;

        return eventEntry;
    }


    public static async Task<bool> UpdateValuesAsync(MainDbContext databaseContext, Event eventItem, Nft nft,
        string tokenId, Chain chain, EventKind eventKind, Contract contract)
    {
        var eventUpdated = false;

        if ( eventItem == null ) return eventUpdated;

        eventItem.Chain = chain;
        eventItem.Contract = contract;
        eventItem.EventKind = eventKind;
        eventItem.TOKEN_ID = tokenId;
        eventItem.Nft = nft;

        eventUpdated = true;

        var burnEvent = await EventKindMethods.GetByNameAsync(databaseContext, chain, "TokenBurn");
        if ( burnEvent == null || eventKind != burnEvent || nft == null ) return eventUpdated;

        //TODO check if always needed
        // For burns we must release all infused nfts.

        var startTime = DateTime.Now;
        ProcessBurnedNft(databaseContext, nft);
        var updateTime = DateTime.Now - startTime;
        Log.Verbose("Process Burned, processed in {Time} sec", Math.Round(updateTime.TotalSeconds, 3));

        return eventUpdated;
    }
}
