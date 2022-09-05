using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using GhostDevs.Commons;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace GhostDevs.Service.Api;

public partial class Endpoints
{
    /// <summary>
    ///     Returns the disassembled version of the Script.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-DisassemblerResult'>DisassemblerResult</a>
    /// </remarks>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [ProducesResponseType(typeof(DisassemblerResult), ( int ) HttpStatusCode.OK)]
    [HttpPost("{script}")]
    [ApiInfo(typeof(DisassemblerResult), "Returns the disassembled version of the Script")]
    public DisassemblerResult Instructions([FromBody] Script script)
    {
        long totalResults;
        Instruction[] instructionArray;

        try
        {
            if ( script == null )
                throw new ApiParameterException("Unsupported value for 'script' parameter.");

            if ( !string.IsNullOrEmpty(script.script_raw) && !ArgValidation.CheckString(script.script_raw) )
                throw new ApiParameterException("Unsupported value for 'script_raw' parameter.");

            var startTime = DateTime.Now;

            List<string> instructions;
            try
            {
                instructions = Utils.GetInstructionsFromScript(script.script_raw);
            }
            catch ( Exception exception )
            {
                throw new ApiParameterException(exception.Message);
            }

            totalResults = instructions.Count;

            instructionArray = instructions.Select(x => new Instruction
            {
                instruction = x
            }).ToArray();

            var responseTime = DateTime.Now - startTime;

            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch ( ApiParameterException )
        {
            throw;
        }
        catch ( Exception exception )
        {
            var logMessage = LogEx.Exception("Disassembler()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new DisassemblerResult {total_results = totalResults, Instructions = instructionArray};
    }
}
