using System.Collections.Generic;
using System.Linq;

namespace Database.Main;

public static class PlatformTokenMethods
{
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
