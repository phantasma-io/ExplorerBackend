using System;
using System.Collections.Generic;
using System.Linq;

namespace Database.ApiCache;

public static class ContractMethods
{
    // Checks if "Contracts" table has entry with given hash,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    
    public static void InsertIfNotExists(ApiCacheDbContext databaseContext, List<string> contractInfoList, Chain chain)
    {
        if ( !contractInfoList.Any() ) return;

        var contractList = new List<Contract>();

        foreach ( var hash in contractInfoList )
        {
            var contract = databaseContext.Contracts.FirstOrDefault(x => x.Chain == chain && x.HASH == hash);

            if ( contract != null ) continue;

            contract = new Contract {Chain = chain, HASH = hash};
            contractList.Add(contract);
        }

        databaseContext.Contracts.AddRange(contractList);
    }


    public static Contract Upsert(ApiCacheDbContext databaseContext, Chain chain, string hashOrName)
    {
        if ( string.IsNullOrEmpty(hashOrName) )
            throw new ArgumentException("Argument cannot be null or empty.", "hashOrName");


        var contract = databaseContext.Contracts.FirstOrDefault(x => x.Chain == chain && x.HASH == hashOrName);

        if ( contract != null ) return contract;

        contract = new Contract {Chain = chain, HASH = hashOrName};

        databaseContext.Contracts.Add(contract);

        return contract;
    }


    public static Contract Get(ApiCacheDbContext databaseContext, string chainShortName, string hash)
    {
        var chain = ChainMethods.Get(databaseContext, chainShortName);
        return databaseContext.Contracts.FirstOrDefault(x => x.Chain == chain && x.HASH == hash);
    }
}
