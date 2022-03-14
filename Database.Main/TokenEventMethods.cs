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

        var tokenEvent = new TokenEvent {Token = token, VALUE = value, CHAIN_NAME = chainName, Event = databaseEvent};

        databaseContext.TokenEvents.Add(tokenEvent);
        if ( saveChanges ) databaseContext.SaveChanges();

        return tokenEvent;
    }
}
