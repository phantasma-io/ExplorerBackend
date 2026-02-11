using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Service.Api;

public static class GetSeries
{
    private sealed class SeriesPageItem
    {
        public int Id { get; init; }
        public string SeriesId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public Series ApiSeries { get; init; }
        public JsonDocument Metadata { get; init; }
    }

    [ProducesResponseType(typeof(SeriesResult), (int)HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(SeriesResult), "Returns series of NFTs available on the backend.", false, 10, cacheTag: "serieses")]
    public static async Task<SeriesResult> Execute(
        // ReSharper disable InconsistentNaming
        string order_by = "id",
        string order_direction = "asc",
        int offset = 0,
        int limit = 50,
        string cursor = "",
        string id = "",
        string series_id = "",
        string creator = "",
        string name = "",
        string q = "",
        string chain = "main",
        string contract = "",
        string symbol = "",
        string token_id = "",
        int with_total = 0
    // ReSharper enable InconsistentNaming
    )
    {
        // Results of the query
        long totalResults = 0;
        Series[] seriesArray;
        string? nextCursor = null;
        var useCursor = false;
        var qTrimmed = string.IsNullOrWhiteSpace(q) ? string.Empty : q.Trim();

        try
        {
            #region ArgValidation

            if (!ArgValidation.CheckLimit(limit, false))
                throw new ApiParameterException("Unsupported value for 'limit' parameter.");

            if (!ArgValidation.CheckOffset(offset))
                throw new ApiParameterException("Unsupported value for 'offset' parameter.");

            if (!string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by))
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            if (!ArgValidation.CheckOrderDirection(order_direction))
                throw new ApiParameterException("Unsupported value for 'order_direction' parameter.");

            if (!string.IsNullOrEmpty(series_id) && !ArgValidation.CheckNumber(series_id))
                throw new ApiParameterException("Unsupported value for 'series_id' parameter.");

            if (!string.IsNullOrEmpty(creator) && !ArgValidation.CheckAddress(creator))
                throw new ApiParameterException("Unsupported value for 'creator' parameter.");

            if (!string.IsNullOrEmpty(name) && !ArgValidation.CheckName(name))
                throw new ApiParameterException("Unsupported value for 'name' parameter.");

            if (!string.IsNullOrEmpty(qTrimmed) && !ArgValidation.CheckGeneralSearch(qTrimmed))
                throw new ApiParameterException("Unsupported value for 'q' parameter.");

            if (!string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain))
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            if (!string.IsNullOrEmpty(contract) && !ArgValidation.CheckHash(contract, true))
                throw new ApiParameterException("Unsupported value for 'contract' parameter.");

            if (!string.IsNullOrEmpty(symbol) && !ArgValidation.CheckSymbol(symbol))
                throw new ApiParameterException("Unsupported value for 'symbol' parameter.");

            if (!string.IsNullOrEmpty(token_id) && !ArgValidation.CheckTokenId(token_id))
                throw new ApiParameterException("Unsupported value for 'token_id' parameter.");

            if (!string.IsNullOrEmpty(id) && !ArgValidation.CheckNumber(id))
                throw new ApiParameterException("Unsupported value for 'id' parameter.");

            #endregion

            var cursorToken = CursorPagination.ParseCursor(cursor);
            var sortDirection = CursorPagination.ParseSortDirection(order_direction);
            var orderBy = string.IsNullOrWhiteSpace(order_by) ? "id" : order_by;

