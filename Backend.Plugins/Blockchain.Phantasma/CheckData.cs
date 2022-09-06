using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Database.Main;
using Backend.PluginEngine;
using Serilog;

namespace Backend.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    private void CheckData(int chainId)
    {
        MainDbContext databaseContext = new();
        CheckChainData(databaseContext, chainId);
        CheckTransactionData(databaseContext);
        CheckEventData(databaseContext);

        databaseContext.SaveChanges();
    }


    private void CheckChainData(MainDbContext databaseContext, int chainId)
    {
        var startTime = DateTime.Now;
        //check if we got some orphan data, it should not happen, but still it could happen if we crash
        var highestBlock = BlockMethods.GetHighestBlock(databaseContext, chainId);
        var lastProcessedBlock = ChainMethods.GetLastProcessedBlock(databaseContext, chainId);


        var changesMade = false;
        //check if block.max(id) > chain.height
        if ( highestBlock != null )
        {
            Log.Verbose("[{Name}] Chain Height processed {Height}, Block Height {Block}", Name, highestBlock.HEIGHT,
                lastProcessedBlock);
            if ( BigInteger.Parse(highestBlock.HEIGHT) > lastProcessedBlock )
            {
                //delete entry
                Log.Warning("[{Name}] found in BlockTable {Block}, ChainTable {Chain}, delete the Info", Name,
                    highestBlock.HEIGHT, lastProcessedBlock);
                databaseContext.Blocks.Remove(highestBlock);
                databaseContext.SaveChanges();
                changesMade = true;
            }
        }


        if ( changesMade ) databaseContext.SaveChanges();
        var processTime = DateTime.Now - startTime;
        Log.Information(
            "[{Name}] Checking Block took {CheckTime} sec, made changes {Really}",
            Name, Math.Round(processTime.TotalSeconds, 3), changesMade);
    }


    private void CheckTransactionData(MainDbContext databaseContext)
    {
        //get max id, check blockid, if it returns null, remove entry
        var startTime = DateTime.Now;
        var transactions = new List<Transaction>();
        var count = 0;
        while ( true )
        {
            //var transaction = TransactionMethods.GetHighestId(databaseContext);
            var transaction = TransactionMethods.GetNextId(databaseContext, count);
            if ( transaction == null ) break;

            var block = BlockMethods.Get(databaseContext, transaction.BlockId);
            Log.Verbose("[{Name}] Transaction Id {ID} with BlockId {Bid}, Block is null {Block}", Name, transaction.ID,
                transaction.BlockId, block == null);
            if ( block != null ) break;
            transactions.Add(transaction);
            count++;
        }

        if ( !transactions.Any() ) return;
        Log.Warning("[{Name}] have to remove {Count} Transactions, because the Block could not be found", Name,
            transactions.Count);

        databaseContext.Transactions.RemoveRange(transactions);
        databaseContext.SaveChanges();

        var processTime = DateTime.Now - startTime;
        Log.Information("[{Name}] Checking Transaction took {CheckTime} sec", Name,
            Math.Round(processTime.TotalSeconds, 3));
    }


    private void CheckEventData(MainDbContext databaseContext)
    {
        var startTime = DateTime.Now;
        var events = new List<Event>();
        var count = 0;
        while ( true )
        {
            var e = EventMethods.GetNextId(databaseContext, count);
            if ( e == null ) break;
            var transaction = TransactionMethods.GetById(databaseContext, e.TransactionId);
            Log.Verbose("[{Name}] Event Id {ID} with TransactionId {Tid}, Transaction is null {Transaction}", Name,
                e.ID, e.TransactionId, transaction == null);

            if ( transaction != null && transaction.ID != 0 ) break;
            events.Add(e);
            count++;
        }


        if ( !events.Any() ) return;
        Log.Warning("[{Name}] have to remove {Count} Events, because the Transaction could not be found", Name,
            events.Count);
        databaseContext.Events.RemoveRange(events);
        databaseContext.SaveChanges();

        var processTime = DateTime.Now - startTime;
        Log.Information("[{Name}] Checking Events took {CheckTime} sec", Name,
            Math.Round(processTime.TotalSeconds, 3));
    }
}
