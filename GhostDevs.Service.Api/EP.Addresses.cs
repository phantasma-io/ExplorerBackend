using System;
using System.Linq;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Serilog;
using Address = GhostDevs.Service.ApiResults.Address;
using AddressBalance = GhostDevs.Service.ApiResults.AddressBalance;
using AddressStorage = GhostDevs.Service.ApiResults.AddressStorage;
using Chain = GhostDevs.Service.ApiResults.Chain;
using Token = GhostDevs.Service.ApiResults.Token;
using Transaction = GhostDevs.Service.ApiResults.Transaction;

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
        [APIParameter("Address (partial match)", "string")]
        string address_partial = "",
        [APIParameter("Returns with storage", "integer")]
        int with_storage = 0,
        [APIParameter("Returns with stake", "integer")]
        int with_stakes = 0,
        [APIParameter("Returns with balances", "integer")]
        int with_balance = 0,
        [APIParameter("Returns with transactions", "integer")]
        int with_transactions = 0,
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

                ContractMethods.Drop0x(ref address);

                if ( !string.IsNullOrEmpty(address_partial) && !ArgValidation.CheckAddress(address_partial) )
                    throw new APIException("Unsupported value for 'address_partial' parameter.");

                ContractMethods.Drop0x(ref address_partial);

                if ( !string.IsNullOrEmpty(address_name) && !ArgValidation.CheckString(address_name) )
                    throw new APIException("Unsupported value for 'address_name' parameter.");

                var startTime = DateTime.Now;

                var query = databaseContext.Addresses.AsQueryable();

                if ( !string.IsNullOrEmpty(address) )
                    query = query.Where(x => string.Equals(x.ADDRESS.ToUpper(), address.ToUpper()));

                if ( !string.IsNullOrEmpty(address_name) )
                    query = query.Where(x => string.Equals(x.ADDRESS_NAME.ToUpper(), address_name.ToUpper()));

                if ( !string.IsNullOrEmpty(address_partial) )
                    query = query.Where(x => x.ADDRESS.ToUpper().Contains(address_partial.ToUpper()));

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
                
                addressArray = query.Skip(offset).Take(limit).Select(x => new Address
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
                        : null,
                    transactions = with_transactions == 1 && x.AddressTransactions != null
                        ? x.AddressTransactions.Select(t => new Transaction
                            {
                                hash = t.Transaction.HASH,
                                blockHeight = t.Transaction.Block.HEIGHT,
                                index = t.Transaction.INDEX,
                                events = null //Array.Empty<Event>()
                            }
                        ).ToArray()
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
                var logMessage = LogEx.Exception("Address()", exception);

                throw new APIException(logMessage, exception);
            }
        }

        return new AddressResult {total_results = with_total == 1 ? totalResults : null, addresses = addressArray};
    }
}
