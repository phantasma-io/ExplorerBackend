using System.Collections.Generic;
using System.Linq;

namespace Database.Main;

public static class TransactionScriptInstructionMethods
{
    //change to id
    public static int Upsert(MainDbContext databaseContext, Transaction transaction, List<string> instructions,
        bool saveChanges = true)
    {
        if ( instructions == null ) return 0;

        //for now remove all the data for the token id we have
        databaseContext.TransactionScriptInstructions.RemoveRange(
            databaseContext.TransactionScriptInstructions.Where(x => x.TransactionId == transaction.ID));

        var idx = 0;
        foreach ( var instruction in instructions )
        {
            var transactionScriptInstruction = new TransactionScriptInstruction
            {
                Transaction = transaction,
                INDEX = idx,
                INSTRUCTION = instruction
            };
            idx++;
            databaseContext.TransactionScriptInstructions.Add(transactionScriptInstruction);
        }

        var instructionCount = instructions.Count;

        if ( saveChanges && instructionCount > 0 ) databaseContext.SaveChanges();

        return instructionCount;
    }
}
