using System.Threading.Tasks;
using Backend.Commons;

namespace Database.Main;

public static class TokenEventMethods
{
    public static async Task UpsertAsync(MainDbContext databaseContext, string symbol, string chainName, string value,
        Chain chain, Event databaseEvent)
    {
        if ( string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(value) ) return;

        //use the chain name here to get the data
        //maybe
        var token = await TokenMethods.GetAsync(databaseContext, chain, symbol);
        //var chainNameChain = ChainMethods.Get(databaseContext, chainName);

        var tokenEvent = new TokenEvent
        {
            Token = token,
            VALUE = Utils.ToDecimal(value, token.DECIMALS),
            VALUE_RAW = value,
            CHAIN_NAME = chainName,
            Event = databaseEvent
        };

        await databaseContext.TokenEvents.AddAsync(tokenEvent);
    }
}
