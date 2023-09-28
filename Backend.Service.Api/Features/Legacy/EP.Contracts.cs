using System;
using System.Linq;
using System.Net;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Service.Api;

public partial class Endpoints
{
    [ProducesResponseType(typeof(ContractResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(ContractResult), "Returns the contracts on the backend.", false, 10, cacheTag: "contracts")]
    public static ContractResult Contracts(
        // ReSharper disable InconsistentNaming
        string order_by = "id",
        string order_direction = "asc",
        int offset = 0,
        int limit = 50,
        string symbol = "",
        string hash = "",
        string chain = "main",
        int with_methods = 0,
        int with_script = 0,
        int with_token = 0,
        int with_creation_event = 0,
        int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        long totalResults = 0;
        Contract[] contractArray;

        try
        {
            #region ArgValidation

            if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            if ( !ArgValidation.CheckOrderDirection(order_direction) )
                throw new ApiParameterException("Unsupported value for 'order_direction' parameter.");

            if ( !ArgValidation.CheckLimit(limit, false) )
                throw new ApiParameterException("Unsupported value for 'limit' parameter.");

            if ( !ArgValidation.CheckOffset(offset) )
                throw new ApiParameterException("Unsupported value for 'offset' parameter.");

            if ( !string.IsNullOrEmpty(symbol) && !ArgValidation.CheckSymbol(symbol) )
                throw new ApiParameterException("Unsupported value for 'address' parameter.");

            if ( !string.IsNullOrEmpty(hash) && !ArgValidation.CheckString(hash) )
                throw new ApiParameterException("Unsupported value for 'hash' parameter.");

            if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            #endregion

            var startTime = DateTime.Now;

            using MainDbContext databaseContext = new();
            var query = databaseContext.Contracts.AsQueryable().AsNoTracking();

            #region Filtering

            if ( !string.IsNullOrEmpty(symbol) ) query = query.Where(x => x.SYMBOL.Equals(symbol.ToUpper()));

            if ( !string.IsNullOrEmpty(hash) ) query = query.Where(x => x.HASH.ToLower().Equals(hash.ToLower()));

            if ( !string.IsNullOrEmpty(chain) ) query = query.Where(x => x.Chain.NAME == chain);

            #endregion

            // Count total number of results before adding order and limit parts of query.
            if ( with_total == 1 )
                totalResults = query.Count();

            //in case we add more to sort
            if ( order_direction == "asc" )
                query = order_by switch
                {
                    "id" => query.OrderBy(x => x.ID),
                    "symbol" => query.OrderBy(x => x.SYMBOL),
                    "name" => query.OrderBy(x => x.NAME),
                    _ => query
                };
            else
                query = order_by switch
                {
                    "id" => query.OrderByDescending(x => x.ID),
                    "symbol" => query.OrderByDescending(x => x.SYMBOL),
                    "name" => query.OrderByDescending(x => x.NAME),
                    _ => query
                };


            contractArray = query.Skip(offset).Take(limit).Select(x => new Contract
                {
                    name = x.NAME,
                    hash = x.HASH,
                    symbol = x.SYMBOL,
                    address = x.Address != null
                        ? new Address
                        {
                            address = x.Address.ADDRESS,
                            address_name = x.Address.ADDRESS_NAME
                        }
                        : null,
                    script_raw = with_script == 1 ? x.SCRIPT_RAW : null,
                    methods = with_methods == 1 && x.ContractMethod != null ? x.ContractMethod.METHODS : null,
                    token = with_token == 1 && x.Token != null
                        ? new Token
                        {
                            symbol = x.Token.SYMBOL,
                            fungible = x.Token.FUNGIBLE,
                            transferable = x.Token.TRANSFERABLE,
                            finite = x.Token.FINITE,
                            divisible = x.Token.DIVISIBLE,
                            fiat = x.Token.FIAT,
                            fuel = x.Token.FUEL,
                            swappable = x.Token.SWAPPABLE,
                            burnable = x.Token.BURNABLE,
                            stakable = x.Token.STAKABLE,
                            decimals = x.Token.DECIMALS
                        }
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
                }
            ).ToArray();

            var responseTime = DateTime.Now - startTime;

            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch ( ApiParameterException )
        {
            throw;
        }
        catch ( Exception exception )
        {
            var logMessage = LogEx.Exception("Contract()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new ContractResult {total_results = with_total == 1 ? totalResults : null, contracts = contractArray};
    }
}
