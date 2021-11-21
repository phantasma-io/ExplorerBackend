using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GhostDevs.Service.ApiResults;
using Database.Main;
using Microsoft.EntityFrameworkCore;
using GhostDevs.Commons;
using Serilog;

namespace GhostDevs.Service
{
    public partial class Endpoints
    {
        [APIInfo(typeof(EventsResult), "Returns events available on the backend.", false, 10)]
        public EventsResult Events([APIParameter("Order by [date, token_id, price]", "string")] string order_by = "date",
            [APIParameter("Order direction [asc, desc]", "string")] string order_direction = "asc",
            [APIParameter("Offset", "integer")] int offset = 0,
            [APIParameter("Limit", "integer")] int limit = 50,
            [APIParameter("Chain name (ex. 'main')", "string")] string chain = "",
            [APIParameter("Token contract hash", "string")] string contract = "",
            [APIParameter("Token ID", "string")] string token_id = "",
            [APIParameter("Date day match (matchet whole given day)", "string")] string date_day = "",
            [APIParameter("Date (less than)", "string")] string date_less = "",
            [APIParameter("Date (greater than)", "string")] string date_greater = "",
            [APIParameter("Event kind", "string")] string event_kind = "",
            [APIParameter("Event kind (parial match)", "string")] string event_kind_partial = "",
            [APIParameter("Nft name (parial match)", "string")] string nft_name_partial = "",
            [APIParameter("Nft description (parial match)", "string")] string nft_description_partial = "",
            [APIParameter("Show all events, hidden events (claim, unprocessed send/receive/burn) or not hidden events", "string")] string show_events = "not_hidden",
            [APIParameter("Address", "string")] string address = "",
            [APIParameter("Address (parial match)", "string")] string address_partial = "",
            [APIParameter("Return NFT metadata with events", "integer")] int with_metadata = 0,
            [APIParameter("Return NFT series with events", "integer")] int with_series = 0,
            [APIParameter("Return total (slower) or not (faster)", "integer")] int with_total = 0,
            [APIParameter("Fiat currency to calculate Fiat price", "integer")] string fiat_currency = "USD")
        {
            // Results of the query
            long totalResults = 0;
            ApiResults.Event[] eventsArray = null;

            using (var databaseContext = new MainDbContext())
            {
                try
                {
                    if (!ArgValidation.CheckLimit(limit))
                    {
                        throw new APIException("Unsupported value for 'limit' parameter.");
                    }
                    if (!String.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by))
                    {
                        throw new APIException("Unsupported value for 'order_by' parameter.");
                    }
                    if (!ArgValidation.CheckOrderDirection(order_direction))
                    {
                        throw new APIException("Unsupported value for 'order_direction' parameter.");
                    }
                    if (!String.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain))
                    {
                        throw new APIException("Unsupported value for 'chain' parameter.");
                    }
                    
                    if (!String.IsNullOrEmpty(contract) && !ArgValidation.CheckAddress(contract))
                    {
                        throw new APIException("Unsupported value for 'contract' parameter.");
                    }
                    ContractMethods.Drop0x(ref contract);

                    if (!String.IsNullOrEmpty(token_id) && !Regex.IsMatch(token_id, @"^[_\-a-zA-Z0-9]+$"))
                    {
                        throw new APIException("Unsupported value for 'token_id' parameter.");
                    }
                    if (!String.IsNullOrEmpty(date_day) && !Regex.IsMatch(date_day, @"^[0-9.]+$"))
                    {
                        throw new APIException("Unsupported value for 'date_day' parameter.");
                    }
                    if (!String.IsNullOrEmpty(date_less) && !Regex.IsMatch(date_less, @"^[0-9]+$"))
                    {
                        throw new APIException("Unsupported value for 'date_less' parameter.");
                    }
                    if (!String.IsNullOrEmpty(date_greater) && !Regex.IsMatch(date_greater, @"^[0-9]+$"))
                    {
                        throw new APIException("Unsupported value for 'date_greater' parameter.");
                    }
                    if (!String.IsNullOrEmpty(event_kind) && !Regex.IsMatch(event_kind, @"^[_\-a-zA-Z0-9]+$"))
                    {
                        throw new APIException("Unsupported value for 'event_kind' parameter.");
                    }
                    if (!String.IsNullOrEmpty(event_kind_partial) && !Regex.IsMatch(event_kind_partial, @"^[_\-a-zA-Z0-9]+$"))
                    {
                        throw new APIException("Unsupported value for 'event_kind_partial' parameter.");
                    }
                    if (!String.IsNullOrEmpty(nft_name_partial) && !ArgValidation.CheckName(nft_name_partial))
                    {
                        throw new APIException("Unsupported value for 'nft_name_partial' parameter.");
                    }
                    if (!String.IsNullOrEmpty(nft_description_partial) && !Regex.IsMatch(nft_description_partial, @"^[_\-a-zA-Z0-9]+$"))
                    {
                        throw new APIException("Unsupported value for 'nft_description_partial' parameter.");
                    }
                    if (show_events != "all" && show_events != "not_hidden" && show_events != "hidden")
                    {
                        throw new APIException("Unsupported value for 'show_events' parameter.");
                    }
                    if (!String.IsNullOrEmpty(address) && !ArgValidation.CheckAddress(address))
                    {
                        throw new APIException("Unsupported value for 'address' parameter.");
                    }
                    ContractMethods.Drop0x(ref address);

                    if (!String.IsNullOrEmpty(address_partial) && !ArgValidation.CheckAddress(address_partial))
                    {
                        throw new APIException("Unsupported value for 'address_partial' parameter.");
                    }
                    ContractMethods.Drop0x(ref address_partial);

                    if (!String.IsNullOrEmpty(fiat_currency) && !ArgValidation.CheckSymbol(fiat_currency))
                    {
                        throw new APIException("Unsupported value for 'fiat_currency' parameter.");
                    }

                    DateTime startTime = DateTime.Now;

                    // Getting exchange rates in advance.
                    var fiatPricesInUSD = FiatExchangeRateMethods.GetPrices(databaseContext);

                    var query = databaseContext.Events
                        .Include(x => x.Chain)
                        .Include(x => x.Transaction)
                        .Include(x => x.Contract)
                        .Include(x => x.EventKind)
                        .Include(x => x.Address)
                        .Include(x => x.SourceAddress)
                        .Include(x => x.QuoteSymbol)
                        .Include(x => x.Nft).ThenInclude(x => x.Series).ThenInclude(x => x.Contract).ThenInclude(x => x.Chain)
                        .Include(x => x.Nft).ThenInclude(x => x.Series).ThenInclude(x => x.CreatorAddress)
                        .Include(x => x.Nft).ThenInclude(x => x.Series).ThenInclude(x => x.SeriesMode)
                        .Include(x => x.InfusedSymbol)
                        .AsQueryable();

                    //if (show_events != "all" && show_events != "hidden")
                    //{
                    //    query = query.Where(x => x.BURNED != true);
                    //}

                    query = query.Where(x => x.NSFW != true);

                    query = query.Where(x => x.BLACKLISTED != true);

                    if (!String.IsNullOrEmpty(chain))
                        query = query.Where(x => x.Chain.NAME.ToUpper() == chain.ToUpper());

                    if (!String.IsNullOrEmpty(token_id))
                        query = query.Where(x => x.TOKEN_ID == token_id);

                    if (!String.IsNullOrEmpty(contract))
                        query = query.Where(x => x.Contract.HASH.ToUpper() == contract.ToUpper());

                    if (!String.IsNullOrEmpty(date_day))
                        query = query.Where(x => x.DATE_UNIX_SECONDS == UnixSeconds.FromDateTimeString(date_day));

                    if (!String.IsNullOrEmpty(date_less))
                        query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS <= UnixSeconds.FromString(date_less));

