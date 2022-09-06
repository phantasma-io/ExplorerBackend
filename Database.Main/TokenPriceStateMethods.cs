using System.Linq;
using Backend.Commons;

namespace Database.Main;

public static class TokenPriceStateMethods
{
    public static TokenPriceState Upsert(MainDbContext databaseContext, Token token, bool coinGecko,
        bool saveChanges = true)
    {
        var tokenPriceState = databaseContext.TokenPriceStates.FirstOrDefault(x => x.Token == token);

        if ( tokenPriceState == null )
        {
            tokenPriceState = new TokenPriceState
            {
                Token = token,
                COIN_GECKO = coinGecko,
                LAST_CHECK_DATE_UNIX_SECONDS = UnixSeconds.Now()
            };
            databaseContext.TokenPriceStates.Add(tokenPriceState);
        }
        else
        {
            tokenPriceState.COIN_GECKO = coinGecko;
            tokenPriceState.LAST_CHECK_DATE_UNIX_SECONDS = UnixSeconds.Now();
        }

        if ( saveChanges ) databaseContext.SaveChanges();
        return tokenPriceState;
    }
}
