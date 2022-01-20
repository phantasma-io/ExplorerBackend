using System.Globalization;
using System.Linq;

namespace Database.Main;

public static class InfusionMethods
{
    // Checks if table has entry with given Nft/key pair,
    // and adds new entry, if there's no entry available.
    public static void Upsert(MainDbContext databaseContext, Event infusionEvent, Nft nft, string key, string value)
    {
        // Trying to get token.
        var token = TokenMethods.Get(databaseContext, infusionEvent.ChainId, infusionEvent.InfusedSymbol.SYMBOL);
        if ( token is {FUNGIBLE: false} )
        {
            // For NFT always create new entry, if we can't find infusion with same value
            var nftInfusion = databaseContext.Infusions.FirstOrDefault(x =>
                x.Nft == nft && string.Equals(x.KEY.ToUpper(), key.ToUpper()) && x.VALUE == value);
            if ( nftInfusion == null )
            {
                nftInfusion = new Infusion {Nft = nft, KEY = key, VALUE = value};
                databaseContext.Infusions.Add(nftInfusion);
            }

            infusionEvent.Infusion = nftInfusion;
            return;
        }

        // Fungible infusions
        var infusion = databaseContext.Infusions
            .FirstOrDefault(x => x.Nft == nft &&
                                 string.Equals(x.KEY.ToUpper(), key.ToUpper()));
        if ( infusion == null )
        {
            var fungibleToken = TokenMethods.Get(databaseContext, infusionEvent.ChainId, key);
            infusion = new Infusion {Nft = nft, KEY = key, Token = fungibleToken};
            databaseContext.Infusions.Add(infusion);

            infusion.VALUE = "0";
        }

        infusion.VALUE =
            ( decimal.Parse(infusion.VALUE) + decimal.Parse(value) ).ToString(CultureInfo.InvariantCulture);

        infusionEvent.Infusion = infusion;
    }
}
