using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Service.Api.Controllers.V1;

public class NftsController : BaseControllerV1
{
    //TODO change order_by and order_direction maybe to enum
    /// <summary>
    ///     Returns NFTs available on Phantasma blockchain.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-Backend.Service.Api.NftsResult'>SeriesResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are id or mint_date</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="cursor" example="eyJvcmRlcl9ieSI6ImlkIn0">pagination cursor</param>
    /// <param name="creator">Address of asset creator</param>
    /// <param name="owner">Address of asset owner</param>
    /// <param name="contract_hash" example="SOUL">Token contract hash</param>
    /// <param name="name">Asset name/description filter (partial match)</param>
    /// <param name="q" example="SOUL">Universal search filter</param>
    /// <param name="chain" example="main">Chain name</param>
    /// <param name="symbol" example="TTRS"></param>
    /// <param name="token_id">Token ID</param>
    /// <param name="series_id">Series ID</param>
    /// <param name="status" example="all">Infusion status (all/active/infused)</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [HttpGet("nfts")]
    [ApiInfo(typeof(NftsResult), "Returns nfts available on the chain", cacheDuration: 10, cacheTag: "nfts")]
    public Task<NftsResult> GetResults(
        // ReSharper disable InconsistentNaming
        [FromQuery] string order_by = "mint_date",
        [FromQuery] string order_direction = "asc",
        [FromQuery] int limit = 50,
        [FromQuery] string cursor = "",
        [FromQuery] string creator = "",
        [FromQuery] string owner = "",
        [FromQuery] string contract_hash = "",
        [FromQuery] string name = "",
        [FromQuery] string q = "",
        [FromQuery] string chain = "main",
        [FromQuery] string symbol = "",
        [FromQuery] string token_id = "",
        [FromQuery] string series_id = "",
        [FromQuery] string status = "all"
    // ReSharper enable InconsistentNaming
    )
    {
        return GetNfts.Execute(
            order_by,
            order_direction,
            limit,
            cursor,
            creator,
            owner,
            contract_hash,
            name,
            q,
            chain,
            symbol,
            token_id,
            series_id,
            status);
    }
}
