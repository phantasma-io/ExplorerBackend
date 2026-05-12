using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Service.Api.Controllers.V1;

public class RejectedTransactionsController : BaseControllerV1
{
    /// <summary>
    ///     Returns rejected transaction candidates captured from recent RPC diagnostics.
    /// </summary>
    /// <param name="hash"><a href='#model-Backend.Service.Api.Transaction'>Transaction</a> hash</param>
    /// <param name="chain" example="main">Chain name used for canonical block verification</param>
    /// <param name="capture" example="1">Capture from RPC if not already persisted</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [HttpGet("rejected-transactions")]
    [ApiInfo(typeof(RejectedTransactionCandidateResult),
        "Returns rejected transaction candidates captured from recent RPC diagnostics.", cacheDuration: 0,
        cacheTag: "rejected-transactions")]
    public Task<RejectedTransactionCandidateResult> GetResults(
        [FromQuery] string hash = "",
        [FromQuery] string chain = "",
        [FromQuery] int capture = 1)
    {
        return GetRejectedTransactions.Execute(hash, chain, capture);
    }
}
