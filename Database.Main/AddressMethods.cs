using System;
using System.Collections.Generic;
using System.Linq;

namespace Database.Main;

public class AddressParsed
{
    public readonly string address;
    public readonly bool caseSensitive;
    public readonly string chain;


    public AddressParsed(string address, string chainShortName = null)
    {
        this.address = ParseExtendedFormat(address, out caseSensitive, out chain, chainShortName);
    }


    private static string ParseExtendedFormat(string address, out bool caseSensitivity,
        out string resultingChainShortName, string chainShortName = null)
    {
        if ( address.Contains(':') )
        {
            var parsed = address.Split(':');
            if ( parsed.Length != 2 ) throw new Exception($"Incorrect address format: '{address}'.");

            chainShortName = parsed[0];
            address = parsed[1];
        }

        caseSensitivity = true;

        resultingChainShortName = chainShortName;

        ContractMethods.Drop0x(ref address);

        return address;
    }
}

public static class AddressMethods
{
    public static string Prepend0x(string address, string chainShortName = null, bool lowercaseWhenApplicable = true)
    {
        if ( string.IsNullOrEmpty(address) ) return address;

        // return "0x" + (lowercaseWhenApplicable ? address.ToLower() : address);

        return address;
    }


    private static AddressParsed[] ParseExtendedFormat(string addresses, string chainShortName = null)
    {
        var values = addresses.Contains(',') ? addresses.Split(',') : new[] {addresses};

        return values.Select(value => new AddressParsed(value, chainShortName)).ToArray();
    }


    // Checks if "Addresses" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    public static Address Upsert(MainDbContext databaseContext, int chainId, string address, bool saveChanges = true)
    {
        ContractMethods.Drop0x(ref address);

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


    public static Address Get(MainDbContext databaseContext, int chainId, string address)
    {
        ContractMethods.Drop0x(ref address);

        var entry = databaseContext.Addresses
            .FirstOrDefault(x => x.ChainId == chainId &&
                                 x.ADDRESS == address);

        if ( entry != null ) return entry;

        // Checking if entry has been added already
        // but not yet inserted into database.
        entry = DbHelper.GetTracked<Address>(databaseContext)
            .FirstOrDefault(x => x.ChainId == chainId &&
                                 x.ADDRESS == address);

        return entry;
    }


    public static int GetId(MainDbContext databaseContext, int chainId, string address)
    {
        ContractMethods.Drop0x(ref address);

        var id = 0;
        var entry = databaseContext.Addresses
            .FirstOrDefault(x => x.ChainId == chainId &&
                                 x.ADDRESS == address);

        if ( entry != null ) id = entry.ID;

        return id;
    }


    public static int[] GetIdsFromExtendedFormat(MainDbContext databaseContext, string addresses,
        bool returnNonexistentAddressIfNoneFound = true, string defaultChain = null)
    {
        var values = ParseExtendedFormat(addresses, defaultChain);

        // Getting owners' ids.
        var ids = new List<int>();
        for ( var i = 0; i < values.Length; i++ )
            // Address query is case insensitive only for BSC
            // TODO: check through special chain settings table

            if ( string.IsNullOrEmpty(values[i].chain) )
                ids.AddRange(databaseContext.Addresses.Where(x =>
                    ( values[i].caseSensitive
                        ? x.ADDRESS == values[i].address
                        : string.Equals(x.ADDRESS.ToUpper(), values[i].address.ToUpper()) ) ||
                    string.Equals(x.ADDRESS_NAME.ToUpper(), values[i].address.ToUpper()) ||
                    string.Equals(x.USER_NAME.ToUpper(), values[i].address.ToUpper())).Select(x => x.ID).ToArray());
            else
                ids.AddRange(databaseContext.Addresses.Where(x =>
                    ( ( values[i].caseSensitive
                          ? x.ADDRESS == values[i].address
                          : string.Equals(x.ADDRESS.ToUpper(), values[i].address.ToUpper()) ) ||
                      string.Equals(x.ADDRESS_NAME.ToUpper(), values[i].address.ToUpper()) ||
                      string.Equals(x.USER_NAME.ToUpper(), values[i].address.ToUpper()) ) &&
                    string.Equals(x.Chain.NAME.ToUpper(), values[i].chain.ToUpper())).Select(x => x.ID).ToArray());

        if ( returnNonexistentAddressIfNoneFound && !ids.Any() ) ids.Add(0);

        return ids.ToArray();
    }


    public static Dictionary<string, Address> InsertIfNotExists(MainDbContext databaseContext, int chainId,
        List<string> addresses,
        bool saveChanges = true)
    {
        if ( !addresses.Any() ) return null;

        var chain = ChainMethods.Get(databaseContext, chainId);
        var addressesToInsert = new List<Address>();

        //we use that to return
        Dictionary<string, Address> addressMap = new();

        foreach ( var address in addresses )
        {
            var addressString = address;
            ContractMethods.Drop0x(ref addressString);

            var entry = databaseContext.Addresses.FirstOrDefault(x => x.ChainId == chainId && x.ADDRESS == address);
            if ( entry == null )
            {
                entry = DbHelper.GetTracked<Address>(databaseContext)
                    .FirstOrDefault(x => x.ChainId == chainId && x.ADDRESS == address);
                if ( entry == null )
                {
                    entry = new Address {Chain = chain, ADDRESS = address};
                    addressesToInsert.Add(entry);
                }
            }

            if ( !addressMap.ContainsKey(address) ) addressMap.Add(address, entry);
        }

        databaseContext.Addresses.AddRange(addressesToInsert);
        if ( !saveChanges ) databaseContext.SaveChanges();

        return addressMap;
    }
}