            var orderDefinitions =
                new Dictionary<string, CursorOrderDefinition<SeriesPageItem>>(StringComparer.OrdinalIgnoreCase)
                {
                    {
                        "id",
                        new CursorOrderDefinition<SeriesPageItem>(
                            "id",
                            new CursorOrderSegment<SeriesPageItem, int>(
                                x => x.Id,
                                value => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)))
                    },
                    {
                        "series_id",
                        new CursorOrderDefinition<SeriesPageItem>(
                            "series_id",
                            new CursorOrderSegment<SeriesPageItem, string>(
                                x => x.SeriesId,
                                value => value))
                    },
                    {
                        "name",
                        new CursorOrderDefinition<SeriesPageItem>(
                            "name",
                            new CursorOrderSegment<SeriesPageItem, string>(
                                x => x.Name ?? string.Empty,
                                value => value))
                    }
                };

            if (!orderDefinitions.TryGetValue(orderBy, out var orderDefinition))
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            useCursor = CursorPagination.ShouldUseCursor(cursorToken, offset, with_total);

            var startTime = DateTime.Now;
            await using MainDbContext databaseContext = new();
            var query = databaseContext.Serieses.AsQueryable().AsNoTracking();

            #region Filtering

            query = query.Where(x => x.BLACKLISTED != true);

            var qUpper = string.IsNullOrEmpty(qTrimmed) ? string.Empty : qTrimmed.ToUpperInvariant();
            var hasParsedId = int.TryParse(qTrimmed, out var qParsedId);

            if (!string.IsNullOrEmpty(qUpper))
            {
                var isNumber = ArgValidation.CheckNumber(qTrimmed);
                var isHex = ArgValidation.CheckBase16(qTrimmed);
                var isFullHash = isHex && qUpper.Length >= 40;
                var isHexPartial = isHex && !isFullHash;

                query = query.Where(x =>
                    (isNumber && (x.SERIES_ID == qTrimmed || (hasParsedId && x.ID == qParsedId))) ||
                    (isFullHash && x.Contract.HASH == qUpper) ||
                    (isHexPartial && x.Contract.HASH.Contains(qUpper)) ||
                    EF.Functions.ILike(x.NAME, $"%{qTrimmed}%") ||
                    EF.Functions.ILike(x.DESCRIPTION, $"%{qTrimmed}%") ||
                    EF.Functions.ILike(x.Contract.SYMBOL, $"%{qTrimmed}%"));
            }

            if (!string.IsNullOrEmpty(id))
            {
                // `id` is a database row identifier (int). If the caller passes a very large numeric string
                // (e.g. a chain series id), `int.TryParse` fails. Historically we would then ignore the filter
                // and return the full list, which is both incorrect and expensive.
                //
                // Instead: treat out-of-range ids as "no match" and return an empty page immediately.
                if (!int.TryParse(id, out var parsedId))
                {
                    return new SeriesResult
                    {
                        total_results = !useCursor && with_total == 1 ? 0 : null,
                        series = Array.Empty<Series>(),
                        next_cursor = null
                    };
                }

                query = query.Where(x => x.ID == parsedId);
            }

            // Searching for series using SERIES_ID.
            if (!string.IsNullOrEmpty(series_id)) query = query.Where(x => x.SERIES_ID == series_id);

            if (!string.IsNullOrEmpty(creator)) query = query.Where(x => x.CreatorAddress.ADDRESS == creator);

            if (!string.IsNullOrEmpty(name))
            {
                var collectionsIds = databaseContext.Serieses.Where(
                        x => x.NAME.Contains(name) || x.DESCRIPTION.Contains(name)).Select(x => x.ID).Distinct()
                    .ToList();

                query = query.Where(x => collectionsIds.Contains(x.ID));
            }

            // Searching for series using given chain.
            if (!string.IsNullOrEmpty(chain)) query = query.Where(x => x.Contract.Chain.NAME == chain);

            if (!string.IsNullOrEmpty(contract)) query = query.Where(x => x.Contract.HASH == contract);

            // Searching for series by symbol.
            if (!string.IsNullOrEmpty(symbol)) query = query.Where(x => x.Contract.SYMBOL == symbol);

            if (!string.IsNullOrEmpty(token_id))
            {
                var ids = NftMethods.GetSeriesIdsByTokenId(databaseContext, token_id);
                query = query.Where(x => ids.Contains(x.ID));
            }

            #endregion

            // Count total number of results before adding order and limit parts of query.
            if (!useCursor && with_total == 1)
                totalResults = await query.CountAsync();

            #region ResultArray

            var pageQuery = query.Select(x => new SeriesPageItem
            {
                Id = x.ID,
                SeriesId = x.SERIES_ID ?? string.Empty,
                Name = x.NAME,
                ApiSeries = new Series
                {
                    id = x.ID,
                    series_id = x.SERIES_ID ?? "",
                    creator = x.CreatorAddress != null ? x.CreatorAddress.ADDRESS : null,
                    name = x.NAME,
                    description = x.DESCRIPTION,
                    image = x.IMAGE,
                    current_supply = x.CURRENT_SUPPLY,
                    max_supply = x.MAX_SUPPLY,
                    mode_name = x.SeriesMode != null ? x.SeriesMode.MODE_NAME ?? string.Empty : string.Empty,
                    royalties = x.ROYALTIES.ToString(CultureInfo.InvariantCulture),
                    type = x.TYPE,
                    attr_type_1 = x.ATTR_TYPE_1,
                    attr_value_1 = x.ATTR_VALUE_1,
                    attr_type_2 = x.ATTR_TYPE_2,
                    attr_value_2 = x.ATTR_VALUE_2,
                    attr_type_3 = x.ATTR_TYPE_3,
                    attr_value_3 = x.ATTR_VALUE_3
                },
                Metadata = x.METADATA
            });

            if (useCursor)
            {
                var cursorFiltered = CursorPagination.ApplyCursor(pageQuery, orderDefinition, sortDirection, cursorToken,
                    x => x.Id);
                var orderedQuery =
                    CursorPagination.ApplyOrdering(cursorFiltered, orderDefinition, sortDirection, x => x.Id);
                var page = await CursorPagination.ReadPageAsync(orderedQuery, orderDefinition, sortDirection, x => x.Id,
                    limit);
                foreach (var item in page.Items)
                {
                    if (item.ApiSeries != null)
                        item.ApiSeries.metadata = MetadataMapper.FromSeries(item.Metadata, item.ApiSeries);
                }

                seriesArray = page.Items.Select(x => x.ApiSeries).ToArray();
                nextCursor = page.NextCursor;
            }
            else
            {
                var orderedQuery = CursorPagination.ApplyOrdering(pageQuery, orderDefinition, sortDirection, x => x.Id);
                var pageItems = limit > 0 ? orderedQuery.Skip(offset).Take(limit) : orderedQuery;
                var materializedPage = await pageItems.ToArrayAsync();

                foreach (var item in materializedPage)
                {
                    if (item.ApiSeries != null)
                        item.ApiSeries.metadata = MetadataMapper.FromSeries(item.Metadata, item.ApiSeries);
                }

                seriesArray = materializedPage.Select(x => x.ApiSeries).ToArray();
            }

            #endregion

            var responseTime = DateTime.Now - startTime;

            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch (ApiParameterException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var logMessage = LogEx.Exception("Series()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new SeriesResult
        {
            total_results = !useCursor && with_total == 1 ? totalResults : null,
            series = seriesArray,
            next_cursor = nextCursor
        };
    }
}
