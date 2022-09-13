using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using Backend.PluginEngine;
using Database.Main;
using Serilog;

namespace Backend.Blockchain;

public partial class BlockchainCommonPlugin : Plugin, IDBAccessPlugin
{
    private void MarkBurnedNfts()
    {
        var startTime = DateTime.Now;

        var markedEventCount = 0;
        var markedNftCount = 0;

        using ( var databaseContext = new MainDbContext() )
        {
            int[] chainIds = {ChainMethods.GetId(databaseContext, "main")};

            var burnEventIds = databaseContext.EventKinds
                .Where(x => chainIds.Contains(x.ChainId) && x.NAME == "TokenBurn").Select(x => x.ID).ToArray();

            foreach ( var burnEventId in burnEventIds )
            {
                var burnedTokens = databaseContext.Events
                    .Where(x => x.EventKindId == burnEventId &&
                                x.BURNED != true)
                    .Select(x => new {x.ContractId, x.TOKEN_ID}).ToList();

                foreach ( var burnedToken in burnedTokens )
                {
                    var tokenEvents = databaseContext.Events.Where(x =>
                        x.ContractId == burnedToken.ContractId && x.TOKEN_ID == burnedToken.TOKEN_ID).ToList();
                    foreach ( var tokenEvent in tokenEvents )
                    {
                        tokenEvent.BURNED = true;
                        markedEventCount++;
                    }

                    var nft = databaseContext.Nfts
                        .FirstOrDefault(x =>
                            x.ContractId == burnedToken.ContractId && x.TOKEN_ID == burnedToken.TOKEN_ID);
                    if ( nft == null ) continue;
                    nft.BURNED = true;
                    markedNftCount++;
                }
            }

            if ( markedEventCount > 0 || markedNftCount > 0 )
                try
                {
                    databaseContext.SaveChanges();
                }
                catch ( Exception ex )
                {
                    //TODO fix, cause it seems the exception text has changed
                    if ( ex.Message.Contains("Database operation expected to affect ") ||
                         ex.Message.Contains("database operation was expected to affect ") )
                    {
                        var attemptTime = DateTime.Now - startTime;
                        Log.Warning(
                            "{Name} plugin: Burned token events processing failed because some NFTs or events were deleted in another thread, attempt took {AttemptTime} sec",
                            Name, Math.Round(attemptTime.TotalSeconds, 3));
                        return;
                    }

                    // Unknown exception, throwing futher.
                    ExceptionDispatchInfo.Capture(ex).Throw();
                }
        }

        var processTime = DateTime.Now - startTime;
        Log.Information(
            "{Name} plugin: Burned token events processing took {ProcessTime} sec, {MarkedEventCount} events marked, {MarkedNftCount} NFTs marked",
            Name, Math.Round(processTime.TotalSeconds, 3), markedEventCount, markedNftCount);
    }
}
