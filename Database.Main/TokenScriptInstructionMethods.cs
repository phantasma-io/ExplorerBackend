using System.Collections.Generic;
using System.Linq;

namespace Database.Main;

public static class ScriptInstructionMethods
{
    public static int Upsert(MainDbContext databaseContext, int tokenId, List<string> instructions,
        bool saveChanges = true)
    {
        if ( instructions == null ) return 0;

        var token = TokenMethods.Get(databaseContext, tokenId);
        //for now remove all the data for the token id we have
        databaseContext.TokenScriptInstructions.RemoveRange(
            databaseContext.TokenScriptInstructions.Where(x => x.TokenId == tokenId));

        var idx = 0;
        foreach ( var instruction in instructions )
        {
            var tokenScriptInstruction = new TokenScriptInstruction
            {
                Token = token,
                INDEX = idx,
                INSTRUCTION = instruction
            };
            idx++;
            databaseContext.TokenScriptInstructions.Add(tokenScriptInstruction);
        }

        var instructionCount = instructions.Count;

        if ( saveChanges && instructionCount > 0 ) databaseContext.SaveChanges();

        return instructionCount;
    }
}
