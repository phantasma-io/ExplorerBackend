using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Database.Main;

public static class OrganizationMethods
{
    public static Organization Upsert(MainDbContext databaseContext, string id, string name)
    {
        var organization =
            databaseContext.Organizations.FirstOrDefault(x => x.ORGANIZATION_ID == id);
        if ( organization != null )
            return organization;

        organization = DbHelper.GetTracked<Organization>(databaseContext)
            .FirstOrDefault(x => x.ORGANIZATION_ID == id);
        if ( organization != null )
            return organization;

        organization = new Organization {ORGANIZATION_ID = id, NAME = name};

        databaseContext.Organizations.Add(organization);

        return organization;
    }


    public static Organization Get(MainDbContext databaseContext, string id)
    {
        return databaseContext.Organizations
            .FirstOrDefault(x => x.ORGANIZATION_ID == id || x.NAME == id || x.ADDRESS == id);
    }

    // TODO this is a slow/bad approach.
    // To be used only as a fix to current counters values.
    public static void UpdateStakeCounts(MainDbContext dbContext, Chain chain)
    {
        var stakersOrg = Get(dbContext, "stakers");

        var allStakers = dbContext.Addresses.FromSqlRaw(@"SELECT * FROM ""Addresses"" WHERE CAST(""STAKED_AMOUNT_RAW"" AS BIGINT) > 0")
            .Select(x => x.ADDRESS)
            .ToList();
        
        // TODO these interfaces are also bad, we should be passing db entity IDs.
        OrganizationAddressMethods.RemoveFromOrganizationAddressesIfNeeded(dbContext, stakersOrg, allStakers);
        OrganizationAddressMethods.InsertIfNotExists(dbContext, stakersOrg, allStakers, chain);
        
        var smsOrg = Get(dbContext, "masters");

        var smStakers = dbContext.Addresses.FromSqlRaw(@"SELECT * FROM ""Addresses"" WHERE CAST(""STAKED_AMOUNT_RAW"" AS BIGINT) >= 5000000000000")
            .Select(x => x.ADDRESS)
            .ToList();
        
        // TODO these interfaces are also bad, we should be passing db entity IDs.
        OrganizationAddressMethods.RemoveFromOrganizationAddressesIfNeeded(dbContext, smsOrg, smStakers);
        OrganizationAddressMethods.InsertIfNotExists(dbContext, smsOrg, smStakers, chain);
    }
}
