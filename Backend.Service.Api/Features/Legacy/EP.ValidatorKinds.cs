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
    [ProducesResponseType(typeof(ValidatorKindResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(ValidatorKindResult), "Returns the ValidatorKinds on the backend.", false, 10)]
    public static ValidatorKindResult ValidatorKinds(
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
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            if ( !ArgValidation.CheckOrderDirection(order_direction) )
                throw new ApiParameterException("Unsupported value for 'order_direction' parameter.");

            if ( !ArgValidation.CheckLimit(limit, false) )
                throw new ApiParameterException("Unsupported value for 'limit' parameter.");

            if ( !ArgValidation.CheckOffset(offset) )
                throw new ApiParameterException("Unsupported value for 'offset' parameter.");

            if ( !string.IsNullOrEmpty(validator_kind) && !ArgValidation.CheckString(validator_kind, true) )
                throw new ApiParameterException("Unsupported value for 'validator_kind' parameter.");

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
        catch ( ApiParameterException )
        {
            throw;
        }
        catch ( Exception exception )
        {
            var logMessage = LogEx.Exception("ValidatorKinds()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new ValidatorKindResult
            {total_results = with_total == 1 ? totalResults : null, validator_kinds = validatorKindArray};
    }
}
