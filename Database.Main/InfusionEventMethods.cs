using System;
using System.Linq;
using System.Threading.Tasks;
using Backend.Commons;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Database.Main;

public static class InfusionEventMethods
{
    public static async Task InsertAsync(MainDbContext databaseContext, string tokenId, string baseSymbol,
        string infusedSymbol, string infusedValue, Chain chain, Event databaseEvent)
    {
        if ( string.IsNullOrEmpty(tokenId) || string.IsNullOrEmpty(baseSymbol) ) return;

        //use the chain name here to get the data
        //could use id too, but who knows what can be send in the future
        var baseToken = await TokenMethods.GetAsync(databaseContext, chain, baseSymbol);
        var infusedToken = await TokenMethods.GetAsync(databaseContext, chain, infusedSymbol);


        var infusionEvent = new InfusionEvent
        {
            BaseToken = baseToken,
            InfusedToken = infusedToken,
            INFUSED_VALUE = Utils.ToDecimal(infusedValue, infusedToken.DECIMALS),
            INFUSED_VALUE_RAW = infusedValue,
            TOKEN_ID = tokenId,
            Event = databaseEvent
        };

        if ( databaseEvent.Nft != null )
        {
            var startTime = DateTime.Now;
            await MarkNtfInfused(databaseContext, infusedToken, infusedValue, databaseEvent.Nft);
            var updateTime = DateTime.Now - startTime;
            Log.Verbose("Marked infused processed in {Time} sec", Math.Round(updateTime.TotalSeconds, 3));
        }

        await databaseContext.InfusionEvents.AddAsync(infusionEvent);
    }


    private static async Task MarkNtfInfused(MainDbContext databaseContext, Token infusedToken, string infusedValue, Nft nft)
    {
        if ( infusedToken == null || infusedToken.FUNGIBLE ) return;

        // NFT was infused, we should mark it as infused.
        var infusedNft = await databaseContext.Nfts.FirstOrDefaultAsync(x => x.TOKEN_ID == infusedValue) ??
                         DbHelper.GetTracked<Nft>(databaseContext).FirstOrDefault(x => x.TOKEN_ID == infusedValue);

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
