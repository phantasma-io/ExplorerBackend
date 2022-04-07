using System;
using System.Linq;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Serilog;

namespace GhostDevs.Service;

public partial class Endpoints
{
    [APIInfo(typeof(BlockResult), "Returns the block information from backend.", false, 10)]
    public BlockResult Blocks(
        [APIParameter("Order by [id, hash]", "string")]
        string order_by = "id",
        [APIParameter("Order direction [asc, desc]", "string")]
        string order_direction = "asc",
        [APIParameter("Offset", "integer")] int offset = 0,
        [APIParameter("Limit", "integer")] int limit = 50,
        [APIParameter("hash", "string")] string hash = "",
        [APIParameter("hash (partial match)", "string")]
        string hash_partial = "",
        [APIParameter("height of the block", "string")]
        string height = "",
        [APIParameter("Chain name (ex. 'main')", "string")]
        string chain = "",
        [APIParameter("Date (less than)", "string")]
        string date_less = "",
        [APIParameter("Date (greater than)", "string")]
        string date_greater = "",
        [APIParameter("Return total (slower) or not (faster)", "integer")]
        int with_total = 0
    )
    {
        long totalResults = 0;
        Block[] blockArray;


        try
        {
            #region ArgValidation

            if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                throw new APIException("Unsupported value for 'order_by' parameter.");

            if ( !ArgValidation.CheckOrderDirection(order_direction) )
                throw new APIException("Unsupported value for 'order_direction' parameter.");

            if ( !ArgValidation.CheckLimitOffset(limit, offset) )
                throw new APIException("Unsupported value for 'limit' and/or 'offset' parameter.");

            if ( !string.IsNullOrEmpty(hash) && !ArgValidation.CheckHash(hash) )
                throw new APIException("Unsupported value for 'hash' parameter.");

            if ( !string.IsNullOrEmpty(hash_partial) && !ArgValidation.CheckHash(hash_partial) )
                throw new APIException("Unsupported value for 'hash_partial' parameter.");

            if ( !string.IsNullOrEmpty(height) && !ArgValidation.CheckNumber(height) )
                throw new APIException("Unsupported value for 'height' parameter.");


            if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                throw new APIException("Unsupported value for 'chain' parameter.");

            #endregion

            var startTime = DateTime.Now;

            //just need that since we build the model so it knows what we can use
            var query = _context.Blocks.AsQueryable();

            #region Filtering

            if ( !string.IsNullOrEmpty(hash) )
                query = query.Where(x => x.HASH == hash);

            if ( !string.IsNullOrEmpty(hash_partial) )
                query = query.Where(x => x.HASH.Contains(hash_partial));

            if ( !string.IsNullOrEmpty(height) )
                query = query.Where(x => x.HEIGHT == height);

            if ( !string.IsNullOrEmpty(date_less) )
                query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS <= UnixSeconds.FromString(date_less));

            if ( !string.IsNullOrEmpty(date_greater) )
                query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS >= UnixSeconds.FromString(date_greater));

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
                    "hash" => query.OrderBy(x => x.HASH),
                    _ => query
                };
            else
                query = order_by switch
                {
                    "id" => query.OrderByDescending(x => x.ID),
                    "hash" => query.OrderByDescending(x => x.HASH),
                    _ => query
                };

            #region ResultArray

            if ( limit > 0 && offset >= 0 ) query = query.Skip(offset).Take(limit);

            blockArray = query.Select(x => new Block
            {
                height = x.HEIGHT,
                hash = x.HASH,
                previous_hash = x.PREVIOUS_HASH,
                protocol = x.PROTOCOL,
                chain_address = x.ChainAddress.ADDRESS,
                validator_address = x.ValidatorAddress.ADDRESS,
                date = x.TIMESTAMP_UNIX_SECONDS.ToString()
            }).ToArray();

            #endregion

            var responseTime = DateTime.Now - startTime;
            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch ( APIException )
        {
            throw;
        }
        catch ( Exception exception )
        {
            var logMessage = LogEx.Exception("Block()", exception);

            throw new APIException(logMessage, exception);
        }


        return new BlockResult {total_results = with_total == 1 ? totalResults : null, blocks = blockArray};
    }
}
