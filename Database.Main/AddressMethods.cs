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
        if (address == null)
        {
            throw new($"Attempt to store null address");
        }
        // TODO we should get rid of this NULL address and fix db schema
        if (address.Length < 47 && address.ToUpperInvariant() != "NULL")
        {
            throw new($"Attempt to store address with invalid length {address.Length} '{address}'");
        }

        var entry = databaseContext.Addresses
            .FirstOrDefault(x => x.ChainId == chainId && x.ADDRESS == address);

        if (entry != null) return entry;

        // Checking if entry has been added already
        // but not yet inserted into database.
        entry = DbHelper.GetTracked<Address>(databaseContext)
            .FirstOrDefault(x => x.ChainId == chainId && x.ADDRESS == address);

        if (entry != null) return entry;

        var chain = ChainMethods.Get(databaseContext, chainId);
        entry = new Address { Chain = chain, ADDRESS = address };
        databaseContext.Addresses.Add(entry);

        return entry;
    }


    public static Address Get(MainDbContext databaseContext, Chain chain, string address)
    {
        var chainId = chain?.ID ?? 0;
        var entry = databaseContext.Addresses.FirstOrDefault(x => x.ChainId == chainId && x.ADDRESS == address);

        if (entry != null) return entry;

        // Checking if entry has been added already
        // but not yet inserted into database.
        entry = DbHelper.GetTracked<Address>(databaseContext)
            .FirstOrDefault(x => x.ChainId == chainId && x.ADDRESS == address);

        return entry;
    }


    public static Address GetByName(MainDbContext databaseContext, Chain chain, string addressName)
    {
        var chainId = chain?.ID ?? 0;
        var entry = databaseContext.Addresses.FirstOrDefault(x => x.ChainId == chainId && x.ADDRESS_NAME == addressName);

        if (entry != null) return entry;

        // Checking if entry has been added already
        // but not yet inserted into database.
        entry = DbHelper.GetTracked<Address>(databaseContext)
            .FirstOrDefault(x => x.ChainId == chainId && x.ADDRESS_NAME == addressName);

        return entry;
    }


    public static Dictionary<string, Address> InsertIfNotExists(MainDbContext databaseContext, Chain chain,
        List<string> addresses)
    {
        if (!addresses.Any() || chain == null) return null;

        var chainId = chain.ID;
        if (addresses.Any(x => x == null))
            throw new("Attempt to store null address");

        var distinctAddresses = addresses
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Validate inputs first to preserve previous guard behavior.
        foreach (var address in distinctAddresses)
        {
            // TODO we should get rid of this NULL address and fix db schema
            if (address.Length < 47 && address.ToUpperInvariant() != "NULL")
            {
                throw new($"Attempt to store address with invalid length {address.Length} '{address}'");
            }
        }

        if (distinctAddresses.Count == 0)
            return new Dictionary<string, Address>(StringComparer.Ordinal);

        // Fetch existing rows in a single query and merge tracked entities to avoid per-address DB probes.
        var existing = databaseContext.Addresses
            .Where(x => x.ChainId == chainId && distinctAddresses.Contains(x.ADDRESS))
            .ToList();

        var tracked = DbHelper.GetTracked<Address>(databaseContext)
            .Where(x => x.ChainId == chainId && distinctAddresses.Contains(x.ADDRESS));

        var addressMap = existing
            .Concat(tracked)
            .GroupBy(x => x.ADDRESS, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        var addressesToInsert = distinctAddresses
            .Where(x => !addressMap.ContainsKey(x))
            .Select(x => new Address { Chain = chain, ADDRESS = x })
            .ToList();

        if (addressesToInsert.Count > 0)
        {
            databaseContext.Addresses.AddRange(addressesToInsert);
            foreach (var inserted in addressesToInsert)
                addressMap[inserted.ADDRESS] = inserted;
        }

        return addressMap;
    }


    public static async Task<Address> UpsertAsync(MainDbContext databaseContext, Chain chain, string address)
    {
        if (address == null)
        {
            throw new($"Attempt to store null address");
        }
        // TODO we should get rid of this NULL address and fix db schema
        if (address.Length < 47 && address.ToUpperInvariant() != "NULL")
        {
            throw new($"Attempt to store address with invalid length {address.Length} '{address}'");
        }

        var chainId = chain.ID;
        var entry = await databaseContext.Addresses
            .FirstOrDefaultAsync(x => x.ChainId == chainId && x.ADDRESS == address);

        if (entry != null) return entry;

        // Checking if entry has been added already
        // but not yet inserted into database.
        entry = DbHelper.GetTracked<Address>(databaseContext)
            .FirstOrDefault(x => x.ChainId == chainId && x.ADDRESS == address);

        if (entry != null) return entry;

        entry = new Address { Chain = chain, ADDRESS = address };
        await databaseContext.Addresses.AddAsync(entry);

        return entry;
    }
}
