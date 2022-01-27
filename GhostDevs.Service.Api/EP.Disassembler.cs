using System;
using System.Linq;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Serilog;

namespace GhostDevs.Service;

public partial class Endpoints
{
    [APIInfo(typeof(DisassemblerResult), "Returns the disassembled version of the Script", false, 10)]
    public DisassemblerResult Instructions([APIParameter("script in raw version", "string")] string script_raw = "")
    {
        long totalResults;
        Instruction[] instructionArray;


        try
        {
            if ( !string.IsNullOrEmpty(script_raw) && !ArgValidation.CheckString(script_raw) )
                throw new APIException("Unsupported value for 'script_raw' parameter.");

            var startTime = DateTime.Now;

            var instructions = Utils.GetInstructionsFromScript(script_raw);


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
