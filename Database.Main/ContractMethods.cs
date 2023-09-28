using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Database.Main;

public static class ContractMethods
{
    // Checks if "Contracts" table has entry with given hash,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.


    public static Contract Get(MainDbContext databaseContext, int chainId, string hash)
    {
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
            var contract =
                databaseContext.Contracts.FirstOrDefault(x =>
                    x.Chain == chain && x.HASH == hash && x.SYMBOL == symbol) ?? DbHelper
                    .GetTracked<Contract>(databaseContext).FirstOrDefault(x =>
                        x.Chain == chain && x.HASH == hash && x.SYMBOL == symbol);

            if ( contract != null ) continue;

            contract = new Contract {NAME = name, Chain = chain, HASH = hash, SYMBOL = symbol};
            contractList.Add(contract);
        }

        databaseContext.Contracts.AddRange(contractList);
        if ( !saveChanges ) databaseContext.SaveChanges();
    }


    public static async Task<Contract> UpsertAsync(MainDbContext databaseContext, string name, Chain chain, string hash, string symbol)
    {
        //also check data in cache
        var contract =
            await databaseContext.Contracts.FirstOrDefaultAsync(x => x.Chain == chain && x.HASH == hash && x.SYMBOL == symbol) ??
            DbHelper.GetTracked<Contract>(databaseContext)
                .FirstOrDefault(x => x.Chain == chain && x.HASH == hash && x.SYMBOL == symbol);

        if ( contract != null ) return contract;

        contract = new Contract {NAME = name, Chain = chain, HASH = hash, SYMBOL = symbol};

        await databaseContext.Contracts.AddAsync(contract);

        return contract;
    }


    public static async Task<Contract> GetAsync(MainDbContext databaseContext, Chain chain, string name, string hash)
    {
        return await databaseContext.Contracts.FirstOrDefaultAsync(x => x.Chain == chain && x.NAME == name && x.HASH == hash) ??
               DbHelper.GetTracked<Contract>(databaseContext)
                   .FirstOrDefault(x => x.Chain == chain && x.NAME == name && x.HASH == hash);
    }


    public static Contract Get(MainDbContext databaseContext, Chain chain, string hash)
    {
        var contract = databaseContext.Contracts.FirstOrDefault(x => x.Chain == chain && x.HASH == hash);
        if ( contract != null ) return contract;

        contract = DbHelper.GetTracked<Contract>(databaseContext)
            .FirstOrDefault(x => x.Chain == chain && x.HASH == hash);
        return contract;
    }
}
