namespace Database.Main;

public static class TokenEventMethods
{
    public static TokenEvent Upsert(MainDbContext databaseContext, string symbol, string chainName, string value,
        int chainId, Event databaseEvent, bool saveChanges = true)
    {
        if ( string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(value) ) return null;

        //use the chain name here to get the data
        //could use id too, but who knows what can be send in the future
        var chain = ChainMethods.Get(databaseContext, chainName);
        var token = TokenMethods.Get(databaseContext, chainId, symbol);

        var tokenEvent = new TokenEvent {Token = token, VALUE = value, Chain = chain, Event = databaseEvent};

        databaseContext.TokenEvents.Add(tokenEvent);
        if ( saveChanges ) databaseContext.SaveChanges();

        return tokenEvent;
    }
}
