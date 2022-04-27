using System;
using System.Collections.Generic;
using System.Linq;

namespace Database.ApiCache;

public static class ContractMethods
{
    private static string Drop0x(string hash)
    {
        if ( string.IsNullOrEmpty(hash) ) return hash;

        if ( hash.StartsWith("0x") ) hash = hash[2..];

        return hash;
    }


    private static void Drop0x(ref string hash)
    {
        hash = Drop0x(hash);
    }


    // Checks if "Contracts" table has entry with given hash,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.


    public static void InsertIfNotExists(ApiCacheDbContext databaseContext, List<string> contractInfoList, Chain chain,
        bool saveChanges = true)
    {
        if ( !contractInfoList.Any() ) return;

        var contractList = new List<Contract>();

        foreach ( var hash in contractInfoList )
        {
            var hashString = hash;
            Drop0x(ref hashString);

            var contract = databaseContext.Contracts.FirstOrDefault(x => x.Chain == chain && x.HASH == hash);

            if ( contract != null ) continue;

            contract = new Contract {Chain = chain, HASH = hash};
            contractList.Add(contract);
        }

        databaseContext.Contracts.AddRange(contractList);
        if ( saveChanges ) databaseContext.SaveChanges();
    }


    public static Contract Upsert(ApiCacheDbContext databaseContext, Chain chain, string hashOrName,
        bool saveChanges = true)
    {
        Drop0x(ref hashOrName);

        if ( string.IsNullOrEmpty(hashOrName) )
            throw new ArgumentException("Argument cannot be null or empty.", "hashOrName");


        var contract = databaseContext.Contracts.FirstOrDefault(x => x.Chain == chain && x.HASH == hashOrName);

        if ( contract != null ) return contract;

        contract = new Contract {Chain = chain, HASH = hashOrName};

        databaseContext.Contracts.Add(contract);
        if ( saveChanges ) databaseContext.SaveChanges();

        return contract;
    }


    public static Contract Get(ApiCacheDbContext databaseContext, string chainShortName, string hash)
    {
        Drop0x(ref hash);

        var chain = ChainMethods.Get(databaseContext, chainShortName);
        return databaseContext.Contracts.FirstOrDefault(x => x.Chain == chain && x.HASH == hash);
    }
}
