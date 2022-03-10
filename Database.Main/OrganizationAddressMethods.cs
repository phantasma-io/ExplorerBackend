using System.Collections.Generic;
using System.Linq;

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


    public static void InsertIfNotExists(MainDbContext databaseContext, Organization organization,
        List<string> addresses,
        int chainId, bool saveChanges = true)
    {
        if ( organization == null || !addresses.Any() ) return;

        var addressMap = AddressMethods.InsertIfNotExists(databaseContext, chainId, addresses, saveChanges);

        var organizationAddressesToInsert = new List<OrganizationAddress>();

        foreach ( var address in addresses )
        {
            var organizationAddress = databaseContext.OrganizationAddresses.FirstOrDefault(x =>
                x.Address.ADDRESS == address && x.Organization == organization);

            if ( organizationAddress != null ) continue;

            organizationAddress = new OrganizationAddress
                {Address = addressMap.GetValueOrDefault(address), Organization = organization};
            organizationAddressesToInsert.Add(organizationAddress);
        }

        databaseContext.OrganizationAddresses.AddRange(organizationAddressesToInsert);
        if ( !saveChanges ) databaseContext.SaveChanges();
    }
}
