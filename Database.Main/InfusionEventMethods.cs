namespace Database.Main;

public static class InfusionEventMethods
{
    public static InfusionEvent Upsert(MainDbContext databaseContext, string tokenId, string baseSymbol,
        string infusedSymbol, string infusedValue, int chainId, Event databaseEvent, bool saveChanges = true)
    {
        if ( string.IsNullOrEmpty(tokenId) || string.IsNullOrEmpty(baseSymbol) ) return null;

        //use the chain name here to get the data
        //could use id too, but who knows what can be send in the future
        var baseToken = TokenMethods.Get(databaseContext, chainId, baseSymbol);
        var infusedToken = TokenMethods.Get(databaseContext, chainId, infusedSymbol);


        var infusionEvent = new InfusionEvent
        {
            BaseToken = baseToken, 
            InfusedToken = infusedToken, 
            INFUSED_VALUE = infusedValue, 
            TOKEN_ID = tokenId,
            Event = databaseEvent
        };

        databaseContext.InfusionEvents.Add(infusionEvent);
        if ( saveChanges ) databaseContext.SaveChanges();

        return infusionEvent;
    }
}
