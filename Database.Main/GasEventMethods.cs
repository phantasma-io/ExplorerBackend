using System.Numerics;
using System.Threading.Tasks;
using Backend.Commons;

namespace Database.Main;

public static class GasEventMethods
{
    public static async Task<GasEvent> UpsertAsync(MainDbContext databaseContext, string address, string price, string amount,
        Event databaseEvent, Chain chain)
    {
        if ( string.IsNullOrEmpty(address) || string.IsNullOrEmpty(price) || string.IsNullOrEmpty(amount) ) return null;

        var addressEntry = await AddressMethods.UpsertAsync(databaseContext, chain, address);

        //get KCAL token here, to find out the decimals
        var decimals = TokenMethods.GetKcalDecimals(databaseContext, chain);

        //add new price for fees amount * price / decimals
        var gasEvent = new GasEvent
        {
            Address = addressEntry,
            PRICE = price,
            AMOUNT = amount,
            FEE = Utils.ToDecimal(( BigInteger.Parse(price) * BigInteger.Parse(amount) ).ToString(), decimals),
            Event = databaseEvent
        };

        await databaseContext.GasEvents.AddAsync(gasEvent);

        return gasEvent;
    }
}
