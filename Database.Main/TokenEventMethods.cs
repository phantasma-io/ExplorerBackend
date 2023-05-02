using Backend.Commons;

namespace Database.Main;

public static class TokenEventMethods
{
    public static TokenEvent Upsert(MainDbContext databaseContext, string symbol, string chainName, string value,
        Chain chain, Event databaseEvent, bool saveChanges = true)
    {
        if ( string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(value) ) return null;

        //use the chain name here to get the data
        //maybe
        var token = TokenMethods.Get(databaseContext, chain, symbol);
        //var chainNameChain = ChainMethods.Get(databaseContext, chainName);

        var tokenEvent = new TokenEvent
        {
            Token = token,
            VALUE = Utils.ToDecimal(value, token.DECIMALS),
            VALUE_RAW = value,
            CHAIN_NAME = chainName,
            Event = databaseEvent
        };

        databaseContext.TokenEvents.Add(tokenEvent);
        if ( saveChanges ) databaseContext.SaveChanges();

        return tokenEvent;
    }
}
