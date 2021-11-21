using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using GhostDevs.Commons;

namespace Database.Main
{
    public static class NftMethods
    {
        // Checks if "Nfts" table has entry with given name,
        // and adds new entry, if there's no entry available.
        // Returns new or existing entry's Id.
        public static Nft Upsert(MainDbContext databaseContext,
            out bool newNftCreated,
            int chainId,
            string tokenId,
            string tokenUri,
            int contractId)
        {
            newNftCreated = false;

            var entry = databaseContext.Nfts.Where(x => x.ChainId == chainId && x.ContractId == contractId && x.TOKEN_ID == tokenId).FirstOrDefault();
            if (entry != null)
            {
                entry.TOKEN_URI = tokenUri;
            }
            else
            {
                entry = new Nft {
                    ChainId = chainId,
                    TOKEN_ID = tokenId,
                    TOKEN_URI = tokenUri,
                    ContractId = contractId
                };

                entry.DM_UNIX_SECONDS = UnixSeconds.Now();

                databaseContext.Nfts.Add(entry);
                
                newNftCreated = true;
            }

            return entry;
        }
        public static Nft Get(MainDbContext databaseContext, int chainId, int contractId, string tokenId)
        {
            return databaseContext.Nfts.Where(x => x.ChainId == chainId && x.ContractId == contractId && x.TOKEN_ID == tokenId).FirstOrDefault();
        }
        public static void Delete(MainDbContext databaseContext, int id, bool saveChanges = true)
        {
            var nft = databaseContext.Nfts.Where(x => x.ID == id).FirstOrDefault();
            if (nft != null)
                databaseContext.Entry(nft).State = EntityState.Deleted;

            if(saveChanges)
                databaseContext.SaveChanges();
        }
        private static readonly string ownershipProcessingLock = "ownershipProcessingLock";
        public static void ProcessOwnershipChange(MainDbContext databaseContext, int chainId, Nft nft, Int64 timestampUnixSeconds, string toAddress, bool saveChanges = true)
        {
            // This is our original logic based on ownership change timestamp.
            // We update ownership only if event is newer,
            // and ignore older events.
            // We can switch to 1155 logic, but don't see any pros in it for now.

            var lockSting = ownershipProcessingLock + chainId;
            lock (string.Intern(lockSting))
            {
                var ownership = databaseContext.NftOwnerships.Where(x => x.Nft == nft).OrderBy(x => x.LAST_CHANGE_UNIX_SECONDS).FirstOrDefault();

                if (ownership == null)
                {
                    // Ownership was never registered before, creating new entity.

                    ownership = new NftOwnership
                    {
                        Nft = nft,
                        Address = AddressMethods.Upsert(databaseContext, chainId, toAddress, false),
                        AMOUNT = 1,
                        LAST_CHANGE_UNIX_SECONDS = timestampUnixSeconds
                    };

                    databaseContext.NftOwnerships.Add(ownership);

                    if (saveChanges)
                        databaseContext.SaveChanges();
                }
                else if (ownership != null && timestampUnixSeconds >= ownership.LAST_CHANGE_UNIX_SECONDS)
                {
                    // Our ownership change is newer, we need to update entity.

                    ownership.Address = AddressMethods.Upsert(databaseContext, nft.ChainId, toAddress, false);
                    ownership.LAST_CHANGE_UNIX_SECONDS = timestampUnixSeconds;

                    if (saveChanges)
                        databaseContext.SaveChanges();
                }
            }
        }
    }
}
