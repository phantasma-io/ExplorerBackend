using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Service.Api.Controllers.V1;

public class InstructionsController : BaseControllerV1
{
    /// <summary>
    ///     Returns the disassembled version of the Script.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-Backend.Service.Api.DisassemblerResult'>DisassemblerResult</a>
    /// </remarks>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [HttpPost("instructions")]
    [ApiInfo(typeof(DisassemblerResult), "Returns the disassembled version of the script")]
    [ProducesResponseType(typeof(DisassemblerResult), ( int ) HttpStatusCode.OK)]
    public Task<DisassemblerResult> GetResult(
        [FromBody] Script script
    )
    {
        return Task.FromResult(Endpoints.Instructions(script));
    }
}
