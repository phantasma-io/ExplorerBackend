using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Database.Main;

public static class AddressMethods
{
    // Checks if "Addresses" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    public static Address Upsert(MainDbContext databaseContext, int chainId, string address)
    {
        if(address == null)
        {
            throw new($"Attempt to store null address");
        }
        if(address.Length < 47)
        {
            throw new($"Attempt to store address with invalid length {address.Length} '{address}'");
        }

        var entry = databaseContext.Addresses
            .FirstOrDefault(x => x.ChainId == chainId && x.ADDRESS == address);

        if ( entry != null ) return entry;

        // Checking if entry has been added already
        // but not yet inserted into database.
        entry = DbHelper.GetTracked<Address>(databaseContext)
            .FirstOrDefault(x => x.ChainId == chainId && x.ADDRESS == address);

        if ( entry != null ) return entry;

        var chain = ChainMethods.Get(databaseContext, chainId);
        entry = new Address {Chain = chain, ADDRESS = address};
        databaseContext.Addresses.Add(entry);

        return entry;
    }


    public static Address Get(MainDbContext databaseContext, Chain chain, string address)
    {
        var entry = databaseContext.Addresses.FirstOrDefault(x => x.Chain == chain && x.ADDRESS == address);

        if ( entry != null ) return entry;

        // Checking if entry has been added already
        // but not yet inserted into database.
        entry = DbHelper.GetTracked<Address>(databaseContext)
            .FirstOrDefault(x => x.Chain == chain && x.ADDRESS == address);

        return entry;
    }


    public static Address GetByName(MainDbContext databaseContext, Chain chain, string addressName)
    {
        var entry = databaseContext.Addresses.FirstOrDefault(x => x.Chain == chain && x.ADDRESS_NAME == addressName);

        if ( entry != null ) return entry;

        // Checking if entry has been added already
        // but not yet inserted into database.
        entry = DbHelper.GetTracked<Address>(databaseContext)
            .FirstOrDefault(x => x.Chain == chain && x.ADDRESS_NAME == addressName);

        return entry;
    }


    public static Dictionary<string, Address> InsertIfNotExists(MainDbContext databaseContext, Chain chain,
        List<string> addresses)
    {
        if ( !addresses.Any() || chain == null ) return null;

        var addressesToInsert = new List<Address>();

        //we use that to return
        Dictionary<string, Address> addressMap = new();

        foreach ( var address in addresses )
        {
            if(address == null)
            {
                throw new($"Attempt to store null address");
            }
            if(address.Length < 47)
            {
                throw new($"Attempt to store address with invalid length {address.Length} '{address}'");
            }

            var entry = databaseContext.Addresses.FirstOrDefault(x => x.Chain == chain && x.ADDRESS == address);
            if ( entry == null )
            {
                entry = DbHelper.GetTracked<Address>(databaseContext)
                    .FirstOrDefault(x => x.Chain == chain && x.ADDRESS == address);
                if ( entry == null )
                {
                    entry = new Address {Chain = chain, ADDRESS = address};
                    addressesToInsert.Add(entry);
                }
            }

            if ( !addressMap.ContainsKey(address) ) addressMap.Add(address, entry);
        }

        databaseContext.Addresses.AddRange(addressesToInsert);

        return addressMap;
    }


    public static async Task<Address> UpsertAsync(MainDbContext databaseContext, Chain chain, string address)
    {
        if(address == null)
        {
            throw new($"Attempt to store null address");
        }
        if(address.Length < 47)
        {
            throw new($"Attempt to store address with invalid length {address.Length} '{address}'");
        }

        var entry = await databaseContext.Addresses
            .FirstOrDefaultAsync(x => x.Chain == chain && x.ADDRESS == address);

        if ( entry != null ) return entry;

        // Checking if entry has been added already
        // but not yet inserted into database.
        entry = DbHelper.GetTracked<Address>(databaseContext)
            .FirstOrDefault(x => x.Chain == chain && x.ADDRESS == address);

        if ( entry != null ) return entry;

        entry = new Address {Chain = chain, ADDRESS = address};
        await databaseContext.Addresses.AddAsync(entry);

        return entry;
    }
}
