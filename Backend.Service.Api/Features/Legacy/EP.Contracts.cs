using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Service.Api;

public static class GetContracts
{
    private sealed class ContractPageItem
    {
        public int Id { get; init; }
        public string Symbol { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public Contract ApiContract { get; init; }
    }

    [ProducesResponseType(typeof(ContractResult), (int)HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(ContractResult), "Returns the contracts on the backend.", false, 10, cacheTag: "contracts")]
    public static async Task<ContractResult> Execute(
        // ReSharper disable InconsistentNaming
        string order_by = "id",
        string order_direction = "asc",
        int limit = 50,
        string cursor = "",
        string symbol = "",
        string hash = "",
        string q = "",
        string chain = "main",
        int with_methods = 0,
        int with_script = 0,
        int with_token = 0,
        int with_creation_event = 0
    // ReSharper enable InconsistentNaming
    )
    {
        Contract[] contractArray;
        string? nextCursor = null;
        var qTrimmed = string.IsNullOrWhiteSpace(q) ? string.Empty : q.Trim();

        try
        {
            #region ArgValidation

            if (!string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by))
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            if (!ArgValidation.CheckOrderDirection(order_direction))
                throw new ApiParameterException("Unsupported value for 'order_direction' parameter.");

            if (!ArgValidation.CheckLimit(limit, false))
                throw new ApiParameterException("Unsupported value for 'limit' parameter.");

            if (!string.IsNullOrEmpty(symbol) && !ArgValidation.CheckSymbol(symbol))
                throw new ApiParameterException("Unsupported value for 'address' parameter.");

            if (!string.IsNullOrEmpty(hash) && !ArgValidation.CheckString(hash))
                throw new ApiParameterException("Unsupported value for 'hash' parameter.");

            if (!string.IsNullOrEmpty(qTrimmed) && !ArgValidation.CheckGeneralSearch(qTrimmed))
                throw new ApiParameterException("Unsupported value for 'q' parameter.");

            if (!string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain))
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            #endregion

            var cursorToken = CursorPagination.ParseCursor(cursor);
            var sortDirection = CursorPagination.ParseSortDirection(order_direction);
            var orderBy = string.IsNullOrWhiteSpace(order_by) ? "id" : order_by;

            var orderDefinitions =
                new Dictionary<string, CursorOrderDefinition<ContractPageItem>>(StringComparer.OrdinalIgnoreCase)
                {
                    {
                        "id",
                        new CursorOrderDefinition<ContractPageItem>(
                            "id",
                            new CursorOrderSegment<ContractPageItem, int>(
                                x => x.Id,
                                value => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)))
                    },
                    {
                        "symbol",
                        new CursorOrderDefinition<ContractPageItem>(
                            "symbol",
                            new CursorOrderSegment<ContractPageItem, string>(
                                x => x.Symbol,
                                value => value))
                    },
                    {
                        "name",
                        new CursorOrderDefinition<ContractPageItem>(
                            "name",
                            new CursorOrderSegment<ContractPageItem, string>(
                                x => x.Name,
                                value => value))
                    }
                };

            if (!orderDefinitions.TryGetValue(orderBy, out var orderDefinition))
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            var startTime = DateTime.Now;

            await using MainDbContext databaseContext = new();
            var query = databaseContext.Contracts.AsQueryable().AsNoTracking();

            #region Filtering
            var qUpper = string.IsNullOrEmpty(qTrimmed) ? string.Empty : qTrimmed.ToUpperInvariant();

            if (!string.IsNullOrEmpty(qUpper))
            {
                var isHex = ArgValidation.CheckBase16(qTrimmed);
                var isFullHash = isHex && qUpper.Length >= 40;
                var isHexPartial = isHex && !isFullHash;
                var treatAsName = !isHex;

                query = query.Where(x =>
                    (isFullHash && x.HASH == qUpper) ||
                    (isHexPartial && x.HASH.Contains(qUpper)) ||
                    (treatAsName &&
                      (EF.Functions.ILike(x.SYMBOL, $"%{qTrimmed}%") || EF.Functions.ILike(x.NAME, $"%{qTrimmed}%"))));
            }

            if (!string.IsNullOrEmpty(symbol)) query = query.Where(x => x.SYMBOL.Equals(symbol.ToUpper()));

            if (!string.IsNullOrEmpty(hash)) query = query.Where(x => x.HASH.ToLower().Equals(hash.ToLower()));

            if (!string.IsNullOrEmpty(chain)) query = query.Where(x => x.Chain.NAME == chain);

            #endregion

            var pageQuery = query.Select(x => new ContractPageItem
            {
                Id = x.ID,
                Symbol = x.SYMBOL,
                Name = x.NAME,
                ApiContract = new Contract
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
                            string_event = EventPayloadMapper.ParseStringEvent(x.CreateEvent.PAYLOAD_JSON)
                        }
                        : null
                }
            });

            var cursorFiltered = CursorPagination.ApplyCursor(pageQuery, orderDefinition, sortDirection, cursorToken,
                x => x.Id);
            var orderedQuery =
                CursorPagination.ApplyOrdering(cursorFiltered, orderDefinition, sortDirection, x => x.Id);
            var page = await CursorPagination.ReadPageAsync(orderedQuery, orderDefinition, sortDirection, x => x.Id,
                limit);
            contractArray = page.Items.Select(x => x.ApiContract).ToArray();
            nextCursor = page.NextCursor;

            var responseTime = DateTime.Now - startTime;

            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch (ApiParameterException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var logMessage = LogEx.Exception("Contract()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new ContractResult
        {
            total_results = null,
            contracts = contractArray,
            next_cursor = nextCursor
        };
    }
}
