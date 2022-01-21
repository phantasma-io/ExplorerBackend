using System.Linq;

namespace Database.Main;

public static class ExternalMethods
{
    public static void Upsert(MainDbContext databaseContext, string platformName, string hash, int tokenId)
    {
        var token = TokenMethods.Get(databaseContext, tokenId);
        var platform = PlatformMethods.Get(databaseContext, platformName);

        if ( token == null || platform == null ) return;

        var external =
            databaseContext.Externals.FirstOrDefault(x =>
                string.Equals(x.HASH.ToUpper(), hash.ToUpper()) && x.PlatformId == platform.ID && x.TokenId == tokenId);
        if ( external != null )
            return;


        external = new External {HASH = hash, Token = token, Platform = platform};

        databaseContext.Externals.Add(external);
        databaseContext.SaveChanges();
    }


    public static External Get(MainDbContext databaseContext, string hash, int platformId, int tokenId)
    {
        return databaseContext.Externals
            .FirstOrDefault(x =>
                x.PlatformId == platformId && x.TokenId == tokenId && string.Equals(x.HASH.ToUpper(), hash.ToUpper()));
    }
}
