using System;
using System.Linq;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Serilog;
using Address = GhostDevs.Service.ApiResults.Address;
using Organization = GhostDevs.Service.ApiResults.Organization;

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
        [APIParameter("Return addresses with organization", "integer")]
        int with_addresses = 0,
        [APIParameter("Return total (slower) or not (faster)", "integer")]
        int with_total = 0
    )
    {
        long totalResults = 0;
        Organization[] organizationArray;

        using ( var databaseContext = new MainDbContext() )
        {
            try
            {
                if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                    throw new APIException("Unsupported value for 'order_by' parameter.");

                if ( !ArgValidation.CheckOrderDirection(order_direction) )
                    throw new APIException("Unsupported value for 'order_direction' parameter.");

                if ( !ArgValidation.CheckLimit(limit) )
                    throw new APIException("Unsupported value for 'limit' parameter.");

                var startTime = DateTime.Now;

                var query = databaseContext.Organizations.AsQueryable();

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
                    addresses = with_addresses == 1 && x.OrganizationAddresses != null
                        ? x.OrganizationAddresses.Select(y => new Address
                        {
                            address = y.Address.ADDRESS,
                            address_name = y.Address.ADDRESS_NAME
                        }).ToArray()
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
        }

        return new OrganizationResult
            {total_results = with_total == 1 ? totalResults : null, organizations = organizationArray};
    }
}
