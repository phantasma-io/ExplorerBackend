using System.Linq;

namespace Database.ApiCache
{
    public static class ContractMethods
    {
        public static string Drop0x(string hash)
        {
            if (string.IsNullOrEmpty(hash))
                return hash;

            if (hash.StartsWith("0x"))
                hash = hash.Substring(2);

            return hash;
        }
        public static void Drop0x(ref string hash)
        {
            hash = Drop0x(hash);
        }

        // Checks if "Contracts" table has entry with given hash,
        // and adds new entry, if there's no entry available.
        // Returns new or existing entry's Id.
        public static int Upsert(ApiCacheDatabaseContext databaseContext, int chainId, string hashOrName)
        {
            Drop0x(ref hashOrName);

            if (string.IsNullOrEmpty(hashOrName))
                throw new System.ArgumentException("Argument cannot be null or empty.", "hashOrName");

            int contractId;

            var contract = databaseContext.Contracts.Where(x => x.ChainId == chainId && x.HASH == hashOrName).FirstOrDefault();

            if (contract != null)
            {
                contractId = contract.ID;
            }
            else
            {
                contract = new Contract { ChainId = chainId, HASH = hashOrName };

                databaseContext.Contracts.Add(contract);

                databaseContext.SaveChanges();

                contractId = contract.ID;
            }

            return contractId;
        }
        public static Contract UpsertWOSave(ApiCacheDatabaseContext databaseContext, int chainId, string hashOrName)
        {
            Drop0x(ref hashOrName);

            var contract = databaseContext.Contracts.Where(x => x.ChainId == chainId && x.HASH == hashOrName).FirstOrDefault();

            if (contract == null)
            {
                contract = new Contract { ChainId = chainId, HASH = hashOrName };

                databaseContext.Contracts.Add(contract);

                databaseContext.SaveChanges();
            }

            return contract;
        }

        public static int GetId(ApiCacheDatabaseContext databaseContext, string chainShortName, string hash)
        {
            Drop0x(ref hash);

            var chainId = Database.ApiCache.ChainMethods.GetId(databaseContext, chainShortName);

            var contract = databaseContext.Contracts.Where(x => x.ChainId == chainId && x.HASH.ToUpper() == hash.ToUpper()).FirstOrDefault();
            if (contract == null)
                return 0;
            
            return contract.ID;
        }
    }
}
