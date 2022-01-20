using System;
using System.Linq;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Serilog;
using Address = GhostDevs.Service.ApiResults.Address;

namespace GhostDevs.Service;

public partial class Endpoints
{
    [APIInfo(typeof(AddressResult), "Returns the addresses on the backend.", false, 10)]
    public AddressResult Addresses(
        [APIParameter("Order by [id, address, address_name]", "string")]
        string order_by = "id",
        [APIParameter("Order direction [asc, desc]", "string")]
        string order_direction = "asc",
        [APIParameter("Offset", "integer")] int offset = 0,
        [APIParameter("Limit", "integer")] int limit = 50,
        [APIParameter("address", "string")] string address = "",
        [APIParameter("address_name (ex. 'genesis')", "string")]
        string address_name = "",
        [APIParameter("Return total (slower) or not (faster)", "integer")]
        int with_total = 0)
    {
        long totalResults = 0;
        Address[] addressArray;

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

                if ( !string.IsNullOrEmpty(address) && !ArgValidation.CheckAddress(address) )
                    throw new APIException("Unsupported value for 'address' parameter.");

                if ( !string.IsNullOrEmpty(address) && !ArgValidation.CheckString(address_name) )
                    throw new APIException("Unsupported value for 'address_name' parameter.");

                var startTime = DateTime.Now;

                var query = databaseContext.Addresses.AsQueryable();

                if ( !string.IsNullOrEmpty(address) )
                    query = query.Where(x => string.Equals(x.ADDRESS.ToUpper(), address.ToUpper()));

                if ( !string.IsNullOrEmpty(address_name) )
                    query = query.Where(x => string.Equals(x.ADDRESS_NAME.ToUpper(), address_name.ToUpper()));

                // Count total number of results before adding order and limit parts of query.
                if ( with_total == 1 )
                    totalResults = query.Count();

                //in case we add more to sort
                if ( order_direction == "asc" )
                    query = order_by switch
                    {
                        "id" => query.OrderBy(x => x.ID),
                        "address" => query.OrderBy(x => x.ADDRESS),
                        "address_name" => query.OrderBy(x => x.ADDRESS_NAME),
                        _ => query
                    };
                else
                    query = order_by switch
                    {
                        "id" => query.OrderByDescending(x => x.ID),
                        "address" => query.OrderByDescending(x => x.ADDRESS),
                        "address_name" => query.OrderByDescending(x => x.ADDRESS_NAME),
                        _ => query
                    };

                var queryResults = query.Skip(offset).Take(limit).ToList();

                addressArray = queryResults.Select(x => new Address
                {
                    address = x.ADDRESS,
                    address_name = x.ADDRESS_NAME
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
                var logMessage = LogEx.Exception("Address()", exception);

                throw new APIException(logMessage, exception);
            }
        }

        return new AddressResult {total_results = with_total == 1 ? totalResults : null, addresses = addressArray};
    }
}
