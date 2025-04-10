using System;
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

public static class GetSeries
{
    [ProducesResponseType(typeof(SeriesResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(SeriesResult), "Returns series of NFTs available on the backend.", false, 10, cacheTag: "serieses")]
    public static async Task<SeriesResult> Execute(
        // ReSharper disable InconsistentNaming
        string order_by = "id",
        string order_direction = "asc",
        int offset = 0,
        int limit = 50,
        string id = "",
        string series_id = "",
        string creator = "",
        string name = "",
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

        try
        {
            #region ArgValidation

            if ( !ArgValidation.CheckLimit(limit, false) )
                throw new ApiParameterException("Unsupported value for 'limit' parameter.");

            if ( !ArgValidation.CheckOffset(offset) )
                throw new ApiParameterException("Unsupported value for 'offset' parameter.");

            if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            if ( !ArgValidation.CheckOrderDirection(order_direction) )
                throw new ApiParameterException("Unsupported value for 'order_direction' parameter.");

            if ( !string.IsNullOrEmpty(series_id) && !ArgValidation.CheckNumber(series_id) )
                throw new ApiParameterException("Unsupported value for 'series_id' parameter.");

            if ( !string.IsNullOrEmpty(creator) && !ArgValidation.CheckAddress(creator) )
                throw new ApiParameterException("Unsupported value for 'creator' parameter.");

            if ( !string.IsNullOrEmpty(name) && !ArgValidation.CheckName(name) )
                throw new ApiParameterException("Unsupported value for 'name' parameter.");

            if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            if ( !string.IsNullOrEmpty(contract) && !ArgValidation.CheckHash(contract, true) )
                throw new ApiParameterException("Unsupported value for 'contract' parameter.");

            if ( !string.IsNullOrEmpty(symbol) && !ArgValidation.CheckSymbol(symbol) )
                throw new ApiParameterException("Unsupported value for 'symbol' parameter.");

            if ( !string.IsNullOrEmpty(token_id) && !ArgValidation.CheckTokenId(token_id) )
                throw new ApiParameterException("Unsupported value for 'token_id' parameter.");

            if ( !string.IsNullOrEmpty(id) && !ArgValidation.CheckNumber(id) )
                throw new ApiParameterException("Unsupported value for 'id' parameter.");

            #endregion

            var startTime = DateTime.Now;
            await using MainDbContext databaseContext = new();
            var query = databaseContext.Serieses.AsQueryable().AsNoTracking();

            #region Filtering

            query = query.Where(x => x.BLACKLISTED != true);

            if ( !string.IsNullOrEmpty(id) && int.TryParse(id, out var parsedId) )
                query = query.Where(x => x.ID == parsedId);

            // Searching for series using SERIES_ID.
            if ( !string.IsNullOrEmpty(series_id) ) query = query.Where(x => x.SERIES_ID == series_id);

            if ( !string.IsNullOrEmpty(creator) ) query = query.Where(x => x.CreatorAddress.ADDRESS == creator);

            if ( !string.IsNullOrEmpty(name) )
            {
                var collectionsIds = databaseContext.Serieses.Where(
                        x => x.NAME.Contains(name) || x.DESCRIPTION.Contains(name)).Select(x => x.ID).Distinct()
                    .ToList();

                query = query.Where(x => collectionsIds.Contains(x.ID));
            }

            // Searching for series using given chain.
            if ( !string.IsNullOrEmpty(chain) ) query = query.Where(x => x.Contract.Chain.NAME == chain);

            if ( !string.IsNullOrEmpty(contract) ) query = query.Where(x => x.Contract.HASH == contract);

            // Searching for series by symbol.
            if ( !string.IsNullOrEmpty(symbol) ) query = query.Where(x => x.Contract.SYMBOL == symbol);

            if ( !string.IsNullOrEmpty(token_id) )
            {
                var ids = NftMethods.GetSeriesIdsByTokenId(databaseContext, token_id);
                query = query.Where(x => ids.Contains(x.ID));
            }

            #endregion

            // Count total number of results before adding order and limit parts of query.
            if ( with_total == 1 )
                totalResults = await query.CountAsync();

            if ( order_direction == "asc" )
                query = order_by switch
                {
                    "id" => query.OrderBy(x => x.ID),
                    "series_id" => query.OrderBy(x => x.SERIES_ID),
                    "name" => query.OrderBy(x => x.NAME),
                    _ => query
                };
            else
                query = order_by switch
                {
                    "id" => query.OrderByDescending(x => x.ID),
                    "series_id" => query.OrderByDescending(x => x.SERIES_ID),
                    "name" => query.OrderByDescending(x => x.NAME),
                    _ => query
                };

            #region ResultArray

            seriesArray = await query.Skip(offset).Take(limit).Select(x => new Series
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
            }).ToArrayAsync();

            #endregion

            var responseTime = DateTime.Now - startTime;

            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch ( ApiParameterException )
        {
            throw;
        }
        catch ( Exception exception )
        {
            var logMessage = LogEx.Exception("Series()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new SeriesResult {total_results = with_total == 1 ? totalResults : null, series = seriesArray};
    }
}
