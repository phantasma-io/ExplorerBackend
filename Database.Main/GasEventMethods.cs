using System.Numerics;
using Backend.Commons;

namespace Database.Main;

public static class GasEventMethods
{
    public static GasEvent Upsert(MainDbContext databaseContext, string address, string price, string amount,
        Event databaseEvent, Chain chain, bool saveChanges = true)
    {
        if ( string.IsNullOrEmpty(address) || string.IsNullOrEmpty(price) || string.IsNullOrEmpty(amount) ) return null;

        var addressEntry = AddressMethods.Upsert(databaseContext, chain, address, saveChanges);

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

        databaseContext.GasEvents.Add(gasEvent);
        if ( saveChanges ) databaseContext.SaveChanges();

        return gasEvent;
    }
}
