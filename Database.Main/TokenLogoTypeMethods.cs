using System.Linq;

namespace Database.Main;

public static class TokenLogoTypeMethods
{
    public static TokenLogoType Upsert(MainDbContext databaseContext, string name, bool saveChanges = true)
    {
        var tokenLogotype = databaseContext.TokenLogoTypes.FirstOrDefault(x => x.NAME == name);
        if ( tokenLogotype != null )
            return tokenLogotype;

        tokenLogotype = DbHelper.GetTracked<TokenLogoType>(databaseContext).FirstOrDefault(x => x.NAME == name);
        if ( tokenLogotype != null )
            return tokenLogotype;

        tokenLogotype = new TokenLogoType {NAME = name};

        databaseContext.TokenLogoTypes.Add(tokenLogotype);
        if ( saveChanges ) databaseContext.SaveChanges();

        return tokenLogotype;
    }
}
