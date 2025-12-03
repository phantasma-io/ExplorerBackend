using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Service.Api;

public static class GetEvents
{
    [ProducesResponseType(typeof(EventsResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(EventsResult), "Returns events available on the backend.", false, 10, cacheTag: "events")]
    public static async Task<EventsResult> Execute(
        // ReSharper disable InconsistentNaming
        string order_by = "id",
        string order_direction = "asc",
        int offset = 0,
        int limit = 50,
        string chain = "main",
        string contract = "",
        string token_id = "",
        string date_day = "",
        string date_less = "",
        string date_greater = "",
        string event_kind = "",
        string event_kind_partial = "",
        string nft_name_partial = "",
        string nft_description_partial = "",
        string address = "",
        string address_partial = "",
        string block_hash = "",
        string block_height = "",
        string transaction_hash = "",
        string event_id = "",
        int with_event_data = 0,
        int with_metadata = 0,
        int with_series = 0,
        int with_fiat = 0,
        int with_nsfw = 0,
        int with_blacklisted = 0,
        int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        // Results of the query
        long totalResults = 0;
        Event[] eventsArray;
        const string fiatCurrency = "USD";

        //chain is not considered a filter atm
        var filter = !string.IsNullOrEmpty(contract) || !string.IsNullOrEmpty(token_id) ||
                     !string.IsNullOrEmpty(date_day) || !string.IsNullOrEmpty(date_less) ||
                     !string.IsNullOrEmpty(date_greater) || !string.IsNullOrEmpty(event_kind) ||
                     !string.IsNullOrEmpty(event_kind_partial) || !string.IsNullOrEmpty(nft_name_partial) ||
                     !string.IsNullOrEmpty(nft_description_partial) || !string.IsNullOrEmpty(address_partial) ||
                     !string.IsNullOrEmpty(block_hash) || !string.IsNullOrEmpty(block_height) ||
                     !string.IsNullOrEmpty(transaction_hash);

        try
        {
            #region ArgValidation

            if ( !ArgValidation.CheckLimit(limit, filter) )
                throw new ApiParameterException("Unsupported value for 'limit' parameter.");

            if ( !ArgValidation.CheckOffset(offset) )
                throw new ApiParameterException("Unsupported value for 'offset' parameter.");

            if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            if ( !ArgValidation.CheckOrderDirection(order_direction) )
                throw new ApiParameterException("Unsupported value for 'order_direction' parameter.");

            if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            if ( !string.IsNullOrEmpty(contract) && !ArgValidation.CheckHash(contract, true) )
                throw new ApiParameterException("Unsupported value for 'contract' parameter.");

            if ( !string.IsNullOrEmpty(token_id) && !ArgValidation.CheckTokenId(token_id) )
                throw new ApiParameterException("Unsupported value for 'token_id' parameter.");

            if ( !string.IsNullOrEmpty(date_day) && !Regex.IsMatch(date_day, @"^[0-9.]+$") )
                throw new ApiParameterException("Unsupported value for 'date_day' parameter.");

            if ( !string.IsNullOrEmpty(date_less) && !ArgValidation.CheckNumber(date_less) )
                throw new ApiParameterException("Unsupported value for 'date_less' parameter.");

            if ( !string.IsNullOrEmpty(date_greater) && !ArgValidation.CheckNumber(date_greater) )
                throw new ApiParameterException("Unsupported value for 'date_greater' parameter.");

            if ( !string.IsNullOrEmpty(event_kind) && !ArgValidation.CheckString(event_kind, true) )
                throw new ApiParameterException("Unsupported value for 'event_kind' parameter.");

            if ( !string.IsNullOrEmpty(event_kind_partial) && !ArgValidation.CheckString(event_kind_partial, true) )
                throw new ApiParameterException("Unsupported value for 'event_kind_partial' parameter.");

            if ( !string.IsNullOrEmpty(nft_name_partial) && !ArgValidation.CheckName(nft_name_partial) )
                throw new ApiParameterException("Unsupported value for 'nft_name_partial' parameter.");

            if ( !string.IsNullOrEmpty(nft_description_partial) &&
                 !Regex.IsMatch(nft_description_partial, @"^[_\-a-zA-Z0-9]+$") )
                throw new ApiParameterException("Unsupported value for 'nft_description_partial' parameter.");

            if ( !string.IsNullOrEmpty(address) && !ArgValidation.CheckAddress(address) )
                throw new ApiParameterException("Unsupported value for 'address' parameter.");

            if ( !string.IsNullOrEmpty(address) && string.IsNullOrEmpty(chain) )
                throw new ApiParameterException("Pass chain when using address filter.");

            if ( !string.IsNullOrEmpty(address_partial) && !ArgValidation.CheckAddress(address_partial) )
                throw new ApiParameterException("Unsupported value for 'address_partial' parameter.");

            if ( !string.IsNullOrEmpty(block_hash) && !ArgValidation.CheckHash(block_hash) )
                throw new ApiParameterException("Unsupported value for 'block_hash' parameter.");

            if ( !string.IsNullOrEmpty(block_height) && !ArgValidation.CheckNumber(block_height) )
                throw new ApiParameterException("Unsupported value for 'block_height' parameter.");

            if ( !string.IsNullOrEmpty(transaction_hash) && !ArgValidation.CheckHash(transaction_hash) )
                throw new ApiParameterException("Unsupported value for 'transaction_hash' parameter.");

            if ( !string.IsNullOrEmpty(event_id) && !ArgValidation.CheckNumber(event_id) )
                throw new ApiParameterException("Unsupported value for 'event_id' parameter.");

            #endregion

            var startTime = DateTime.Now;
            await using MainDbContext databaseContext = new();
            var fiatPricesInUsd = FiatExchangeRateMethods.GetPrices(databaseContext);


            // Getting exchange rates in advance.
            var query = databaseContext.Events.AsQueryable().AsNoTracking();

            #region Filtering

            if ( with_nsfw == 0 )
                query = query.Where(x => x.NSFW != true);

            if ( with_blacklisted == 0 )
                query = query.Where(x => x.BLACKLISTED != true);

            if ( !string.IsNullOrEmpty(chain) ) query = query.Where(x => x.Chain.NAME == chain);

            if ( !string.IsNullOrEmpty(token_id) ) query = query.Where(x => x.TOKEN_ID == token_id);

            if ( !string.IsNullOrEmpty(contract) ) query = query.Where(x => x.Contract.HASH == contract);

            if ( !string.IsNullOrEmpty(date_day) )
                query = query.Where(x => x.DATE_UNIX_SECONDS == UnixSeconds.FromDateTimeString(date_day));

            if ( !string.IsNullOrEmpty(date_less) )
                query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS <= UnixSeconds.FromString(date_less));

            if ( !string.IsNullOrEmpty(date_greater) )
                query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS >= UnixSeconds.FromString(date_greater));

            if ( !string.IsNullOrEmpty(event_kind) ) query = query.Where(x => x.EventKind.NAME == event_kind);

            if ( !string.IsNullOrEmpty(event_kind_partial) )
                query = query.Where(x => x.EventKind.NAME.Contains(event_kind_partial));

            if ( !string.IsNullOrEmpty(nft_name_partial) )
                query = query.Where(x => x.Nft.NAME.Contains(nft_name_partial));

            if ( !string.IsNullOrEmpty(nft_description_partial) )
                query = query.Where(x => x.Nft.DESCRIPTION.Contains(nft_description_partial));

            if ( !string.IsNullOrEmpty(address) ) query = query.Where(x => x.Address.ADDRESS == address);

            if ( !string.IsNullOrEmpty(address_partial) )
                query = query.Where(x => x.Address.ADDRESS.Contains(address_partial) ||
                                         x.Address.ADDRESS_NAME.Contains(address_partial) ||
                                         x.Address.USER_NAME.Contains(address_partial));

            if ( !string.IsNullOrEmpty(block_hash) )
                query = query.Where(x => x.Transaction.Block.HASH == block_hash);

            if ( !string.IsNullOrEmpty(block_height) )
                query = query.Where(x => x.Transaction.Block.HEIGHT == block_height);

            if ( !string.IsNullOrEmpty(transaction_hash) )
                query = query.Where(x => x.Transaction.HASH == transaction_hash);

            if ( !string.IsNullOrEmpty(event_id) && int.TryParse(event_id, out var parsedEventId) )
                query = query.Where(x => x.ID == parsedEventId);

            #endregion

            if ( with_total == 1 )
                // Count total number of results before adding order and limit parts of query.
                totalResults = await query.CountAsync();

            if ( order_direction == "asc" )
                query = order_by switch
                {
                    "date" => query.OrderBy(x => x.TIMESTAMP_UNIX_SECONDS),
                    "token_id" => query.OrderBy(x => x.TOKEN_ID),
                    "id" => query.OrderBy(x => x.ID),
                    _ => query
                };
            else
                query = order_by switch
                {
                    "date" => query.OrderByDescending(x => x.TIMESTAMP_UNIX_SECONDS),
                    "token_id" => query.OrderByDescending(x => x.TOKEN_ID),
                    "id" => query.OrderByDescending(x => x.ID),
                    _ => query
                };

            #region ResultArray

            if ( limit > 0 ) query = query.Skip(offset).Take(limit);

            var eventProjections = await query.Select(x => new EventPayloadMapper.EventProjection
                {
                    ApiEvent = new Event
                    {
                        event_id = x.ID,
                        chain = x.Chain.NAME.ToLower(),
                        date = x.TIMESTAMP_UNIX_SECONDS.ToString(),
                        block_hash = x.Transaction.Block.HASH,
                        transaction_hash = x.Transaction.HASH,
                        token_id = x.TOKEN_ID,
                        payload_json = x.PAYLOAD_JSON,
                        raw_data = x.RAW_DATA,
                        event_kind = x.EventKind.NAME,
                        address = x.Address.ADDRESS,
                        address_name = x.Address.ADDRESS_NAME,
                        contract = new Contract
                        {
                            name = x.Contract.NAME,
                            hash = x.Contract.HASH,
                            symbol = x.Contract.SYMBOL
                        },
                        nft_metadata = with_metadata == 1 && x.Nft != null
                            ? new NftMetadata
                            {
                                name = x.Nft.NAME,
                                description = x.Nft.DESCRIPTION,
                                image = x.Nft.IMAGE,
                                video = x.Nft.VIDEO,
                                rom = x.Nft.ROM,
                                ram = x.Nft.RAM,
                                mint_date = x.Nft.MINT_DATE_UNIX_SECONDS.ToString(),
                                mint_number = x.Nft.MINT_NUMBER.ToString()
                            }
                            : null,
                        series = with_series == 1 && x.Nft != null && x.Nft.Series != null
                            ? new Series
                            {
                                id = x.Nft.Series.ID,
                                series_id = x.Nft.Series.SERIES_ID,
                                creator = x.Nft.Series.CreatorAddress != null
                                    ? x.Nft.Series.CreatorAddress.ADDRESS
                                    : null,
                                current_supply = x.Nft.Series.CURRENT_SUPPLY,
                                max_supply = x.Nft.Series.MAX_SUPPLY,
                                mode_name = x.Nft.Series.SeriesMode != null ? x.Nft.Series.SeriesMode.MODE_NAME : null,
                                name = x.Nft.Series.NAME,
                                description = x.Nft.Series.DESCRIPTION,
                                image = x.Nft.Series.IMAGE,
                                royalties = x.Nft.Series.ROYALTIES.ToString(CultureInfo.InvariantCulture),
                                type = x.Nft.Series.TYPE,
                                attr_type_1 = x.Nft.Series.ATTR_TYPE_1,
                                attr_value_1 = x.Nft.Series.ATTR_VALUE_1,
                                attr_type_2 = x.Nft.Series.ATTR_TYPE_2,
                                attr_value_2 = x.Nft.Series.ATTR_VALUE_2,
                                attr_type_3 = x.Nft.Series.ATTR_TYPE_3,
                                attr_value_3 = x.Nft.Series.ATTR_VALUE_3
                            }
                            : null
                    },
                    ChainId = x.ChainId,
                    TimestampUnixSeconds = x.TIMESTAMP_UNIX_SECONDS,
                    PayloadJson = x.PAYLOAD_JSON,
                    RawData = x.RAW_DATA
                }
            ).ToArrayAsync();

            await EventPayloadMapper.ApplyAsync(databaseContext, eventProjections, with_event_data == 1,
                with_fiat == 1, fiatCurrency, fiatPricesInUsd);

            eventsArray = eventProjections.Select(p => p.ApiEvent).ToArray();

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
            var logMessage = LogEx.Exception("Events()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new EventsResult {total_results = with_total == 1 ? totalResults : null, events = eventsArray};
    }
}
