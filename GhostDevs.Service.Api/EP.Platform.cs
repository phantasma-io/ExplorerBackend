using System;
using System.Linq;
using System.Net;
using Database.Main;
using GhostDevs.Commons;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace GhostDevs.Service.Api;

public partial class Endpoints
{
    //TODO change order_by and order_direction maybe to enum
    /// <summary>
    ///     Returns the Platform on the backend.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-PlatformResult'>PlatformResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are id or name</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="name" example="neo">Platform name</param>
    /// <param name="with_external" example="0">Return Data with <a href='#model-External'>External</a></param>
    /// <param name="with_interops" example="0">Return Data with <a href='#model-PlatformInterop'>Interops</a></param>
    /// <param name="with_token" example="0">Return Data with <a href='#model-Token'>Token</a></param>
    /// <param name="with_creation_event" example="0">Return data with <a href='#model-Event'>Event</a> of the creation</param>
    /// <param name="with_total" example="0">Returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [ProducesResponseType(typeof(PlatformResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(PlatformResult), "Returns the Platform on the backend.", false, 10)]
    public PlatformResult Platforms(
        // ReSharper disable InconsistentNaming
        string order_by = "id",
        string order_direction = "asc",
        int offset = 0,
        int limit = 50,
        string name = "",
        int with_external = 0,
        int with_interops = 0,
        int with_token = 0,
        int with_creation_event = 0,
        int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        long totalResults = 0;
        Platform[] platformArray;

        try
        {
            if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            if ( !ArgValidation.CheckOrderDirection(order_direction) )
                throw new ApiParameterException("Unsupported value for 'order_direction' parameter.");

            if ( !ArgValidation.CheckLimit(limit, false) )
                throw new ApiParameterException("Unsupported value for 'limit' parameter.");

            if ( !ArgValidation.CheckOffset(offset) )
                throw new ApiParameterException("Unsupported value for 'offset' parameter.");

            if ( !string.IsNullOrEmpty(name) && !ArgValidation.CheckString(name) )
                throw new ApiParameterException("Unsupported value for 'name' parameter.");

            var startTime = DateTime.Now;
            using MainDbContext databaseContext = new();
            var query = databaseContext.Platforms.AsQueryable().AsNoTracking();

            query = query.Where(x => x.HIDDEN == false);

            if ( !string.IsNullOrEmpty(name) ) query = query.Where(x => x.NAME == name);

            if ( with_total == 1 )
                totalResults = query.Count();

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


            platformArray = query.Skip(offset).Take(limit).Select(x => new Platform
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
                platform_interops = with_interops == 1 && x.PlatformInterops != null
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
                platform_tokens = with_token == 1 && x.PlatformTokens != null
                    ? x.PlatformTokens.Select(t => new PlatformToken
                    {
                        name = t.NAME
                    }).ToArray()
                    : null,
                create_event = with_creation_event == 1 && x.CreateEvent != null
                    ? new Event
                    {
                        event_id = x.ID,
                        chain = x.CreateEvent.Chain.NAME.ToLower(),
                        date = x.CreateEvent.TIMESTAMP_UNIX_SECONDS.ToString(),
                        block_hash = x.CreateEvent.Transaction.Block.HASH,
                        transaction_hash = x.CreateEvent.Transaction.HASH,
                        token_id = x.CreateEvent.TOKEN_ID,
                        event_kind = x.CreateEvent.EventKind.NAME,
                        address = x.CreateEvent.Address.ADDRESS,
                        address_name = x.CreateEvent.Address.ADDRESS_NAME,
                        contract = new Contract
                        {
                            name = x.CreateEvent.Contract.NAME,
                            hash = ContractMethods.Prepend0x(x.CreateEvent.Contract.HASH, x.CreateEvent.Chain.NAME),
                            symbol = x.CreateEvent.Contract.SYMBOL
                        },
                        string_event = x.CreateEvent.StringEvent != null
                            ? new StringEvent
                            {
                                string_value = x.CreateEvent.StringEvent.STRING_VALUE
                            }
                            : null
                    }
                    : null
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
            var logMessage = LogEx.Exception("Platform()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new PlatformResult {total_results = with_total == 1 ? totalResults : null, platforms = platformArray};
    }
}
