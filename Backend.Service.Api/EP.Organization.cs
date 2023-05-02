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
    //TODO change order_by and order_direction maybe to enum
    /// <summary>
    ///     Returns the Organizations on the backend.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-OrganizationResult'>OrganizationResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are id, name or organization_id</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="organization_id" example="validators">Organization id</param>
    /// <param name="organization_id_partial" example="valid">Organization id (partial)</param>
    /// <param name="organization_name" example="Block Producers">Organization Name</param>
    /// <param name="organization_name_partial" example="Block Pro">Organization Name (partial)</param>
    /// <param name="with_creation_event" example="0">Return data with <a href='#model-Event'>Event</a> of the creation</param>
    /// <param name="with_address" example="0">Return data with <a href='#model-Address'>Event</a> of the creation</param>
    /// <param name="with_total" example="0">Returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [ProducesResponseType(typeof(OrganizationResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(OrganizationResult), "Returns the Organizations on the backend.", false, 10)]
    public OrganizationResult Organizations(
        // ReSharper disable InconsistentNaming
        string order_by = "name",
        string order_direction = "asc",
        int offset = 0,
        int limit = 50,
        string organization_id = "",
        string organization_id_partial = "",
        string organization_name = "",
        string organization_name_partial = "",
        int with_creation_event = 0,
        int with_address = 0,
        int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        long totalResults = 0;
        Organization[] organizationArray;

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

            if ( !string.IsNullOrEmpty(organization_id) && !ArgValidation.CheckString(organization_id) )
                throw new ApiParameterException("Unsupported value for 'organization_id' parameter.");

            if ( !string.IsNullOrEmpty(organization_id_partial) &&
                 !ArgValidation.CheckString(organization_id_partial) )
                throw new ApiParameterException("Unsupported value for 'organization_name_partial' parameter.");

            if ( !string.IsNullOrEmpty(organization_name) && !ArgValidation.CheckString(organization_name) )
                throw new ApiParameterException("Unsupported value for 'organization_name' parameter.");

            if ( !string.IsNullOrEmpty(organization_name_partial) &&
                 !ArgValidation.CheckString(organization_name_partial) )
                throw new ApiParameterException("Unsupported value for 'organization_name_partial' parameter.");

            var startTime = DateTime.Now;
            using MainDbContext databaseContext = new();
            var query = databaseContext.Organizations.AsQueryable().AsNoTracking();

            if ( !string.IsNullOrEmpty(organization_id) )
                query = query.Where(x => x.ORGANIZATION_ID == organization_id);

            if ( !string.IsNullOrEmpty(organization_id_partial) )
                query = query.Where(x => x.ORGANIZATION_ID.Contains(organization_id_partial));

            if ( !string.IsNullOrEmpty(organization_name) ) query = query.Where(x => x.NAME == organization_name);

            if ( !string.IsNullOrEmpty(organization_name_partial) )
                query = query.Where(x => x.NAME.Contains(organization_name_partial));

            if ( with_total == 1 )
                totalResults = query.Count();

            //in case we add more to sort
            if ( order_direction == "asc" )
                query = order_by switch
                {
                    "id" => query.OrderBy(x => x.ID),
                    "name" => query.OrderBy(x => x.NAME),
                    "organization_id" => query.OrderBy(x => x.ORGANIZATION_ID),
                    _ => query
                };
            else
                query = order_by switch
                {
                    "id" => query.OrderByDescending(x => x.ID),
                    "name" => query.OrderByDescending(x => x.NAME),
                    "organization_id" => query.OrderByDescending(x => x.ORGANIZATION_ID),
                    _ => query
                };

            organizationArray = query.Skip(offset).Take(limit).Select(x => new Organization
            {
                id = x.ORGANIZATION_ID,
                name = x.NAME,
                size = x.OrganizationAddresses.Count,
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
                            hash = ContractMethods.Prepend0x(x.CreateEvent.Contract.HASH, x.CreateEvent.Chain.NAME),
                            symbol = x.CreateEvent.Contract.SYMBOL
                        },
                        string_event = x.CreateEvent.StringEvent != null
                            ? new StringEvent
                            {
                                string_value = x.CreateEvent.StringEvent.STRING_VALUE
                            }
                            : null
                    }
                    : null,
                address = with_address == 1 && x.ADDRESS != null && x.ADDRESS_NAME != null
                    ? new Address
                    {
                        address = x.ADDRESS,
                        address_name = x.ADDRESS_NAME
                    }
                    : null
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
            var logMessage = LogEx.Exception("Organization()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new OrganizationResult
            {total_results = with_total == 1 ? totalResults : null, organizations = organizationArray};
    }
}
