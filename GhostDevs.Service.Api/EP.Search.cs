using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace GhostDevs.Service;

public partial class Endpoints
{
    /// <summary>
    ///     Search help.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-SearchResult'>SearchResult</a>
    /// </remarks>
    /// <param name="value">
    ///     Will be checked if it is an Address, a Block, a Chain, a Contract, an Organization, a Platform or a
    ///     Token
    /// </param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [ProducesResponseType(typeof(SearchResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(SearchResult), "Returns the ValidatorKinds on the backend.", false, 10)]
    public SearchResult Searches(
        // ReSharper disable InconsistentNaming
        [Required] string value
        // ReSharper enable InconsistentNaming
    )
    {
        var searchList = new List<Search>();

        try
        {
            if ( string.IsNullOrEmpty(value) || !ArgValidation.CheckString(value) || value.Length < 3 )
                throw new ApiParameterException("Unsupported value for 'value' parameter.");

            var startTime = DateTime.Now;
            using MainDbContext databaseContext = new();

            var searches = new List<Tuple<string, string>>
            {
                new("addresses", "address"),
                new("blocks", "hash"),
                new("chains", "chain"),
                new("contracts", "hash"),
                new("organizations", "organization_name"),
                new("platforms", "name"),
                new("tokens", "symbol")
            };

            foreach ( var (endpoint, parameter) in searches )
            {
                var anyHit = endpoint switch
                {
                    "addresses" => databaseContext.Addresses.Any(x => x.ADDRESS == value),
                    "blocks" => databaseContext.Blocks.Any(x => x.HASH == value),
                    "chains" => databaseContext.Chains.Any(x => x.NAME == value),
                    "contracts" => databaseContext.Contracts.Any(x => x.HASH == value),
                    "organizations" => databaseContext.Organizations.Any(x => x.NAME == value),
                    "platforms" => databaseContext.Platforms.Any(x => x.NAME == value),
                    "tokens" => databaseContext.Tokens.Any(x => x.SYMBOL == value),
                    _ => false
                };

                searchList.Add(new Search {endpoint_name = endpoint, endpoint_parameter = parameter, found = anyHit});
            }


            var responseTime = DateTime.Now - startTime;

            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch ( ApiParameterException )
        {
            throw;
        }
        catch ( Exception exception )
        {
            var logMessage = LogEx.Exception("Search()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new SearchResult {result = searchList.ToArray()};
    }
}
