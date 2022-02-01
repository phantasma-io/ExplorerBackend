namespace Database.Main;

public static class OrganizationAddressMethods
{
    public static OrganizationAddress Upsert(MainDbContext databaseContext, Organization organization, string address,
        int chainId, bool saveChanges = true)
    {
        if ( organization == null || string.IsNullOrEmpty(address) ) return null;

        var addressEntry = AddressMethods.Upsert(databaseContext, chainId, address, false);

        var organizationAddress = new OrganizationAddress {Address = addressEntry, Organization = organization};

        databaseContext.OrganizationAddresses.Add(organizationAddress);
        if ( saveChanges ) databaseContext.SaveChanges();

        return organizationAddress;
    }
}
