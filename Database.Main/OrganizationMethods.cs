using System.Linq;

namespace Database.Main;

public static class OrganizationMethods
{
    public static Organization Upsert(MainDbContext databaseContext, string name)
    {
        var organization =
            databaseContext.Organizations.FirstOrDefault(x => string.Equals(x.NAME.ToUpper(), name.ToUpper()));
        if ( organization != null )
            return organization;

        organization = new Organization {NAME = name};

        databaseContext.Organizations.Add(organization);
        databaseContext.SaveChanges();

        return organization;
    }


    public static Organization Get(MainDbContext databaseContext, string name)
    {
        return databaseContext.Organizations
            .FirstOrDefault(x => x.NAME == name);
    }
}
