using Database.Main;
using GhostDevs.PluginEngine;
using Serilog;
using System;
using System.Linq;

namespace GhostDevs.Blockchain
{
    public partial class BlockchainCommonPlugin : Plugin, IDBAccessPlugin
    {
        public void MarkBurnedNfts()
        {
            DateTime startTime = DateTime.Now;

            var markedEventCount = 0;
            var markedNftCount = 0;

            using (var databaseContext = new MainDbContext())
            {
                int[] chainIds =
                    {
                    ChainMethods.GetId(databaseContext, "main")
                    };

                var burnEventIds = databaseContext.EventKinds.Where(x => chainIds.Contains(x.ChainId) && x.NAME == "TokenBurn").Select(x => x.ID).ToArray();

                foreach (var burnEventId in burnEventIds)
                {
                    var burnedTokens = databaseContext.Events
                        .Where(x => x.EventKindId == burnEventId &&
                            x.BURNED != true)
                        .Select(x => new { x.ContractId, x.TOKEN_ID }).ToList();

                    foreach (var burnedToken in burnedTokens)
                    {
                        var tokenEvents = databaseContext.Events.Where(x => x.ContractId == burnedToken.ContractId && x.TOKEN_ID == burnedToken.TOKEN_ID).ToList();
                        foreach (var tokenEvent in tokenEvents)
                        {
                            tokenEvent.BURNED = true;
                            markedEventCount++;
                        }

                        var nft = databaseContext.Nfts.Where(x => x.ContractId == burnedToken.ContractId && x.TOKEN_ID == burnedToken.TOKEN_ID).FirstOrDefault();
                        if (nft != null)
                        {
                            nft.BURNED = true;
                            markedNftCount++;
                        }
                    }
                }

                if (markedEventCount > 0 || markedNftCount > 0)
                {
                    try
                    {
                        databaseContext.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.Contains("Database operation expected to affect "))
                        {
                            TimeSpan attemptTime = DateTime.Now - startTime;
                            Log.Warning($"{Name} plugin: Burned token events processing failed because some NFTs or events were deleted in another thread, attempt took {Math.Round(attemptTime.TotalSeconds, 3)} sec");
                            return;
                        }
                        else
                        {
                            // Unknown exception, throwing futher.
                            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
                        }
                    }
                }
            }

            TimeSpan processTime = DateTime.Now - startTime;
            Log.Information($"{Name} plugin: Burned token events processing took {Math.Round(processTime.TotalSeconds, 3)} sec, {markedEventCount} events marked, {markedNftCount} NFTs marked");
        }
    }
}
