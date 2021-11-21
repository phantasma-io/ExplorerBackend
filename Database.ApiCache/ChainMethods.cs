using System;
using System.Linq;

namespace Database.ApiCache
{
    public static class ChainMethods
    {
        // Checks if "Chains" table has entry with given name,
        // and adds new entry, if there's no entry available.
        // Returns new or existing entry's Id.
        public static int Upsert(ApiCacheDatabaseContext databaseContext, string shortName)
        {
            if (string.IsNullOrEmpty(shortName))
                throw new System.ArgumentException("Argument cannot be null or empty.", "shortName");

            int chainId;
            var chain = databaseContext.Chains.Where(x => x.SHORT_NAME.ToUpper() == shortName.ToUpper()).FirstOrDefault();
            if (chain != null)
            {
                chainId = chain.ID;

                chain.SHORT_NAME = shortName;

                databaseContext.SaveChanges();
            }
            else
            {
                chain = new Chain { SHORT_NAME = shortName };

                databaseContext.Chains.Add(chain);

                try
                {
                    databaseContext.SaveChanges();
                }
                catch (Exception ex)
                {
                    var exMessage = ex.ToString();
                    if (exMessage.Contains("duplicate key value violates unique constraint") &&
                        exMessage.Contains("IX_Chains_SHORT_NAME"))
                    {
                        // We tried to create same record in two threads concurrently.
                        // Now we should just remove duplicating record and get an existing record.
                        databaseContext.Chains.Remove(chain);
                        chain = databaseContext.Chains.Where(x => x.SHORT_NAME.ToUpper() == shortName.ToUpper()).First();
                    }
                    else
                    {
                        // Unknown exception.
                        throw;
                    }
                }

                chainId = chain.ID;
            }
            return chainId;
        }

        public static int GetId(ApiCacheDatabaseContext databaseContext, string shortName)
        {
            if (string.IsNullOrEmpty(shortName))
                throw new System.ArgumentException("Argument cannot be null or empty.", "shortName");

            var chain = databaseContext.Chains.Where(x => x.SHORT_NAME.ToUpper() == shortName.ToUpper()).FirstOrDefault();
            if (chain != null)
            {
                return chain.ID;
            }
            return 0;
        }
    }
}
