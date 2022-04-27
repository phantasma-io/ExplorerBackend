using System.Collections.Generic;
using System.Linq;

namespace Database.Main;

public static class PlatformTokenMethods
{
    public static void Upsert(MainDbContext databaseContext, string name, Platform platform, bool saveChanges = true)
    {
        var platformToken =
            databaseContext.PlatformTokens.FirstOrDefault(x => x.NAME == name);
        if ( platformToken != null )
            return;

        platformToken = new PlatformToken {NAME = name, Platform = platform};

        databaseContext.PlatformTokens.Add(platformToken);
        if ( !saveChanges ) databaseContext.SaveChanges();
    }


    public static PlatformToken Get(MainDbContext databaseContext, string name)
    {
        return databaseContext.PlatformTokens.FirstOrDefault(x => x.NAME == name);
    }


    public static void InsertIfNotExists(MainDbContext databaseContext, IEnumerable<string> names, Platform platform,
        bool saveChanges = true)
    {
        var tokens =
            ( from name in names
                let platformToken = databaseContext.PlatformTokens.FirstOrDefault(x => x.NAME == name)
                where platformToken == null
                select new PlatformToken {NAME = name, Platform = platform} ).ToList();

        databaseContext.PlatformTokens.AddRange(tokens);
        if ( !saveChanges ) databaseContext.SaveChanges();
    }
}
