using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace Database.Main;

public static class OrganizationAddressMethods
{
    public static OrganizationAddress Upsert(MainDbContext databaseContext, Organization organization, string address,
        int chainId, bool saveChanges = true)
    {
        if ( organization == null || string.IsNullOrEmpty(address) ) return null;

        var addressEntry = AddressMethods.Upsert(databaseContext, chainId, address, saveChanges);

        var organizationAddress = databaseContext.OrganizationAddresses.FirstOrDefault(x =>
            x.Address.ADDRESS == address && x.Organization == organization);

        if ( organizationAddress != null ) return organizationAddress;

        organizationAddress = new OrganizationAddress {Address = addressEntry, Organization = organization};

        databaseContext.OrganizationAddresses.Add(organizationAddress);
        if ( saveChanges ) databaseContext.SaveChanges();

        return organizationAddress;
    }
    
    public static void RemoveFromOrganizationAddressesIfNeeded(MainDbContext databaseContext, Organization organization, List<string> addresses, bool saveChanges = true)
    {
        if ( organization == null || !addresses.Any() ) return;

        var organizationAddress = databaseContext.OrganizationAddresses.Where(x => x.OrganizationId == organization.ID).ToList();

        if ( !organizationAddress.Any() ) return;
        var organizationAddressListUsers = organizationAddress.Select(x => x.Address.ADDRESS).ToList();
        var addressesToRemoveString = organizationAddressListUsers.Except(addresses);
        var addressesToRemove = organizationAddress.Where(x => addressesToRemoveString.Contains(x.Address.ADDRESS)).ToList();
        
        Log.Information("Removing {0} addresses from {1}", addressesToRemove.Count, organization.NAME);

        databaseContext.OrganizationAddresses.RemoveRange(addressesToRemove);
        if ( saveChanges ) databaseContext.SaveChanges();
    }

    public static void InsertIfNotExists(MainDbContext databaseContext, Organization organization,
        List<string> addresses, Chain chain, bool saveChanges = true)
    {
        if ( organization == null || !addresses.Any() ) return;

        var addressMap = AddressMethods.InsertIfNotExists(databaseContext, chain, addresses, saveChanges);

        var organizationAddressesToInsert = ( from address in addresses
            let organizationAddress =
                databaseContext.OrganizationAddresses.FirstOrDefault(x =>
                    x.Address.ADDRESS == address && x.Organization == organization)
            where organizationAddress == null
            select new OrganizationAddress
                {Address = addressMap.GetValueOrDefault(address), Organization = organization} ).ToList();

        databaseContext.OrganizationAddresses.AddRange(organizationAddressesToInsert);
        if ( saveChanges ) databaseContext.SaveChanges();
    }
    
    public static IEnumerable<Organization> GetOrganizationsByAddress(MainDbContext databaseContext, string address)
    {
        return string.IsNullOrEmpty(address)
            ? null
            : databaseContext.OrganizationAddresses.Where(x => x.Address.ADDRESS == address).Select(x => x.Organization);
    }


    public static IEnumerable<OrganizationAddress> GetOrganizationAddressByOrganization(MainDbContext databaseContext,
        string organization)
    {
        return string.IsNullOrEmpty(organization)
            ? null
            : databaseContext.OrganizationAddresses.Where(x => x.Organization.NAME == organization);
    }
}
