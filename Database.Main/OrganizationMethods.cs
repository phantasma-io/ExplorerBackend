using System.Linq;

namespace Database.Main;

public static class OrganizationMethods
{
    public static Organization Upsert(MainDbContext databaseContext, string name, bool saveChanges = true)
    {
        var organization = databaseContext.Organizations.FirstOrDefault(x => x.NAME == name);
        if ( organization != null )
            return organization;

        organization = DbHelper.GetTracked<Organization>(databaseContext).FirstOrDefault(x => x.NAME == name);
        if ( organization != null )
            return organization;

        organization = new Organization {NAME = name};

        databaseContext.Organizations.Add(organization);
        if ( saveChanges ) databaseContext.SaveChanges();

        return organization;
    }


    public static Organization Get(MainDbContext databaseContext, string name)
    {
        return databaseContext.Organizations
            .FirstOrDefault(x => x.NAME == name);
    }
}
