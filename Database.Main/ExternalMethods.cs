using System;
using System.Collections.Generic;
using System.Linq;

namespace Database.Main;

public static class ExternalMethods
{
    public static void InsertIfNotExists(MainDbContext databaseContext, List<Tuple<string, string>> externals,
        Token token)
    {
        if ( token == null || !externals.Any() ) return;

        var externalList = new List<External>();
        foreach ( var (platformName, hash) in externals )
        {
            var platform = PlatformMethods.Get(databaseContext, platformName);
            var external = databaseContext.Externals.FirstOrDefault(x =>
                x.HASH == hash && x.PlatformId == platform.ID && x.Token == token);

            if ( external != null ) continue;

            external = new External {HASH = hash, Token = token, Platform = platform};
            externalList.Add(external);
        }

        databaseContext.Externals.AddRange(externalList);
    }
}
