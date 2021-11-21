using System;
using System.Linq;
using System.Numerics;

namespace Database.Main
{
    public static class ChainMethods
    {
        // Checks if "Chains" table has entry with given name,
        // and adds new entry, if there's no entry available.
        // Returns new or existing entry's Id.
        public static int Upsert(MainDbContext databaseContext, string name)
        {
            int chainId;
            var chain = databaseContext.Chains.Where(x => x.NAME.ToUpper() == name.ToUpper()).FirstOrDefault();
            if (chain != null)
            {
                chainId = chain.ID;
            }
            else
            {
                chain = new Chain { NAME = name, CURRENT_HEIGHT = "0" };

                databaseContext.Chains.Add(chain);
                databaseContext.SaveChanges();

                chainId = chain.ID;
            }
            return chainId;
        }
        public static Chain Get(MainDbContext databaseContext, int id)
        {
            return databaseContext.Chains.Where(x => x.ID == id).Single();
        }
        public static Chain Get(MainDbContext databaseContext, string name)
        {
            return databaseContext.Chains.Where(x => x.NAME.ToUpper() == name.ToUpper()).Single();
        }
        public static int GetId(MainDbContext databaseContext, string name)
        {
            if(!String.IsNullOrEmpty(name))
                return databaseContext.Chains.Where(x => x.NAME.ToUpper() == name.ToUpper()).Select(x => x.ID).FirstOrDefault();

            return 0;
        }
        public static BigInteger GetLastProcessedBlock(MainDbContext databaseContext, int chainId)
        {
            return BigInteger.Parse(Get(databaseContext, chainId).CURRENT_HEIGHT);
        }

        public static void SetLastProcessedBlock(MainDbContext databaseContext, int chainId, BigInteger height, bool saveChanges = true)
        {
            var chain = Get(databaseContext, chainId);
            chain.CURRENT_HEIGHT = height.ToString();

            if(saveChanges)
                databaseContext.SaveChanges();
        }
    }
}
