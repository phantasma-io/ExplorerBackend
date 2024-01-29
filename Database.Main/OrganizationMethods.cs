using System.Linq;

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
}
