using System;
using System.Collections.Generic;
using System.Linq;

namespace Database.Main;

public static class ContractMethods
{
    private static string Drop0x(string hash)
    {
        if ( string.IsNullOrEmpty(hash) ) return hash;

        if ( hash.StartsWith("0x") ) hash = hash[2..];

        // For comma-separated values
        hash = hash.Replace(",0x", ",");

        return hash;
    }


    public static void Drop0x(ref string hash)
    {
        hash = Drop0x(hash);
    }


    public static string Prepend0x(string contract, string chainShortName = null)
    {
        if ( string.IsNullOrEmpty(contract) ) return contract;

        if ( contract.Length <= 10 ) return contract;

        if ( chainShortName is "main" ) return contract;

        return "0x" + contract;
    }


    public static void Prepend0x(ref string contract, string chainShortName = null)
    {
        contract = Prepend0x(contract, chainShortName);
    }


    // Checks if "Contracts" table has entry with given hash,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    public static int Upsert(MainDbContext databaseContext, string name, int chain, string hash, string symbol,
        bool saveChanges = true)
    {
        Drop0x(ref hash);

        int contractId;

        var contract =
            databaseContext.Contracts.FirstOrDefault(x => x.ChainId == chain && x.HASH == hash && x.SYMBOL == symbol);

        if ( contract != null )
            contractId = contract.ID;
        else
        {
            contract = new Contract {NAME = name, ChainId = chain, HASH = hash, SYMBOL = symbol};

            databaseContext.Contracts.Add(contract);

            if ( saveChanges ) databaseContext.SaveChanges();

            contractId = contract.ID;
        }

        return contractId;
    }


    public static Contract Get(MainDbContext databaseContext, int chainId, string hash)
    {
        Drop0x(ref hash);

        return databaseContext.Contracts.FirstOrDefault(x => x.ChainId == chainId && x.HASH == hash);
    }


    public static int GetId(MainDbContext databaseContext, int chainId, string hash)
    {
        var contract = Get(databaseContext, chainId, hash);

        return contract?.ID ?? 0;
    }


    public static void InsertIfNotExistList(MainDbContext databaseContext, List<Tuple<string, string>> contractInfoList,
        Chain chain, string symbol, bool saveChanges = true)
    {
        if ( !contractInfoList.Any() || string.IsNullOrEmpty(symbol) ) return;

        var contractList = new List<Contract>();
        //name, hash
        foreach ( var (name, hash) in contractInfoList )
        {
            var hashString = hash;
            Drop0x(ref hashString);
            var contract =
                databaseContext.Contracts.FirstOrDefault(x =>
                    x.Chain == chain && x.HASH == hashString && x.SYMBOL == symbol) ?? DbHelper
                    .GetTracked<Contract>(databaseContext).FirstOrDefault(x =>
                        x.Chain == chain && x.HASH == hashString && x.SYMBOL == symbol);

            if ( contract != null ) continue;

            contract = new Contract {NAME = name, Chain = chain, HASH = hashString, SYMBOL = symbol};
            contractList.Add(contract);
        }

        databaseContext.Contracts.AddRange(contractList);
        if ( !saveChanges ) databaseContext.SaveChanges();
    }


    public static Contract Upsert(MainDbContext databaseContext, string name, Chain chain, string hash, string symbol,
        bool saveChanges = true)
    {
        Drop0x(ref hash);

        //also check data in cache
        var contract =
            databaseContext.Contracts.FirstOrDefault(x => x.Chain == chain && x.HASH == hash && x.SYMBOL == symbol) ??
            DbHelper.GetTracked<Contract>(databaseContext)
                .FirstOrDefault(x => x.Chain == chain && x.HASH == hash && x.SYMBOL == symbol);

        if ( contract != null ) return contract;

        contract = new Contract {NAME = name, Chain = chain, HASH = hash, SYMBOL = symbol};

        databaseContext.Contracts.Add(contract);

        if ( saveChanges ) databaseContext.SaveChanges();

        return contract;
    }


    public static Contract Get(MainDbContext databaseContext, Chain chain, string name, string hash)
    {
        Drop0x(ref hash);

        return databaseContext.Contracts.FirstOrDefault(x => x.Chain == chain && x.NAME == name && x.HASH == hash) ??
               DbHelper.GetTracked<Contract>(databaseContext)
                   .FirstOrDefault(x => x.Chain == chain && x.NAME == name && x.HASH == hash);
    }


    public static Contract Get(MainDbContext databaseContext, Chain chain, string hash)
    {
        Drop0x(ref hash);

        var contract = databaseContext.Contracts.FirstOrDefault(x => x.Chain == chain && x.HASH == hash);
        if ( contract != null ) return contract;

        contract = DbHelper.GetTracked<Contract>(databaseContext)
            .FirstOrDefault(x => x.Chain == chain && x.HASH == hash);
        return contract;
    }
}
