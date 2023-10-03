using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Service.Api.Controllers.V1;

public class SearchesController : BaseControllerV1
{
    /// <summary>
    ///     Search help.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-Backend.Service.Api.SearchResult'>SearchResult</a>
    /// </remarks>
    /// <param name="value">
    ///     Will be checked if it is an Address, a Block, a Chain, a Contract, an Organization, a Platform or a
    ///     Token
    /// </param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [HttpGet("searches")]
    [ApiInfo(typeof(SearchResult), "Search for chain objects", cacheDuration: 10, cacheTag: "search")]
    public Task<SearchResult> GetResults(
        // ReSharper disable InconsistentNaming
        [FromQuery] string value
        // ReSharper enable InconsistentNaming
    )
    {
        return GetSearch.Execute(
            value);
    }
}
