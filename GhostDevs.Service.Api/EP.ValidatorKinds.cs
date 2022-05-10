using System;
using System.Linq;
using System.Net;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace GhostDevs.Service;

public partial class Endpoints
{
    //TODO change order_by and order_direction maybe to enum
    /// <summary>
    ///     Returns the ValidatorKinds on the backend.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-ValidatorKindResult'>ValidatorKindResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are id or name</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="validator_kind" example="Invalid">validatorKind name</param>
    /// <param name="with_total" example="0">returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Ok</response>
    [ProducesResponseType(typeof(ValidatorKindResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [APIInfo(typeof(ValidatorKindResult), "Returns the ValidatorKinds on the backend.", false, 10)]
    public ValidatorKindResult ValidatorKinds(
        // ReSharper disable InconsistentNaming
        string order_by = "id",
        string order_direction = "asc",
        int offset = 0,
        int limit = 50,
        string validator_kind = "",
        int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        long totalResults = 0;
        ValidatorKind[] validatorKindArray;

        try
        {
            if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                throw new APIException("Unsupported value for 'order_by' parameter.");

            if ( !ArgValidation.CheckOrderDirection(order_direction) )
                throw new APIException("Unsupported value for 'order_direction' parameter.");

            if ( !ArgValidation.CheckLimit(limit, false) )
                throw new APIException("Unsupported value for 'limit' parameter.");

            if ( !string.IsNullOrEmpty(validator_kind) && !ArgValidation.CheckString(validator_kind, true) )
                throw new APIException("Unsupported value for 'validator_kind' parameter.");

            var startTime = DateTime.Now;
            using MainDbContext databaseContext = new();
            var query = databaseContext.AddressValidatorKinds.AsQueryable().AsNoTracking();

            if ( !string.IsNullOrEmpty(validator_kind) ) query = query.Where(x => x.NAME == validator_kind);

            // Count total number of results before adding order and limit parts of query.
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

            validatorKindArray = query.Skip(offset).Take(limit).Select(x => new ValidatorKind
            {
                name = x.NAME
            }).ToArray();

            var responseTime = DateTime.Now - startTime;

            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch ( APIException )
        {
            throw;
        }
        catch ( Exception exception )
        {
            var logMessage = LogEx.Exception("ValidatorKinds()", exception);

            throw new APIException(logMessage, exception);
        }

        return new ValidatorKindResult
            {total_results = with_total == 1 ? totalResults : null, validator_kinds = validatorKindArray};
    }
}
