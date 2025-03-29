using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Commons;
using Microsoft.EntityFrameworkCore;

namespace Database.Main;

public static class NftMethods
{
    private const string OwnershipProcessingLock = "ownershipProcessingLock";


    // Checks if "Nfts" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.


    public static Nft Get(MainDbContext databaseContext, int chainId, int contractId, string tokenId)
    {
        return databaseContext.Nfts.FirstOrDefault(x =>
            x.ChainId == chainId && x.ContractId == contractId && x.TOKEN_ID == tokenId);
    }


    public static void Delete(MainDbContext databaseContext, int id, bool saveChanges = true)
    {
        var nft = databaseContext.Nfts.FirstOrDefault(x => x.ID == id);
        if ( nft != null ) databaseContext.Entry(nft).State = EntityState.Deleted;

        if ( saveChanges ) databaseContext.SaveChanges();
    }


    public static async Task<(Nft, bool)> UpsertAsync(MainDbContext databaseContext, Chain chain, string tokenId,
        string tokenUri, int contractId)
    {
        var entry = await databaseContext.Nfts.FirstOrDefaultAsync(x =>
            x.Chain == chain && x.ContractId == contractId && x.TOKEN_ID == tokenId) ?? DbHelper
            .GetTracked<Nft>(databaseContext).FirstOrDefault(x =>
                x.Chain == chain && x.ContractId == contractId && x.TOKEN_ID == tokenId);

        if ( entry != null )
        {
            entry.TOKEN_URI = tokenUri;
            return (entry, false);
        }

        entry = new Nft
        {
            Chain = chain,
            TOKEN_ID = tokenId,
            TOKEN_URI = tokenUri,
            ContractId = contractId,
            DM_UNIX_SECONDS = UnixSeconds.Now()
        };

        await databaseContext.Nfts.AddAsync(entry);

        return (entry, true);
    }


    public static void ProcessOwnershipChange(MainDbContext databaseContext, Chain chain, Nft nft,
        long timestampUnixSeconds, Address toAddress, bool saveChanges = true)
    {
        // This is our original logic based on ownership change timestamp.
        // We update ownership only if event is newer,
        // and ignore older events.
        // We can switch to 1155 logic, but don't see any pros in it for now.

        var lockSting = OwnershipProcessingLock + chain.ID;
        lock ( string.Intern(lockSting) )
        {
            //also check in cache
            var ownership = databaseContext.NftOwnerships.Where(x => x.Nft == nft)
                .OrderBy(x => x.LAST_CHANGE_UNIX_SECONDS).FirstOrDefault() ?? DbHelper
                .GetTracked<NftOwnership>(databaseContext).Where(x => x.Nft == nft)
                .MinBy(x => x.LAST_CHANGE_UNIX_SECONDS);

            if ( ownership == null )
            {
                // Ownership was never registered before, creating new entity.

                ownership = new NftOwnership
                {
                    Nft = nft,
                    Address = toAddress,
                    AMOUNT = 1,
                    LAST_CHANGE_UNIX_SECONDS = timestampUnixSeconds
                };

                databaseContext.NftOwnerships.Add(ownership);

                if ( saveChanges ) databaseContext.SaveChanges();
            }
            else if ( timestampUnixSeconds >= ownership.LAST_CHANGE_UNIX_SECONDS )
            {
                // Our ownership change is newer, we need to update entity.

                ownership.Address = toAddress;
                ownership.LAST_CHANGE_UNIX_SECONDS = timestampUnixSeconds;

                if ( saveChanges ) databaseContext.SaveChanges();
            }
        }
    }


    public static IEnumerable<int> GetIdsByOwnerAddress(MainDbContext databaseContext, string address, string chain)
    {
        var addressEntry = AddressMethods.Get(databaseContext, ChainMethods.Get(databaseContext, chain), address);

        return databaseContext.NftOwnerships.Where(x => x.Address == addressEntry).Select(x => x.NftId);
    }


    public static IEnumerable<int?> GetSeriesIdsByTokenId(MainDbContext databaseContext, string tokenId)
    {
        return databaseContext.Nfts.Where(x => x.TOKEN_ID == tokenId && x.SeriesId != null).Select(x => x.SeriesId);
    }
}
