using System;
using System.Globalization;
using System.Linq;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Series = GhostDevs.Service.ApiResults.Series;

namespace GhostDevs.Service;

public partial class Endpoints
{
    [APIInfo(typeof(SeriesResult), "Returns series of NFTs available on the backend.", false, 10, cacheTag: "serieses")]
    public SeriesResult Series([APIParameter("Order by [id, series_id, name]", "string")] string order_by = "id",
        [APIParameter("Order direction [asc, desc]", "string")]
        string order_direction = "asc",
        [APIParameter("Offset", "integer")] int offset = 0,
        [APIParameter("Limit", "integer")] int limit = 50,
        [APIParameter("ID", "string")] string id = "",
        [APIParameter("Series ID", "string")] string series_id = "",
        [APIParameter("Creator of series", "string")]
        string creator = "",
        [APIParameter("Series name/description filter (partial match)", "string")]
        string name = "",
        [APIParameter("Chain name (ex. 'PHA')", "string")]
        string chain = "",
        [APIParameter("Token contract hash", "string")]
        string contract = "",
        [APIParameter("Symbol (ex. 'SOUL')", "string")]
        string symbol = "",
        [APIParameter("Token ID", "string")] string token_id = "",
        [APIParameter("Return total (slower) or not (faster)", "integer")]
        int with_total = 0)
    {
        // Results of the query
        long totalResults = 0;
        Series[] seriesArray;

        try
        {
            #region ArgValidation

            if ( !ArgValidation.CheckLimit(limit, false) )
                throw new APIException("Unsupported value for 'limit' parameter.");

            if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                throw new APIException("Unsupported value for 'order_by' parameter.");

            if ( !ArgValidation.CheckOrderDirection(order_direction) )
                throw new APIException("Unsupported value for 'order_direction' parameter.");

            if ( !string.IsNullOrEmpty(series_id) && !ArgValidation.CheckNumber(series_id) )
                throw new APIException("Unsupported value for 'series_id' parameter.");

            if ( !string.IsNullOrEmpty(creator) && !ArgValidation.CheckAddress(creator) )
                throw new APIException("Unsupported value for 'creator' parameter.");

            ContractMethods.Drop0x(ref creator);

            if ( !string.IsNullOrEmpty(name) && !ArgValidation.CheckName(name) )
                throw new APIException("Unsupported value for 'name' parameter.");

            if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                throw new APIException("Unsupported value for 'chain' parameter.");

            if ( !string.IsNullOrEmpty(contract) && !ArgValidation.CheckHash(contract, true) )
                throw new APIException("Unsupported value for 'contract' parameter.");

            ContractMethods.Drop0x(ref contract);

            if ( !string.IsNullOrEmpty(symbol) && !ArgValidation.CheckSymbol(symbol) )
                throw new APIException("Unsupported value for 'symbol' parameter.");

            if ( !string.IsNullOrEmpty(token_id) && !ArgValidation.CheckTokenId(token_id) )
                throw new APIException("Unsupported value for 'token_id' parameter.");

            if ( !string.IsNullOrEmpty(id) && !ArgValidation.CheckNumber(id) )
                throw new APIException("Unsupported value for 'id' parameter.");

            #endregion

            var startTime = DateTime.Now;
            using MainDbContext databaseContext = new();
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
                totalResults = query.Count();

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

            seriesArray = query.Skip(offset).Take(limit).Select(x => new Series
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
            }).ToArray();

            #endregion

            var responseTime = DateTime.Now - startTime;

            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch ( APIException )
        {
            throw;
        }
        catch ( Exception e )
        {
            var logMessage = LogEx.Exception("Series()", e);

            throw new APIException(logMessage, e);
        }

        return new SeriesResult {total_results = with_total == 1 ? totalResults : null, series = seriesArray};
    }
}
