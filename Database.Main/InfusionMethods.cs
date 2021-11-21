using System;
using System.Linq;

namespace Database.Main
{
    public static class InfusionMethods
    {
        // Checks if table has entry with given Nft/key pair,
        // and adds new entry, if there's no entry available.
        public static void Upsert(MainDbContext databaseContext, Event infusionEvent, Nft nft, string key, string value)
        {
            // Trying to get token.
            var token = TokenMethods.Get(databaseContext, infusionEvent.ChainId, infusionEvent.InfusedSymbol.SYMBOL);
            if (token != null && token.FUNGIBLE == false)
            {
                // For NFT always create new entry, if we can't find infusion with same value
                var nftInfusion = databaseContext.Infusions.Where(x => x.Nft == nft && x.KEY.ToUpper() == key.ToUpper() && x.VALUE == value).FirstOrDefault();
                if (nftInfusion == null)
                {
                    nftInfusion = new Infusion { Nft = nft, KEY = key, VALUE = value };
                    databaseContext.Infusions.Add(nftInfusion);
                }

                infusionEvent.Infusion = nftInfusion;
                return;
            }

            // Fungible infusions
            var infusion = databaseContext.Infusions.Where(x => x.Nft == nft && x.KEY.ToUpper() == key.ToUpper()).FirstOrDefault();
            if (infusion == null)
            {
                var fungibleToken = TokenMethods.UpsertWOSave(databaseContext, infusionEvent.ChainId, key);
                infusion = new Infusion { Nft = nft, KEY = key, Token = fungibleToken };
                databaseContext.Infusions.Add(infusion);

                infusion.VALUE = "0";
            }

            infusion.VALUE = (Decimal.Parse(infusion.VALUE) + Decimal.Parse(value)).ToString();

            infusionEvent.Infusion = infusion;
        }
    }
}