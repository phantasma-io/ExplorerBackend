using System;
using System.Linq;
using System.Net;
using Database.Main;
using Backend.Commons;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Service.Api;

public partial class Endpoints
{
    //TODO change order_by and order_direction maybe to enum
    /// <summary>
    ///     Returns the Addresses on the backend.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-AddressResult'>AddressResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are id, address or address_name</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="chain" example="main">Chain name</param>
    /// <param name="address">hash of an address</param>
    /// <param name="address_name">Name of an Address, if is has one</param>
    /// <param name="address_partial">partial hash of an address</param>
    /// <param name="organization_name">Filter for an Organization Name"</param>
    /// <param name="validator_kind" example="Primary">Filter for a Validator Kind</param>
    /// <param name="with_storage" example="0">returns data with <a href='#model-AddressStorage'>AddressStorage</a></param>
    /// <param name="with_stakes" example="0">returns data with <a href='#model-AddressStake'>AddressStake</a></param>
    /// <param name="with_balance" example="0">returns data with <a href='#model-AddressBalances'>AddressBalances</a></param>
    /// <param name="with_total" example="0">returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [ProducesResponseType(typeof(AddressResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(AddressResult), "Returns the addresses on the backend.", false, 10, cacheTag: "addresses")]
    public AddressResult Addresses(
        // ReSharper disable InconsistentNaming
        string order_by = "id",
        string order_direction = "asc",
        int offset = 0,
        int limit = 50,
        string chain = "main",
        string address = "",
        string address_name = "",
        string address_partial = "",
        string organization_name = "",
        string validator_kind = "",
        int with_storage = 0,
        int with_stakes = 0,
        int with_balance = 0,
        int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        long totalResults = 0;
        Address[] addressArray;

        //chain is not considered a filter atm
        var filter = !string.IsNullOrEmpty(address) || !string.IsNullOrEmpty(address_partial) ||
                     !string.IsNullOrEmpty(organization_name) || !string.IsNullOrEmpty(validator_kind);

        try
        {
            #region ArgValidation

            if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            if ( !ArgValidation.CheckOrderDirection(order_direction) )
                throw new ApiParameterException("Unsupported value for 'order_direction' parameter.");

            if ( !ArgValidation.CheckLimit(limit, filter) )
                throw new ApiParameterException("Unsupported value for 'limit' parameter.");

            if ( !ArgValidation.CheckOffset(offset) )
                throw new ApiParameterException("Unsupported value for 'offset' parameter.");

            if ( !string.IsNullOrEmpty(address) && !ArgValidation.CheckAddress(address) )
                throw new ApiParameterException("Unsupported value for 'address' parameter.");

            ContractMethods.Drop0x(ref address);

            if ( !string.IsNullOrEmpty(address_partial) && !ArgValidation.CheckAddress(address_partial) )
                throw new ApiParameterException("Unsupported value for 'address_partial' parameter.");

            ContractMethods.Drop0x(ref address_partial);

            if ( !string.IsNullOrEmpty(address_name) && !ArgValidation.CheckString(address_name) )
                throw new ApiParameterException("Unsupported value for 'address_name' parameter.");

            if ( !string.IsNullOrEmpty(organization_name) && !ArgValidation.CheckString(organization_name) )
                throw new ApiParameterException("Unsupported value for 'organization_name' parameter.");

            if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            if ( !string.IsNullOrEmpty(validator_kind) && !ArgValidation.CheckString(validator_kind, true) )
                throw new ApiParameterException("Unsupported value for 'validator_kind' parameter.");

            #endregion

            var startTime = DateTime.Now;

            using MainDbContext databaseContext = new();

            var query = databaseContext.Addresses.AsQueryable().AsNoTracking();


            #region Filtering

            if ( !string.IsNullOrEmpty(address) ) query = query.Where(x => x.ADDRESS == address);

            if ( !string.IsNullOrEmpty(address_name) ) query = query.Where(x => x.ADDRESS_NAME == address_name);

            if ( !string.IsNullOrEmpty(address_partial) )
                query = query.Where(x => x.ADDRESS.Contains(address_partial));

            if ( !string.IsNullOrEmpty(organization_name) )
            {
                var organizationAddresses = OrganizationAddressMethods
                    .GetOrganizationAddressByOrganization(databaseContext, organization_name).ToList();
                query = query.Where(x => x.OrganizationAddresses.Any(y => organizationAddresses.Contains(y)));
            }

            if ( !string.IsNullOrEmpty(chain) ) query = query.Where(x => x.Chain.NAME == chain);

            if ( !string.IsNullOrEmpty(validator_kind) )
                query = query.Where(x => x.AddressValidatorKind.NAME == validator_kind);

            #endregion

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

            #region ResultArray

            //limit -1 is just allowed if a filter is set
            if ( limit > 0 ) query = query.Skip(offset).Take(limit);

            addressArray = query.Select(x => new Address
            {
                address = x.ADDRESS,
                address_name = x.ADDRESS_NAME,
                validator_kind = x.AddressValidatorKind != null ? x.AddressValidatorKind.NAME : null,
                stake = x.STAKE,
                unclaimed = x.UNCLAIMED,
                relay = x.RELAY,
                storage = with_storage == 1 && x.AddressStorage != null
                    ? new AddressStorage
                    {
                        available = x.AddressStorage.AVAILABLE,
                        used = x.AddressStorage.USED,
                        avatar = x.AddressStorage.AVATAR
                    }
                    : null,
                stakes = with_stakes == 1 && x.AddressStake != null
                    ? new AddressStakes
                    {
                        amount = x.AddressStake.AMOUNT,
                        time = x.AddressStake.TIME,
                        unclaimed = x.AddressStake.UNCLAIMED
                    }
                    : null,
                balances = with_balance == 1 && x.AddressBalances != null
                    ? x.AddressBalances.Select(b => new AddressBalance
                        {
                            token = b.Token != null
                                ? new Token
                                {
                                    symbol = b.Token.SYMBOL,
                                    fungible = b.Token.FUNGIBLE,
                                    transferable = b.Token.TRANSFERABLE,
                                    finite = b.Token.FINITE,
                                    divisible = b.Token.DIVISIBLE,
                                    fiat = b.Token.FIAT,
                                    fuel = b.Token.FUEL,
                                    swappable = b.Token.SWAPPABLE,
                                    burnable = b.Token.BURNABLE,
                                    stakable = b.Token.STAKABLE,
                                    decimals = b.Token.DECIMALS
                                }
                                : null,
                            chain = b.Chain != null
                                ? new Chain
                                {
                                    chain_name = b.Chain.NAME
                                }
                                : null,
                            amount = b.AMOUNT
                        }
                    ).ToArray()
                    : null
            }).ToArray();

            #endregion

            var responseTime = DateTime.Now - startTime;

            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch ( ApiParameterException )
        {
            throw;
        }
        catch ( Exception exception )
        {
            var logMessage = LogEx.Exception("Address()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new AddressResult {total_results = with_total == 1 ? totalResults : null, addresses = addressArray};
    }
}