                    if (!String.IsNullOrEmpty(date_greater))
                        query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS >= UnixSeconds.FromString(date_greater));

                    if (!String.IsNullOrEmpty(event_kind))
                        query = query.Where(x => x.EventKind.NAME.ToUpper() == event_kind.ToUpper());

                    if (!String.IsNullOrEmpty(event_kind_partial))
                        query = query.Where(x => x.EventKind.NAME.ToUpper().Contains(event_kind_partial.ToUpper()));

                    if (!String.IsNullOrEmpty(nft_name_partial))
                        query = query.Where(x => x.Nft.NAME.ToUpper().Contains(nft_name_partial.ToUpper()));

                    if (!String.IsNullOrEmpty(nft_description_partial))
                        query = query.Where(x => x.Nft.DESCRIPTION.ToUpper().Contains(nft_description_partial.ToUpper()));

                    if (show_events != "all")
                    {
                        if (show_events == "not_hidden")
                        {
                            query = query.Where(x => x.HIDDEN == false);
                        }
                        else
                        {
                            query = query.Where(x => x.HIDDEN == true);
                        }
                    }

                    if (!String.IsNullOrEmpty(address))
                    {
                        var ids = AddressMethods.GetIdsFromExtendedFormat(databaseContext, address, true, chain);

                        query = query.Where(x => 
                            (x.SourceAddressId != null && ids.Contains((int)x.SourceAddressId)) ||
                            ids.Contains(x.AddressId)
                        );
                    }

                    if (!String.IsNullOrEmpty(address_partial))
                        query = query.Where(x => x.Address.ADDRESS.Contains(address_partial) ||
                            x.Address.ADDRESS_NAME.ToUpper().Contains(address_partial.ToUpper()) ||
                            x.Address.USER_NAME.ToUpper().Contains(address_partial.ToUpper()));

