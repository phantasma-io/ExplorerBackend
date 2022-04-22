using System;
using System.Linq;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Contract = GhostDevs.Service.ApiResults.Contract;
using Event = GhostDevs.Service.ApiResults.Event;
using Organization = GhostDevs.Service.ApiResults.Organization;
using StringEvent = GhostDevs.Service.ApiResults.StringEvent;

namespace GhostDevs.Service;

public partial class Endpoints
{
    [APIInfo(typeof(OrganizationResult), "Returns the token on the backend.", false, 10)]
    public OrganizationResult Organizations(
        [APIParameter("Order by [id, name]", "string")]
        string order_by = "name",
        [APIParameter("Order direction [asc, desc]", "string")]
        string order_direction = "asc",
        [APIParameter("Offset", "integer")] int offset = 0,
        [APIParameter("Limit", "integer")] int limit = 50,
        [APIParameter("Organization name", "string")]
        string organization_name = "",
        [APIParameter("Organization name (partial)", "string")]
        string organization_name_partial = "",
        [APIParameter("return data with event of the creation", "integer")]
        int with_creation_event = 0,
        [APIParameter("Return total (slower) or not (faster)", "integer")]
        int with_total = 0
    )
    {
        long totalResults = 0;
        Organization[] organizationArray;

        try
        {
            if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                throw new APIException("Unsupported value for 'order_by' parameter.");

            if ( !ArgValidation.CheckOrderDirection(order_direction) )
                throw new APIException("Unsupported value for 'order_direction' parameter.");

            if ( !ArgValidation.CheckLimit(limit, false) )
                throw new APIException("Unsupported value for 'limit' parameter.");

            if ( !string.IsNullOrEmpty(organization_name) && !ArgValidation.CheckString(organization_name) )
                throw new APIException("Unsupported value for 'organization_name' parameter.");

            if ( !string.IsNullOrEmpty(organization_name_partial) &&
                 !ArgValidation.CheckString(organization_name_partial) )
                throw new APIException("Unsupported value for 'organization_name_partial' parameter.");

            var startTime = DateTime.Now;

            var query = _context.Organizations.AsQueryable().AsNoTracking();

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
                    _ => query
                };
            else
                query = order_by switch
                {
                    "id" => query.OrderByDescending(x => x.ID),
                    "name" => query.OrderByDescending(x => x.NAME),
                    _ => query
                };

            organizationArray = query.Skip(offset).Take(limit).Select(x => new Organization
            {
                name = x.NAME,
                create_event = with_creation_event == 1 && x.CreateEvent != null
                    ? new Event
                    {
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
                    : null
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
            var logMessage = LogEx.Exception("Organization()", exception);

            throw new APIException(logMessage, exception);
        }

        return new OrganizationResult
            {total_results = with_total == 1 ? totalResults : null, organizations = organizationArray};
    }
}
