using System;
using System.Linq;

namespace Database.Main;

public static class ContractMethods
{
    public static string Drop0x(string hash)
    {
        if ( string.IsNullOrEmpty(hash) ) return hash;

        if ( hash.StartsWith("0x") ) hash = hash.Substring(2);

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

        if ( chainShortName != null && chainShortName.ToUpper() == "main" ) return contract;

        return "0x" + contract;
    }


    public static void Prepend0x(ref string contract, string chainShortName = null)
    {
        contract = Prepend0x(contract, chainShortName);
    }


    // Checks if "Contracts" table has entry with given hash,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    public static int Upsert(MainDbContext databaseContext, string name, int chain, string hash
        , string symbol)
    {
        Drop0x(ref hash);

        int contractId;

        var contract = databaseContext.Contracts
            .FirstOrDefault(x =>
                x.ChainId == chain && string.Equals(x.HASH.ToUpper(), hash.ToUpper()) &&
                string.Equals(x.SYMBOL.ToUpper(), symbol != null ? symbol.ToUpper() : null));

        if ( contract != null )
            contractId = contract.ID;
        else
        {
            contract = new Contract {NAME = name, ChainId = chain, HASH = hash, SYMBOL = symbol};

            databaseContext.Contracts.Add(contract);

            databaseContext.SaveChanges();

            contractId = contract.ID;
        }

        return contractId;
    }


    public static Contract Get(MainDbContext databaseContext, int chainId, string hash, bool ignoreCase = false)
    {
        Drop0x(ref hash);

        if ( ignoreCase )
            return databaseContext.Contracts
                .FirstOrDefault(x => x.ChainId == chainId && string.Equals(x.HASH.ToUpper(), hash.ToUpper()));

        return databaseContext.Contracts.FirstOrDefault(x => x.ChainId == chainId && x.HASH == hash);
    }


    public static int GetId(MainDbContext databaseContext, int chainId, string hash, bool ignoreCase = false)
    {
        var contract = Get(databaseContext, chainId, hash, ignoreCase);

        return contract?.ID ?? 0;
    }
}
