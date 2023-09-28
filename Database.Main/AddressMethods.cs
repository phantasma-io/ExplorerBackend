using System;
using System.Collections.Generic;
using System.Linq;

namespace Database.Main;

public static class AddressMethods
{
    // Checks if "Addresses" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    public static Address Upsert(MainDbContext databaseContext, int chainId, string address, bool saveChanges = true)
    {
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

        if ( !saveChanges ) return entry;


        try
        {
            databaseContext.SaveChanges();
        }
        catch ( Exception ex )
        {
            var exMessage = ex.ToString();
            if ( exMessage.Contains("duplicate key value violates unique constraint") &&
                 exMessage.Contains("IX_Addresses_ChainId_ADDRESS") )
            {
                // We tried to create same record in two threads concurrently.
                // Now we should just remove duplicating record and get an existing record.
                databaseContext.Addresses.Remove(entry);
                entry = databaseContext.Addresses.First(x => x.ChainId == chainId && x.ADDRESS == address);
            }
            else
                // Unknown exception.
                throw;
        }

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


    public static Address Upsert(MainDbContext databaseContext, Chain chain, string address)
    {
        var entry = databaseContext.Addresses
            .FirstOrDefault(x => x.Chain == chain && x.ADDRESS == address);

        if ( entry != null ) return entry;

        // Checking if entry has been added already
        // but not yet inserted into database.
        entry = DbHelper.GetTracked<Address>(databaseContext)
            .FirstOrDefault(x => x.Chain == chain && x.ADDRESS == address);

        if ( entry != null ) return entry;

        entry = new Address {Chain = chain, ADDRESS = address};
        databaseContext.Addresses.Add(entry);

        return entry;
    }
}
