using System;
using System.Collections.Generic;
using System.Linq;

namespace Database.ApiCache;

public static class ContractMethods
{
    public static string Drop0x(string hash)
    {
        if ( string.IsNullOrEmpty(hash) ) return hash;

        if ( hash.StartsWith("0x") ) hash = hash.Substring(2);

        return hash;
    }


    public static void Drop0x(ref string hash)
    {
        hash = Drop0x(hash);
    }


    // Checks if "Contracts" table has entry with given hash,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    public static int Upsert(ApiCacheDbContext databaseContext, int chainId, string hashOrName)
    {
        Drop0x(ref hashOrName);

        if ( string.IsNullOrEmpty(hashOrName) )
            throw new ArgumentException("Argument cannot be null or empty.", "hashOrName");

        int contractId;

        var contract = databaseContext.Contracts
            .FirstOrDefault(x => x.ChainId == chainId && x.HASH == hashOrName);

        if ( contract != null )
            contractId = contract.ID;
        else
        {
            contract = new Contract {ChainId = chainId, HASH = hashOrName};

            databaseContext.Contracts.Add(contract);

            databaseContext.SaveChanges();

            contractId = contract.ID;
        }

        return contractId;
    }


    public static Contract UpsertWOSave(ApiCacheDbContext databaseContext, int chainId, string hashOrName)
    {
        Drop0x(ref hashOrName);

        var contract = databaseContext.Contracts.FirstOrDefault(x => x.ChainId == chainId && x.HASH == hashOrName);

        if ( contract != null ) return contract;

        contract = new Contract {ChainId = chainId, HASH = hashOrName};

        databaseContext.Contracts.Add(contract);
        databaseContext.SaveChanges();

        return contract;
    }


    public static int GetId(ApiCacheDbContext databaseContext, string chainShortName, string hash)
    {
        Drop0x(ref hash);

        var chainId = ChainMethods.GetId(databaseContext, chainShortName);

        var contract = databaseContext.Contracts
            .FirstOrDefault(x => x.ChainId == chainId && string.Equals(x.HASH, hash));

        return contract?.ID ?? 0;
    }


    public static void InsertIfNotExists(ApiCacheDbContext databaseContext, List<string> contractInfoList, int chainId)
    {
        if ( !contractInfoList.Any() ) return;

        var contractList = new List<Contract>();

        foreach ( var hash in contractInfoList )
        {
            var hashString = hash;
            Drop0x(ref hashString);

            var contract = databaseContext.Contracts.FirstOrDefault(x => x.ChainId == chainId && x.HASH == hash);

            if ( contract != null ) continue;

            contract = new Contract {ChainId = chainId, HASH = hash};
            contractList.Add(contract);
        }

        databaseContext.Contracts.AddRange(contractList);
        databaseContext.SaveChanges();
    }
}
