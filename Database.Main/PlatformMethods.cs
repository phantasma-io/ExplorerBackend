using System.Linq;

namespace Database.Main;

public static class PlatformMethods
{
    public static Platform Upsert(MainDbContext databaseContext, string name, string chain, string fuel,
        bool saveChanges = true)
    {
        var platform = databaseContext.Platforms.FirstOrDefault(x => string.Equals(x.NAME.ToUpper(), name.ToUpper()));
        if ( platform != null )
            return platform;

        platform = new Platform {NAME = name, CHAIN = chain, FUEL = fuel};

        databaseContext.Platforms.Add(platform);
        if ( saveChanges ) databaseContext.SaveChanges();

        return platform;
    }


    public static Platform Get(MainDbContext databaseContext, string name)
    {
        return databaseContext.Platforms
            .FirstOrDefault(x => x.NAME == name);
    }


    public static Platform Get(MainDbContext databaseContext, string name, string chain)
    {
        return databaseContext.Platforms
            .FirstOrDefault(x =>
                string.Equals(x.NAME.ToUpper(), name.ToUpper()) && string.Equals(x.CHAIN.ToUpper(), chain.ToUpper()));
    }
}
