using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Service.Api;

public static class GetSearch
{
    [ProducesResponseType(typeof(SearchResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(SearchResult), "Returns the ValidatorKinds on the backend.", false, 10)]
    public static async Task<SearchResult> Execute(
        // ReSharper disable InconsistentNaming
        [Required] string value
        // ReSharper enable InconsistentNaming
    )
    {
        var searchList = new List<Search>();

        try
        {
            if ( string.IsNullOrEmpty(value) || !ArgValidation.CheckSearch(value) || value.Length < 3 )
                throw new ApiParameterException("Unsupported value for 'value' parameter.");

            var startTime = DateTime.Now;
            await using MainDbContext databaseContext = new();

            var searches = new List<Tuple<string, string>>
            {
                new("addresses", "address"),
                new("blocks", "hash"),
                new("chains", "chain"),
                new("contracts", "hash"),
                new("organizations", "organization_name"),
                new("platforms", "name"),
                new("tokens", "symbol"),
                new("transactions", "hash")
            };

            foreach ( var (endpoint, parameter) in searches )
            {
                var anyHit = endpoint switch
                {
                    "addresses" => databaseContext.Addresses.AsNoTracking().Any(x => x.ADDRESS == value || x.USER_NAME == value ||  x.ADDRESS_NAME == value),
                    "blocks" => databaseContext.Blocks.AsNoTracking().Any(x => x.HASH == value),
                    "chains" => databaseContext.Chains.AsNoTracking().Any(x => x.NAME == value),
                    "contracts" => databaseContext.Contracts.AsNoTracking()
                        .Any(x => x.HASH.ToLower().Equals(value.ToLower())),
                    "organizations" => databaseContext.Organizations.AsNoTracking().Any(x => x.NAME == value),
                    "platforms" => databaseContext.Platforms.AsNoTracking().Any(x => x.NAME == value),
                    "tokens" => databaseContext.Tokens.AsNoTracking()
                        .Any(x => x.SYMBOL.ToLower().Equals(value.ToLower())),
                    "transactions" => databaseContext.Transactions.AsNoTracking().Any(x => x.HASH == value),
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
