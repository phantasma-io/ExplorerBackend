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
using CommonsUtils = Backend.Commons.Utils;

namespace Backend.Service.Api;

public static class GetTokens
{
    private sealed class TokenPageItem
    {
        public int Id { get; init; }
        public string Symbol { get; init; } = string.Empty;
        public Token ApiToken { get; init; }
    }

    [ProducesResponseType(typeof(TokenResult), (int)HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(TokenResult), "Returns the token on the backend.", false, 10)]
    public static async Task<TokenResult> Execute(
        // ReSharper disable InconsistentNaming
        string order_by = "id",
        string order_direction = "asc",
        int limit = 50,
        string cursor = "",
        string symbol = "",
        string q = "",
        string chain = "main",
        int with_price = 0,
        int with_creation_event = 0,
        int with_logo = 0
    // ReSharper enable InconsistentNaming
    )
    {
        Token[] tokenArray;
        string? nextCursor = null;
        var qTrimmed = string.IsNullOrWhiteSpace(q) ? string.Empty : q.Trim();

        try
        {
            if (!string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by))
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            if (!ArgValidation.CheckOrderDirection(order_direction))
                throw new ApiParameterException("Unsupported value for 'order_direction' parameter.");

            if (!ArgValidation.CheckLimit(limit, false))
                throw new ApiParameterException("Unsupported value for 'limit' parameter.");

            if (!string.IsNullOrEmpty(symbol) && !ArgValidation.CheckSymbol(symbol))
                throw new ApiParameterException("Unsupported value for 'address' parameter.");

            if (!string.IsNullOrEmpty(qTrimmed) && !ArgValidation.CheckGeneralSearch(qTrimmed))
                throw new ApiParameterException("Unsupported value for 'q' parameter.");

            if (!string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain))
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            var cursorToken = CursorPagination.ParseCursor(cursor);
            var sortDirection = CursorPagination.ParseSortDirection(order_direction);
            var orderBy = string.IsNullOrWhiteSpace(order_by) ? "id" : order_by;

            var orderDefinitions =
                new Dictionary<string, CursorOrderDefinition<TokenPageItem>>(StringComparer.OrdinalIgnoreCase)
                {
                    {
                        "id",
                        new CursorOrderDefinition<TokenPageItem>(
                            "id",
                            new CursorOrderSegment<TokenPageItem, int>(
                                x => x.Id,
                                value => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)))
                    },
                    {
                        "symbol",
                        new CursorOrderDefinition<TokenPageItem>(
                            "symbol",
                            new CursorOrderSegment<TokenPageItem, string>(
                                x => x.Symbol,
                                value => value))
                    }
                };

            if (!orderDefinitions.TryGetValue(orderBy, out var orderDefinition))
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            var startTime = DateTime.Now;
            await using MainDbContext databaseContext = new();
            var query = databaseContext.Tokens.AsQueryable().AsNoTracking();

            var qUpper = string.IsNullOrEmpty(qTrimmed) ? string.Empty : qTrimmed.ToUpperInvariant();

            if (!string.IsNullOrEmpty(qUpper))
            {
                query = query.Where(x =>
                    EF.Functions.ILike(x.SYMBOL, $"%{qTrimmed}%") ||
                    EF.Functions.ILike(x.NAME, $"%{qTrimmed}%"));
            }

            if (!string.IsNullOrEmpty(symbol)) query = query.Where(x => x.SYMBOL == symbol.ToUpper());

            if (!string.IsNullOrEmpty(chain)) query = query.Where(x => x.Chain.NAME == chain);

            var pageQuery = query.Select(x => new TokenPageItem
            {
                Id = x.ID,
                Symbol = x.SYMBOL,
                ApiToken = new Token
                {
                    symbol = x.SYMBOL,
                    name = x.NAME,
                    fungible = x.FUNGIBLE,
                    transferable = x.TRANSFERABLE,
                    finite = x.FINITE,
                    divisible = x.DIVISIBLE,
                    fiat = x.FIAT,
                    fuel = x.FUEL,
                    swappable = x.SWAPPABLE,
                    burnable = x.BURNABLE,
                    stakable = x.STAKABLE,
                    mintable = x.MINTABLE,
                    decimals = x.DECIMALS,
                    current_supply = x.CURRENT_SUPPLY,
                    current_supply_raw = x.CURRENT_SUPPLY_RAW,
                    max_supply = x.MAX_SUPPLY,
                    max_supply_raw = x.MAX_SUPPLY_RAW,
                    burned_supply = x.BURNED_SUPPLY,
                    burned_supply_raw = x.BURNED_SUPPLY_RAW,
                    script_raw = x.SCRIPT_RAW,
                    price = with_price == 1
                        ? new Price
                        {
                            usd = x.PRICE_USD != 0 ? x.PRICE_USD : null
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
                        : null,
                    token_logos = with_logo == 1 && x.TokenLogos != null
                        ? x.TokenLogos.Select(t => new TokenLogo
                        {
                            type = t.TokenLogoType.NAME,
                            url = t.URL
                        }).ToArray()
                        : null
                }
            });

            var cursorFiltered = CursorPagination.ApplyCursor(pageQuery, orderDefinition, sortDirection, cursorToken,
                x => x.Id);
            var orderedQuery = CursorPagination.ApplyOrdering(cursorFiltered, orderDefinition, sortDirection,
                x => x.Id);
            var page = await CursorPagination.ReadPageAsync(orderedQuery, orderDefinition, sortDirection, x => x.Id,
                limit);
            tokenArray = page.Items
                .Select(x =>
                {
                    var token = x.ApiToken;
                    token.finite = CommonsUtils.HasPositiveMaxSupply(token.max_supply_raw);
                    return token;
                })
                .ToArray();
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
            var logMessage = LogEx.Exception("Token()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new TokenResult
        {
            total_results = null,
            tokens = tokenArray,
            next_cursor = nextCursor
        };
    }
}
