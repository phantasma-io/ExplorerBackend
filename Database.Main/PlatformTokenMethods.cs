using System.Linq;

namespace Database.Main;

public static class PlatformTokenMethods
{
    public static void Upsert(MainDbContext databaseContext, string name, Platform platform)
    {
        var platformToken =
            databaseContext.PlatformTokens.FirstOrDefault(x => string.Equals(x.NAME.ToUpper(), name.ToUpper()));
        if ( platformToken != null )
            return;

        platformToken = new PlatformToken {NAME = name, Platform = platform};

        databaseContext.PlatformTokens.Add(platformToken);
        databaseContext.SaveChanges();
    }


    public static PlatformToken Get(MainDbContext databaseContext, string name)
    {
        return databaseContext.PlatformTokens
            .FirstOrDefault(x => x.NAME == name);
    }
}
