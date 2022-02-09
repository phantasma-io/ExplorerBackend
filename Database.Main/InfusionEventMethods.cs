using System;
using System.Linq;
using Serilog;

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

        if ( databaseEvent.Nft != null )
        {
            var startTime = DateTime.Now;
            MarkNtfInfused(databaseContext, infusedToken, infusedValue, databaseEvent.Nft);
            var updateTime = DateTime.Now - startTime;
            Log.Verbose("Marked infused processed in {Time} sec", Math.Round(updateTime.TotalSeconds, 3));
        }

        databaseContext.InfusionEvents.Add(infusionEvent);
        if ( saveChanges ) databaseContext.SaveChanges();

        return infusionEvent;
    }


    private static void MarkNtfInfused(MainDbContext databaseContext, Token infusedToken, string infusedValue, Nft nft)
    {
        if ( infusedToken == null || infusedToken.FUNGIBLE ) return;

        // NFT was infused, we should mark it as infused.
        var infusedNft = databaseContext.Nfts.FirstOrDefault(x => x.TOKEN_ID == infusedValue);
        if ( infusedNft == null )
            Log.Warning("NFT infused, could not find a Nft for Value {Infused}, Symbol {Symbol}", infusedValue,
                infusedToken.SYMBOL);
        else
        {
            infusedNft.InfusedInto = nft;
            Log.Information("NFT infused: {InfusedNft} into NFT {Nft}", infusedNft.TOKEN_ID, nft.TOKEN_ID);
        }
    }
}
