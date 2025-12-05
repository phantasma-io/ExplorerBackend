using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Service.Api;

public static class GetAddresses
{
    private sealed class AddressPageItem
    {
        public int Id { get; init; }
        public string Address { get; init; } = string.Empty;
        public string AddressName { get; init; } = string.Empty;
        public int BalanceMissingScore { get; init; }
        public BigInteger BalanceRaw { get; init; }
        public Address ApiAddress { get; init; }
    }

    [ProducesResponseType(typeof(AddressResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(AddressResult), "Returns the addresses on the backend.", false, 10, cacheTag: "addresses")]
    public static async Task<AddressResult> Execute(
        // ReSharper disable InconsistentNaming
        string order_by = "id",
        string order_direction = "asc",
        int offset = 0,
        int limit = 50,
        string cursor = "",
        string chain = "main",
        string address = "",
        string address_name = "",
        string address_partial = "",
        string symbol = "",
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
        string? nextCursor = null;
        var useCursor = false;

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

            if ( !string.IsNullOrEmpty(address_partial) && !ArgValidation.CheckAddress(address_partial) )
                throw new ApiParameterException("Unsupported value for 'address_partial' parameter.");

            if ( !string.IsNullOrEmpty(address_name) && !ArgValidation.CheckString(address_name) )
                throw new ApiParameterException("Unsupported value for 'address_name' parameter.");

            if ( !string.IsNullOrEmpty(organization_name) && !ArgValidation.CheckString(organization_name) )
                throw new ApiParameterException("Unsupported value for 'organization_name' parameter.");

            if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            if ( !string.IsNullOrEmpty(validator_kind) && !ArgValidation.CheckString(validator_kind, true) )
                throw new ApiParameterException("Unsupported value for 'validator_kind' parameter.");

            #endregion

            var cursorToken = CursorPagination.ParseCursor(cursor);
            var sortDirection = CursorPagination.ParseSortDirection(order_direction);
            var orderBy = string.IsNullOrWhiteSpace(order_by) ? "id" : order_by;
            var symbolUpper = string.IsNullOrWhiteSpace(symbol) ? string.Empty : symbol.ToUpperInvariant();
            var balanceUsesSoul = symbolUpper == "SOUL";

            var orderDefinitions =
                new Dictionary<string, CursorOrderDefinition<AddressPageItem>>(StringComparer.OrdinalIgnoreCase)
                {
                    {
                        "id",
                        new CursorOrderDefinition<AddressPageItem>(
                            "id",
                            new CursorOrderSegment<AddressPageItem, int>(
                                x => x.Id,
                                value => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)))
                    },
                    {
                        "address",
                        new CursorOrderDefinition<AddressPageItem>(
                            "address",
                            new CursorOrderSegment<AddressPageItem, string>(
                                x => x.Address,
                                value => value))
                    },
                    {
                        "address_name",
                        new CursorOrderDefinition<AddressPageItem>(
                            "address_name",
                            new CursorOrderSegment<AddressPageItem, string>(
                                x => x.AddressName ?? string.Empty,
                                value => value))
                    },
                    {
                        "balance",
                        new CursorOrderDefinition<AddressPageItem>(
                            "balance",
                            new CursorOrderSegment<AddressPageItem, int>(
                                x => x.BalanceMissingScore,
                                value => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                                directionResolver: _ => CursorSortDirection.Asc),
                            new CursorOrderSegment<AddressPageItem, BigInteger>(
                                x => x.BalanceRaw,
                                value => BigInteger.Parse(value, CultureInfo.InvariantCulture)))
                    }
                };

            if ( !orderDefinitions.TryGetValue(orderBy, out var orderDefinition) )
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            useCursor = CursorPagination.ShouldUseCursor(cursorToken, offset, with_total);

            var startTime = DateTime.Now;

            await using MainDbContext databaseContext = new();

            var query = databaseContext.Addresses.AsQueryable().AsNoTracking();


            #region Filtering

            bool isValidAddress = false;
            if ( !string.IsNullOrEmpty(address) )
                isValidAddress = PhantasmaPhoenix.Cryptography.Address.IsValidAddress(address);
            
            if ( !string.IsNullOrEmpty(address) && isValidAddress) query = query.Where(x => x.ADDRESS == address);

            if ( !string.IsNullOrEmpty(address_name) ) query = query.Where(x => x.ADDRESS_NAME == address_name);
            
            if ( !string.IsNullOrEmpty(address) && !isValidAddress ) query = query.Where(x => x.ADDRESS_NAME == address);

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

            #region ResultArray

            if ( !useCursor && with_total == 1 )
                totalResults = await query.CountAsync();

            var pageQuery = query.Select(x => new AddressPageItem
            {
                Id = x.ID,
                Address = x.ADDRESS,
                AddressName = x.ADDRESS_NAME,
                BalanceMissingScore = balanceUsesSoul
                    ? 0
                    : ( x.AddressBalances.Any(y => y.Token.SYMBOL == symbolUpper) ? 0 : 1 ),
                BalanceRaw = balanceUsesSoul
                    ? x.TOTAL_SOUL_AMOUNT
                    : x.AddressBalances.Where(y => y.Token.SYMBOL == symbolUpper).Select(y => y.AMOUNT_RAW)
                        .FirstOrDefault(),
                ApiAddress = new Address
                {
                    address = x.ADDRESS,
                    address_name = x.ADDRESS_NAME,
                    validator_kind = x.AddressValidatorKind != null ? x.AddressValidatorKind.NAME : null,
                    stake = x.STAKED_AMOUNT,
                    stake_raw = x.STAKED_AMOUNT_RAW,
                    unclaimed = x.UNCLAIMED_AMOUNT,
                    unclaimed_raw = x.UNCLAIMED_AMOUNT_RAW,
                    storage = with_storage == 1 && x.STORAGE_AVAILABLE > 0
                        ? new AddressStorage
                        {
                            available = x.STORAGE_AVAILABLE,
                            used = x.STORAGE_USED,
                            avatar = x.AVATAR
                        }
                        : null,
                    stakes = with_stakes == 1 && ( !string.IsNullOrEmpty(x.STAKED_AMOUNT) || !string.IsNullOrEmpty(x.UNCLAIMED_AMOUNT) )
                        ? new AddressStakes
                        {
                            amount = x.STAKED_AMOUNT,
                            amount_raw = x.STAKED_AMOUNT_RAW,
                            time = x.STAKE_TIMESTAMP,
                            unclaimed = x.UNCLAIMED_AMOUNT,
                            unclaimed_raw = x.UNCLAIMED_AMOUNT_RAW
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
                                chain = new Chain
                                    {
                                        // TODO probably useless, check if can be removed
                                        chain_name = b.Address.Chain.NAME
                                    },
                                amount = b.AMOUNT,
                                amount_raw = b.AMOUNT_RAW.ToString()
                            }
                        ).ToArray()
                        : null
                }
            });

            var cursorFiltered = useCursor
                ? CursorPagination.ApplyCursor(pageQuery, orderDefinition, sortDirection, cursorToken, x => x.Id)
                : pageQuery;
            var orderedQuery = CursorPagination.ApplyOrdering(cursorFiltered, orderDefinition, sortDirection, x => x.Id);

            if ( useCursor )
            {
                var page = await CursorPagination.ReadPageAsync(orderedQuery, orderDefinition, sortDirection, x => x.Id,
                    limit);
                addressArray = page.Items.Select(x => x.ApiAddress).ToArray();
                nextCursor = page.NextCursor;
            }
            else
            {
                var pageItems = limit > 0 ? orderedQuery.Skip(offset).Take(limit) : orderedQuery;
                addressArray = ( await pageItems.ToArrayAsync() ).Select(x => x.ApiAddress).ToArray();
            }

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

        return new AddressResult
        {
            total_results = !useCursor && with_total == 1 ? totalResults : null,
            addresses = addressArray,
            next_cursor = nextCursor
        };
    }
}
