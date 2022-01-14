using System;
using System.Linq;
using Database.Main;
using GhostDevs.PluginEngine;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace GhostDevs.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    private void MergeSendReceiveToTransfer(int chainId)
    {
        var startTime = DateTime.Now;

        var mergedEventPairCount = 0;

        using ( var databaseContext = new MainDbContext() )
        {
            var sendEventKindId = databaseContext.EventKinds.Where(x => x.NAME == "TokenSend").Select(x => x.ID)
                .FirstOrDefault();

            if ( sendEventKindId == 0 ) return;

            var receiveEventKindId = databaseContext.EventKinds.Where(x => x.NAME == "TokenReceive").Select(x => x.ID)
                .FirstOrDefault();

            if ( receiveEventKindId == 0 ) return;

            // Searching for all receive events.
            // Then we'll search for their "send" couples, transform receive to transfer event,
            // and delete send event.

            var receiveEvents = databaseContext.Events
                .Where(x => x.ChainId == chainId && x.EventKindId == receiveEventKindId)
                .OrderBy(x => x.TIMESTAMP_UNIX_SECONDS).ThenBy(x => x.Transaction.INDEX)
                .ThenBy(x => x.INDEX) // Ensure strict events order
                .ToList();

            // We create new meta-event, if it's not available yet.
            var transferEventId = EventKindMethods.Upsert(databaseContext, chainId, "TokenTransfer");

            foreach ( var receiveEvent in receiveEvents )
            {
                // Searching for send event right before receive event.

                // 1st: Searching in the same TX:
                var sendEvent = databaseContext.Events.Where(x => x.EventKindId == sendEventKindId &&
                                                                  x.ContractId == receiveEvent.ContractId &&
                                                                  x.TOKEN_ID == receiveEvent.TOKEN_ID &&
                                                                  x.Transaction == receiveEvent.Transaction &&
                                                                  x.INDEX < receiveEvent.INDEX)
                    .OrderByDescending(x => x.INDEX)
                    .FirstOrDefault();

                if ( sendEvent == null )
                    // 2nd: Searching in the same block:
                    sendEvent = databaseContext.Events.Where(x => x.EventKindId == sendEventKindId &&
                                                                  x.ContractId == receiveEvent.ContractId &&
                                                                  x.TOKEN_ID == receiveEvent.TOKEN_ID &&
                                                                  x.Transaction.Block ==
                                                                  receiveEvent.Transaction.Block &&
                                                                  x.Transaction.INDEX < receiveEvent.Transaction.INDEX)
                        .OrderByDescending(x => x.Transaction.INDEX).ThenByDescending(x => x.INDEX)
                        .FirstOrDefault();

                if ( sendEvent == null )
                    // 3rd: Searching in older blocks:
                    sendEvent = databaseContext.Events.Where(x => x.EventKindId == sendEventKindId &&
                                                                  x.ContractId == receiveEvent.ContractId &&
                                                                  x.TOKEN_ID == receiveEvent.TOKEN_ID &&
                                                                  x.TIMESTAMP_UNIX_SECONDS <
                                                                  receiveEvent.TIMESTAMP_UNIX_SECONDS)
                        .OrderByDescending(x => x.TIMESTAMP_UNIX_SECONDS).ThenByDescending(x => x.Transaction.INDEX)
                        .ThenByDescending(x => x.INDEX)
                        .FirstOrDefault();

                if ( sendEvent != null ) // Just checking to avoid problems in case of some corruption.
                {
                    var sourceAddressId = sendEvent.AddressId;

                    EventMethods.UpdateOnEventMerge(databaseContext, receiveEvent.ID, transferEventId, sourceAddressId,
                        false);

                    databaseContext.Entry(sendEvent).State = EntityState.Deleted;

                    mergedEventPairCount++;
                }
                else
                    Log.Error(
                        "[{Name}] No corresponding send event found for receive event {ID} TOKEN_ID: {TokenID}",
                        Name, receiveEvent.ID, receiveEvent.TOKEN_ID);
            }

            if ( mergedEventPairCount > 0 ) databaseContext.SaveChanges();
        }

        var mergeTime = DateTime.Now - startTime;
        Log.Information(
            "[{Name}] Send/receive events merge took {MergeTime} sec, {MergedEventPairCount} event pairs merged",
            Name, Math.Round(mergeTime.TotalSeconds, 3), mergedEventPairCount);
    }
}
