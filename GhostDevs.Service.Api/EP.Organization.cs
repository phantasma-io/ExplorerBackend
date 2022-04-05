using System;
using System.Linq;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Serilog;
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
        [APIParameter("Organization name", "string")]
        string organization_name = "",
        [APIParameter("Organization name (partial)", "string")]
        string organization_name_partial = "",
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

                if ( !string.IsNullOrEmpty(organization_name) && !ArgValidation.CheckString(organization_name) )
                    throw new APIException("Unsupported value for 'organization_name' parameter.");

                if ( !string.IsNullOrEmpty(organization_name_partial) &&
                     !ArgValidation.CheckString(organization_name_partial) )
                    throw new APIException("Unsupported value for 'organization_name_partial' parameter.");

                var startTime = DateTime.Now;

                var query = databaseContext.Organizations.AsQueryable();

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
                var logMessage = LogEx.Exception("Organization()", exception);

                throw new APIException(logMessage, exception);
            }
        }

        return new OrganizationResult
            {total_results = with_total == 1 ? totalResults : null, organizations = organizationArray};
    }
}
