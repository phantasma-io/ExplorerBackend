using System.Globalization;
using System.Linq;

namespace Database.Main;

public static class InfusionMethods
{
    // Checks if table has entry with given Nft/key pair,
    // and adds new entry, if there's no entry available.
    public static void Upsert(MainDbContext databaseContext, InfusionEvent infusionEvent, Nft nft, string key,
        string value, Token token)
    {
        //TODO apply decimals to value
        if ( token is {FUNGIBLE: false} )
        {
            // For NFT always create new entry, if we can't find infusion with same value
            var nftInfusion =
                databaseContext.Infusions.FirstOrDefault(x => x.Nft == nft && x.KEY == key && x.VALUE == value);
            if ( nftInfusion == null )
            {
                nftInfusion = new Infusion {Nft = nft, KEY = key, VALUE = value};
                databaseContext.Infusions.Add(nftInfusion);
            }

            infusionEvent.Infusion = nftInfusion;
            return;
        }

        // Fungible infusions
        var infusion = databaseContext.Infusions.FirstOrDefault(x => x.Nft == nft && x.KEY == key);

        if ( infusion == null )
        {
            var fungibleToken = TokenMethods.Get(databaseContext, infusionEvent.Event.ChainId, key);
            infusion = new Infusion {Nft = nft, KEY = key, Token = fungibleToken, VALUE = "0"};
            databaseContext.Infusions.Add(infusion);
        }

        infusion.VALUE =
            ( decimal.Parse(infusion.VALUE) + decimal.Parse(value) ).ToString(CultureInfo.InvariantCulture);

        infusionEvent.Infusion = infusion;
    }
}
