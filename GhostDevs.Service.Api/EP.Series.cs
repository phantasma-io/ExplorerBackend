using System;
using System.Collections.Generic;
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
    [APIInfo(typeof(SeriesResult), "Returns series of NFTs available on the backend.", false, 10)]
    public SeriesResult Series([APIParameter("Order by [id, name]", "string")] string order_by = "id",
        [APIParameter("Order direction [asc, desc]", "string")]
        string order_direction = "asc",
        [APIParameter("Offset", "integer")] int offset = 0,
        [APIParameter("Limit", "integer")] int limit = 50,
        [APIParameter("Series ID", "string")] string id = "",
        [APIParameter("Creator of series", "string")]
        string creator = "",
        [APIParameter("Series name/description filter (partial match)", "string")]
        string name = "",
        [APIParameter("Chain name (ex. 'PHA')", "string")]
        string chain = "",
        [APIParameter("Token contract hash", "string")]
        string contract = "",
        [APIParameter("Symbol (ex. 'SOUL')", "string")]
        string symbol = "")
    {
        // Results of the query
        long totalResults = 0;
        Series[] seriesArray = null;

        using ( var databaseContext = new MainDbContext() )
        {
            try
            {
                if ( !ArgValidation.CheckLimit(limit) )
                    throw new APIException("Unsupported value for 'limit' parameter.");

                if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                    throw new APIException("Unsupported value for 'order_by' parameter.");

                if ( !ArgValidation.CheckOrderDirection(order_direction) )
                    throw new APIException("Unsupported value for 'order_direction' parameter.");

                if ( !string.IsNullOrEmpty(id) && !ArgValidation.CheckSeriesId(id) )
                    throw new APIException("Unsupported value for 'id' parameter.");

                if ( !string.IsNullOrEmpty(creator) && !ArgValidation.CheckAddress(creator) )
                    throw new APIException("Unsupported value for 'creator' parameter.");

                ContractMethods.Drop0x(ref creator);

                if ( !string.IsNullOrEmpty(name) && !ArgValidation.CheckName(name) )
                    throw new APIException("Unsupported value for 'name' parameter.");

                if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                    throw new APIException("Unsupported value for 'chain' parameter.");

                if ( !string.IsNullOrEmpty(contract) && !ArgValidation.CheckAddress(contract) )
                    throw new APIException("Unsupported value for 'contract' parameter.");

                ContractMethods.Drop0x(ref contract);

                if ( !string.IsNullOrEmpty(symbol) && !ArgValidation.CheckSymbol(symbol, true) )
                    throw new APIException("Unsupported value for 'symbol' parameter.");

                var startTime = DateTime.Now;

                //if (UseEFQueries)
                {
                    var query = databaseContext.Serieses
                        .Include(x => x.CreatorAddress)
                        .Include(x => x.SeriesMode)
                        .Include(x => x.Contract).ThenInclude(x => x.Chain)
                        .AsQueryable();

                    query = query.Where(x => x.BLACKLISTED != true);

                    if ( !string.IsNullOrEmpty(id) )
                        // Searching for series using SERIES_ID.
                        query = query.Where(x => x.SERIES_ID == id);

                    if ( !string.IsNullOrEmpty(creator) )
                    {
                        var ids = AddressMethods.GetIdsFromExtendedFormat(databaseContext, creator, true, chain);

                        query = query.Where(x => ids.Contains(x.CreatorAddress.ID));
                    }

                    if ( !string.IsNullOrEmpty(name) )
                    {
                        var collectionsIds = databaseContext.Serieses.Where(
                                x => x.NAME.ToUpper().Contains(name.ToUpper()) ||
                                     x.DESCRIPTION.ToUpper().Contains(name.ToUpper())
                            ).Select(x => x.ID)
                            .Distinct()
                            .ToList();

                        query = query.Where(x => collectionsIds.Contains(x.ID));
                    }

                    if ( !string.IsNullOrEmpty(chain) )
                        // Searching for series using given chain.
                        query = query.Where(x => x.Contract.Chain.NAME.ToUpper() == chain.ToUpper());

                    if ( !string.IsNullOrEmpty(contract) )
                        query = query.Where(x => x.Contract.HASH.ToUpper() == contract.ToUpper());

                    if ( !string.IsNullOrEmpty(symbol) )
                        // Searching for series by symbol.

                        query = query.Where(x => x.Contract.SYMBOL.ToUpper() == symbol.ToUpper());

                    // Count total number of results before adding order and limit parts of query.
                    totalResults = query.Count();

                    if ( order_direction == "asc" )
                    {
                        if ( order_by == "id" )
                            query = query.OrderBy(x => x.SERIES_ID);
                        else if ( order_by == "name" ) query = query.OrderBy(x => x.NAME);
                    }
                    else
                    {
                        if ( order_by == "id" )
                            query = query.OrderByDescending(x => x.SERIES_ID);
                        else if ( order_by == "name" ) query = query.OrderByDescending(x => x.NAME);
                    }

                    var queryResults = query.Skip(offset).Take(limit).ToList();

                    var serieses = new List<Series>();

                    foreach ( var x in queryResults )
                    {
                        var series = new Series
                        {
                            id = x.SERIES_ID != null ? x.SERIES_ID : "",
                            creator = AddressMethods.Prepend0x(x.CreatorAddress?.ADDRESS, x.Contract.Chain.NAME),
                            name = x.NAME,
                            description = x.DESCRIPTION,
                            image = x.IMAGE,
                            current_supply = x.CURRENT_SUPPLY,
                            max_supply = x.MAX_SUPPLY,
                            mode_name = x.SeriesMode?.MODE_NAME,
                            royalties = x.ROYALTIES.ToString(),
                            type = x.TYPE,
                            attrType1 = x.ATTR_TYPE_1,
                            attrValue1 = x.ATTR_VALUE_1,
                            attrType2 = x.ATTR_TYPE_2,
                            attrValue2 = x.ATTR_VALUE_2,
                            attrType3 = x.ATTR_TYPE_3,
                            attrValue3 = x.ATTR_VALUE_3
                        };

                        serieses.Add(series);
                    }

                    seriesArray = serieses.ToArray();
                }

                var responseTime = DateTime.Now - startTime;

                Log.Information($"API result generated in {Math.Round(responseTime.TotalSeconds, 3)} sec");
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
        }

        return new SeriesResult {total_results = totalResults, series = seriesArray};
    }
}
