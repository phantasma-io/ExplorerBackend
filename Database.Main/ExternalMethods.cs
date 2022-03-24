using System;
using System.Collections.Generic;
using System.Linq;

namespace Database.Main;

public static class ExternalMethods
{
    public static void Upsert(MainDbContext databaseContext, string platformName, string hash, int tokenId,
        bool saveChanges = true)
    {
        var token = TokenMethods.Get(databaseContext, tokenId);
        var platform = PlatformMethods.Get(databaseContext, platformName);

        if ( token == null || platform == null ) return;

        var external =
            databaseContext.Externals.FirstOrDefault(x =>
                x.HASH == hash && x.PlatformId == platform.ID && x.TokenId == tokenId);
        if ( external != null )
            return;


        external = new External {HASH = hash, Token = token, Platform = platform};

        databaseContext.Externals.Add(external);
        if ( saveChanges ) databaseContext.SaveChanges();
    }


    public static External Get(MainDbContext databaseContext, string hash, int platformId, int tokenId)
    {
        return databaseContext.Externals.FirstOrDefault(x =>
            x.PlatformId == platformId && x.TokenId == tokenId && x.HASH == hash);
    }


    public static void InsertIfNotExists(MainDbContext databaseContext, List<Tuple<string, string>> externals,
        Token token, bool saveChanges = true)
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
        if ( !saveChanges ) databaseContext.SaveChanges();
    }
}
