using System;
using System.Linq;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Serilog;
using Address = GhostDevs.Service.ApiResults.Address;
using External = GhostDevs.Service.ApiResults.External;
using Platform = GhostDevs.Service.ApiResults.Platform;
using PlatformInterop = GhostDevs.Service.ApiResults.PlatformInterop;
using PlatformToken = GhostDevs.Service.ApiResults.PlatformToken;
using Token = GhostDevs.Service.ApiResults.Token;

namespace GhostDevs.Service;

public partial class Endpoints
{
    [APIInfo(typeof(PlatformResult), "Returns the token on the backend.", false, 10)]
    public PlatformResult Platforms(
        [APIParameter("Order by [id, name]", "string")]
        string order_by = "id",
        [APIParameter("Order direction [asc, desc]", "string")]
        string order_direction = "asc",
        [APIParameter("Offset", "integer")] int offset = 0,
        [APIParameter("Limit", "integer")] int limit = 50,
        [APIParameter("name", "string")] string name = "",
        [APIParameter("Return with external", "integer")]
        int with_external = 0,
        [APIParameter("Return with interops", "integer")]
        int with_interops = 0,
        [APIParameter("Return with token", "integer")]
        int with_token = 0
    )
    {
        Platform[] platformArray;
        using ( var databaseContext = new MainDbContext() )
        {
            try
            {
                if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                    throw new APIException("Unsupported value for 'order_by' parameter.");

                if ( !ArgValidation.CheckOrderDirection(order_direction) )
                    throw new APIException("Unsupported value for 'order_direction' parameter.");

                if ( !ArgValidation.CheckLimit(limit) )
                    throw new APIException("Unsupported value for 'limit' parameter.");

                if ( !string.IsNullOrEmpty(name) && !ArgValidation.CheckString(name) )
                    throw new APIException("Unsupported value for 'name' parameter.");

                var startTime = DateTime.Now;

                var query = databaseContext.Platforms.AsQueryable();

                if ( !string.IsNullOrEmpty(name) )
                    query = query.Where(x => string.Equals(x.NAME.ToUpper(), name.ToUpper()));

                //in case we add more to sort
                if ( order_direction == "asc" )
                    query = order_by switch
                    {
                        "id" => query.OrderBy(x => x.ID),
                        "name" => query.OrderBy(x => x.NAME),
                        _ => query
                    };
                else
                    query = order_by switch
                    {
                        "id" => query.OrderByDescending(x => x.ID),
                        "name" => query.OrderByDescending(x => x.NAME),
                        _ => query
                    };

                var queryResults = query.Skip(offset).Take(limit).ToList();

                platformArray = queryResults.Select(x => new Platform
                {
                    name = x.NAME,
                    chain = x.CHAIN,
                    fuel = x.FUEL,
                    externals = with_external == 1 && x.Externals != null
                        ? x.Externals.Select(e => new External
                        {
                            hash = e.HASH,
                            token = e.Token != null
                                ? new Token
                                {
                                    symbol = e.Token.SYMBOL,
                                    fungible = e.Token.FUNGIBLE,
                                    transferable = e.Token.TRANSFERABLE,
                                    finite = e.Token.FINITE,
                                    divisible = e.Token.DIVISIBLE,
                                    fiat = e.Token.FIAT,
                                    fuel = e.Token.FUEL,
                                    swappable = e.Token.SWAPPABLE,
                                    burnable = e.Token.BURNABLE,
                                    stakable = e.Token.STAKABLE,
                                    decimals = e.Token.DECIMALS,
                                    current_supply = e.Token.CURRENT_SUPPLY,
                                    max_supply = e.Token.MAX_SUPPLY,
                                    burned_supply = e.Token.BURNED_SUPPLY,
                                    script_raw = e.Token.SCRIPT_RAW
                                }
                                : null
                        }).ToArray()
                        : null,
                    platformInterops = with_interops == 1 && x.PlatformInterops != null
                        ? x.PlatformInterops.Select(i => new PlatformInterop
                        {
                            external_address = i.EXTERNAL,
                            local_address = i.LocalAddress != null
                                ? new Address
                                {
                                    address = i.LocalAddress.ADDRESS,
                                    address_name = i.LocalAddress.ADDRESS_NAME
                                }
                                : null
                        }).ToArray()
                        : null,
                    platformTokens = with_token == 1 && x.PlatformTokens != null
                        ? x.PlatformTokens.Select(t => new PlatformToken
                        {
                            name = t.NAME
                        }).ToArray()
                        : null
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
                var logMessage = LogEx.Exception("Organization()", exception);

                throw new APIException(logMessage, exception);
            }
        }

        return new PlatformResult {platforms = platformArray};
    }
}