                    if (order_direction == "asc")
                    {
                        if (order_by == "date")
                            query = query.OrderBy(x => x.TIMESTAMP_UNIX_SECONDS).ThenBy(x => x.Transaction.INDEX).ThenBy(x => x.INDEX);
                        else if (order_by == "token_id")
                            query = query.OrderBy(x => x.TOKEN_ID);
                        else if (order_by == "price")
                            query = query.OrderBy(x => x.PRICE_USD != 0).OrderBy(x => x.PRICE_USD);
                    }
                    else
                    {
                        if (order_by == "date")
                            query = query.OrderByDescending(x => x.TIMESTAMP_UNIX_SECONDS).ThenByDescending(x => x.Transaction.INDEX).ThenByDescending(x => x.INDEX);
                        else if (order_by == "token_id")
                            query = query.OrderByDescending(x => x.TOKEN_ID);
                        else if (order_by == "price")
                            query = query.OrderBy(x => x.PRICE_USD != 0).OrderByDescending(x => x.PRICE_USD);
                    }

                    query = query.Skip(offset).Take(limit);

                    var events = new List<ApiResults.Event>();
                    var eventIds = new List<int>();

                    foreach (var x in query)
                    {
                        var evnt = new ApiResults.Event()
                        {
                            chain = x.Chain.NAME.ToLower(),
                            contract = ContractMethods.Prepend0x(x.Contract.HASH, x.Chain.NAME),
                            date = x.TIMESTAMP_UNIX_SECONDS.ToString(),
                            transaction_hash = x.Transaction == null ? null : TransactionMethods.Prepend0x(x.Transaction.HASH, x.Chain.NAME),
                            token_id = x.TOKEN_ID,
                            token_amount = x.TOKEN_AMOUNT,
                            event_kind = x.EventKind.NAME,
                            base_symbol = x.Contract.SYMBOL,
                            price = x.PRICE,
                            fiat_price = FiatExchangeRateMethods.Convert(fiatPricesInUSD, x.PRICE_USD, "USD", fiat_currency).ToString("0.####"),
                            fiat_currency = fiat_currency,
                            quote_symbol = x.QuoteSymbol?.SYMBOL,
                            infused_symbol = x.InfusedSymbol?.SYMBOL,
                            infused_value = x.INFUSED_VALUE,
                            address = AddressMethods.Prepend0x(x.Address.ADDRESS, x.Chain.NAME),
                            onchain_name = x.Address.ADDRESS_NAME,
                            source_address = AddressMethods.Prepend0x(x.SourceAddress?.ADDRESS, x.Chain.NAME),
                            source_onchain_name = x.SourceAddress?.ADDRESS_NAME,
                            nft_metadata = with_metadata == 1 && x.Nft != null ? new ApiResults.NftMetadata()
                            {
                                name = x.Nft.NAME,
                                description = x.Nft.DESCRIPTION,
                                image = x.Nft.IMAGE,
                                video = x.Nft.VIDEO,
                                // rom = x.Nft.ROM,
                                // ram = x.Nft.RAM,
                                mint_date = x.Nft.MINT_DATE_UNIX_SECONDS.ToString(),
                                mint_number = x.Nft.MINT_NUMBER.ToString()
                            } : (ApiResults.NftMetadata?)null,
                            series = with_series == 1 && x.Nft?.Series != null ? new ApiResults.Series()
                            {
                                id = x.Nft.Series.SERIES_ID != null ? x.Nft.Series.SERIES_ID.ToString() : null,
                                creator = AddressMethods.Prepend0x(x.Nft.Series.CreatorAddress?.ADDRESS, x.Chain.NAME),
                                current_supply = x.Nft.Series.CURRENT_SUPPLY,
                                max_supply = x.Nft.Series.MAX_SUPPLY,
                                mode_name = x.Nft.Series.SeriesMode?.MODE_NAME,
                                name = x.Nft.Series.NAME,
                                description = x.Nft.Series.DESCRIPTION,
                                image = x.Nft.Series.IMAGE,
                                royalties = x.Nft.Series.ROYALTIES.ToString(),
                                type = x.Nft.Series.TYPE,
                                attrType1 = x.Nft.Series.ATTR_TYPE_1,
                                attrValue1 = x.Nft.Series.ATTR_VALUE_1,
                                attrType2 = x.Nft.Series.ATTR_TYPE_2,
                                attrValue2 = x.Nft.Series.ATTR_VALUE_2,
                                attrType3 = x.Nft.Series.ATTR_TYPE_3,
                                attrValue3 = x.Nft.Series.ATTR_VALUE_3
                            } : (ApiResults.Series?)null
                        };

                        eventIds.Add(x.ID);

                        events.Add(evnt);
                    }

                    eventsArray = events.ToArray();

                    TimeSpan responseTime = DateTime.Now - startTime;

                    Log.Information($"API result generated in {Math.Round(responseTime.TotalSeconds, 3)} sec");
                }
                catch (APIException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    var logMessage = LogEx.Exception("Events()", e);

                    throw new APIException(logMessage, e);
                }
            }

            return new EventsResult { total_results = with_total == 1 ? totalResults : null, events = eventsArray };
        }
    }
}
