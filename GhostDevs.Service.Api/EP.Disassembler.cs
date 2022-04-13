using System;
using System.Linq;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace GhostDevs.Service;

public partial class Endpoints
{
    [APIInfo(typeof(DisassemblerResult), "Returns the disassembled version of the Script", false, 10)]
    public DisassemblerResult Instructions([FromBody] Script script)
    {
        long totalResults;
        Instruction[] instructionArray;

        try
        {
            if ( script == null )
                throw new APIException("Unsupported value for 'script' parameter.");

            if ( !string.IsNullOrEmpty(script.script_raw) && !ArgValidation.CheckString(script.script_raw) )
                throw new APIException("Unsupported value for 'script_raw' parameter.");

            var startTime = DateTime.Now;

            var instructions = Utils.GetInstructionsFromScript(script.script_raw);


            totalResults = instructions.Count;

            instructionArray = instructions.Select(x => new Instruction
            {
                instruction = x
            }).ToArray();

            var responseTime = DateTime.Now - startTime;

            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch ( APIException )
        {
            throw;
        }
        catch ( Exception exception )
        {
            var logMessage = LogEx.Exception("Disassembler()", exception);

            throw new APIException(logMessage, exception);
        }

        return new DisassemblerResult {total_results = totalResults, Instructions = instructionArray};
    }
}
