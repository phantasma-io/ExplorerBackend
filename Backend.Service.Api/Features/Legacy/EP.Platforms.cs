using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Service.Api;

public static class GetPlatforms
{
    [ProducesResponseType(typeof(PlatformResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(PlatformResult), "Returns the Platform on the backend.", false, 10)]
    public static async Task<PlatformResult> Execute(
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
            await using MainDbContext databaseContext = new();
            var query = databaseContext.Platforms.AsQueryable().AsNoTracking();

            query = query.Where(x => x.HIDDEN == false);

            if ( !string.IsNullOrEmpty(name) ) query = query.Where(x => x.NAME == name);

            if ( with_total == 1 )
                totalResults = await query.CountAsync();

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


            platformArray = await query.Skip(offset).Take(limit).Select(x => new Platform
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
                                mintable = e.Token.MINTABLE,
                                decimals = e.Token.DECIMALS,
                                current_supply = e.Token.CURRENT_SUPPLY,
                                current_supply_raw = e.Token.CURRENT_SUPPLY_RAW,
                                max_supply = e.Token.MAX_SUPPLY,
                                max_supply_raw = e.Token.MAX_SUPPLY_RAW,
                                burned_supply = e.Token.BURNED_SUPPLY,
                                burned_supply_raw = e.Token.BURNED_SUPPLY_RAW,
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
                            hash = x.CreateEvent.Contract.HASH,
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
            }).ToArrayAsync();

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
