using System.Collections.Generic;
using System.Linq;

namespace Database.Main;

public static class TokenLogoMethods
{
    public static void InsertIfNotExistList(MainDbContext databaseContext, Token token,
        Dictionary<string, string> tokenLogoList, bool saveChanges = true)
    {
        if ( !tokenLogoList.Any() ) return;

        var tokenLogos = new List<TokenLogo>();
        foreach ( var (type, url) in tokenLogoList )
        {
            var tokenLogoType = TokenLogoTypeMethods.Upsert(databaseContext, type);

            var tokenLogo =
                databaseContext.TokenLogos.FirstOrDefault(x => x.Token == token && x.TokenLogoType == tokenLogoType) ??
                DbHelper.GetTracked<TokenLogo>(databaseContext)
                    .FirstOrDefault(x => x.Token == token && x.TokenLogoType == tokenLogoType);

            if ( tokenLogo != null )
            {
                tokenLogo.URL = url;
                continue;
            }

            tokenLogo = new TokenLogo
            {
                Token = token,
                URL = url,
                TokenLogoType = tokenLogoType
            };
            tokenLogos.Add(tokenLogo);
        }

        databaseContext.TokenLogos.AddRange(tokenLogos);
        if ( !saveChanges ) databaseContext.SaveChanges();
    }
}
