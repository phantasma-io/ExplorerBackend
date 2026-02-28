#nullable enable
using System;
using System.Collections.Generic;
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
    private sealed class EventPageItem
    {
        public int Id { get; init; }
        public long TimestampUnixSeconds { get; init; }
        public string TokenId { get; init; } = string.Empty;
        public required EventPayloadMapper.EventProjection Projection { get; init; }
    }

    [ProducesResponseType(typeof(EventsResult), (int)HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(EventsResult), "Returns events available on the backend.", false, 10, cacheTag: "events")]
    public static async Task<EventsResult> Execute(
        // ReSharper disable InconsistentNaming
        string order_by = "id",
        string order_direction = "asc",
        int limit = 50,
        string cursor = "",
        string chain = "",
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
        string q = "",
        string block_hash = "",
        string block_height = "",
        string transaction_hash = "",
        string event_id = "",
        int with_event_data = 0,
        int with_metadata = 0,
        int with_series = 0,
        int with_fiat = 0,
        int with_nsfw = 0,
        int with_blacklisted = 0
    // ReSharper enable InconsistentNaming
    )
    {
        // Results of the query
        Event[] eventsArray;
        const string fiatCurrency = "USD";
        string? nextCursor = null;
        var qTrimmed = string.IsNullOrWhiteSpace(q) ? string.Empty : q.Trim();
        var qIsAddress = !string.IsNullOrEmpty(qTrimmed) &&
                         PhantasmaPhoenix.Cryptography.Address.IsValidAddress(qTrimmed);

        try
        {
            #region ArgValidation

            if (!ArgValidation.CheckLimit(limit))
                throw new ApiParameterException("Unsupported value for 'limit' parameter.");

            if (!string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by))
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            if (!ArgValidation.CheckOrderDirection(order_direction))
                throw new ApiParameterException("Unsupported value for 'order_direction' parameter.");

            if (!string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain))
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            if (!string.IsNullOrEmpty(contract) && !ArgValidation.CheckHash(contract, true))
                throw new ApiParameterException("Unsupported value for 'contract' parameter.");

            if (!string.IsNullOrEmpty(token_id) && !ArgValidation.CheckTokenId(token_id))
                throw new ApiParameterException("Unsupported value for 'token_id' parameter.");

            if (!string.IsNullOrEmpty(date_day) && !Regex.IsMatch(date_day, @"^[0-9.]+$"))
                throw new ApiParameterException("Unsupported value for 'date_day' parameter.");

            if (!string.IsNullOrEmpty(date_less) && !ArgValidation.CheckNumber(date_less))
                throw new ApiParameterException("Unsupported value for 'date_less' parameter.");

            if (!string.IsNullOrEmpty(date_greater) && !ArgValidation.CheckNumber(date_greater))
                throw new ApiParameterException("Unsupported value for 'date_greater' parameter.");

            if (!string.IsNullOrEmpty(event_kind) && !ArgValidation.CheckString(event_kind, true))
                throw new ApiParameterException("Unsupported value for 'event_kind' parameter.");

            if (!string.IsNullOrEmpty(event_kind_partial) && !ArgValidation.CheckString(event_kind_partial, true))
                throw new ApiParameterException("Unsupported value for 'event_kind_partial' parameter.");

            if (!string.IsNullOrEmpty(nft_name_partial) && !ArgValidation.CheckName(nft_name_partial))
                throw new ApiParameterException("Unsupported value for 'nft_name_partial' parameter.");

            if (!string.IsNullOrEmpty(nft_description_partial) &&
                 !Regex.IsMatch(nft_description_partial, @"^[_\-a-zA-Z0-9]+$"))
                throw new ApiParameterException("Unsupported value for 'nft_description_partial' parameter.");

            if (!string.IsNullOrEmpty(address) && !ArgValidation.CheckAddress(address))
                throw new ApiParameterException("Unsupported value for 'address' parameter.");

            if (!string.IsNullOrEmpty(address_partial) && !ArgValidation.CheckAddress(address_partial))
                throw new ApiParameterException("Unsupported value for 'address_partial' parameter.");

            if (!string.IsNullOrEmpty(qTrimmed) && !ArgValidation.CheckGeneralSearch(qTrimmed))
                throw new ApiParameterException("Unsupported value for 'q' parameter.");

            if (!string.IsNullOrEmpty(block_hash) && !ArgValidation.CheckHash(block_hash))
                throw new ApiParameterException("Unsupported value for 'block_hash' parameter.");

            if (!string.IsNullOrEmpty(block_height) && !ArgValidation.CheckNumber(block_height))
                throw new ApiParameterException("Unsupported value for 'block_height' parameter.");

            if (!string.IsNullOrEmpty(transaction_hash) && !ArgValidation.CheckHash(transaction_hash))
                throw new ApiParameterException("Unsupported value for 'transaction_hash' parameter.");

            if (!string.IsNullOrEmpty(event_id) && !ArgValidation.CheckNumber(event_id))
                throw new ApiParameterException("Unsupported value for 'event_id' parameter.");

            #endregion

            var cursorToken = CursorPagination.ParseCursor(cursor);
            var sortDirection = CursorPagination.ParseSortDirection(order_direction);
            var orderBy = string.IsNullOrWhiteSpace(order_by) ? "id" : order_by;
            var addressScopedRequest = !string.IsNullOrWhiteSpace(address) || qIsAddress;

            if (addressScopedRequest && string.Equals(orderBy, "id", StringComparison.OrdinalIgnoreCase))
            {
                // Address timelines are consumed chronologically; id ordering adds unstable scans
                // and conflicts with the frontend default that switches to date immediately after mount.
                orderBy = "date";
            }

            long? parsedBlockHeightFilter = null;

            if (!string.IsNullOrEmpty(block_height))
            {
                if (!long.TryParse(block_height, NumberStyles.None, CultureInfo.InvariantCulture,
                        out var blockHeightValue))
                    throw new ApiParameterException("Unsupported value for 'block_height' parameter.");

                parsedBlockHeightFilter = blockHeightValue;
            }

            var orderDefinitions =
                new Dictionary<string, CursorOrderDefinition<EventPageItem>>(StringComparer.OrdinalIgnoreCase)
                {
                    {
                        "id",
                        new CursorOrderDefinition<EventPageItem>(
                            "id",
                            new CursorOrderSegment<EventPageItem, int>(
                                x => x.Id,
                                value => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)))
                    },
                    {
                        "date",
                        new CursorOrderDefinition<EventPageItem>(
                            "date",
                            new CursorOrderSegment<EventPageItem, long>(
                                x => x.TimestampUnixSeconds,
                                value => long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)))
                    },
                    {
                        "token_id",
                        new CursorOrderDefinition<EventPageItem>(
                            "token_id",
                            new CursorOrderSegment<EventPageItem, string>(
                                x => x.TokenId,
                                value => value))
                    }
                };

            if (!orderDefinitions.TryGetValue(orderBy, out var orderDefinition))
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            var startTime = DateTime.Now;
            await using MainDbContext databaseContext = new();
            var fiatPricesInUsd = FiatExchangeRateMethods.GetPrices(databaseContext);

            int? chainId = null;

            if (!string.IsNullOrEmpty(chain))
            {
                chainId = await databaseContext.Chains.AsNoTracking()
                    .Where(x => x.NAME == chain)
                    .Select(x => (int?)x.ID)
                    .FirstOrDefaultAsync();

                if (!chainId.HasValue)
                    throw new ApiParameterException("Unsupported value for 'chain' parameter.");
            }

            Dictionary<string, int[]>? eventKindIds = null;

            if (!string.IsNullOrEmpty(event_kind))
                eventKindIds = await EventKindMethods.GetAvailableEventKindIdsAsync(databaseContext, chainId);

            // Getting exchange rates in advance.
            var query = databaseContext.Events.AsQueryable().AsNoTracking();

            #region Filtering
            var qUpper = string.IsNullOrEmpty(qTrimmed) ? string.Empty : qTrimmed.ToUpperInvariant();

            if (!string.IsNullOrEmpty(qUpper))
            {
                var isHex = ArgValidation.CheckBase16(qTrimmed);
                var isFullHash = isHex && qUpper.Length >= 64;
                var isNumber = ArgValidation.CheckNumber(qTrimmed);
                var isAddress = qIsAddress;
                var isHashFragment = isHex && !isFullHash && qUpper.Length >= 8;
                var isTextSearch = qTrimmed.Length >= 3;

                // Use a single query strategy per input shape. This avoids the heavy OR predicate
                // that mixes unrelated joins and prevents stable index usage on large event datasets.
                if (isFullHash)
                {
                    var matchingTransactionIdsByHash = await databaseContext.Transactions
                        .AsNoTracking()
                        .Where(x => x.HASH == qUpper)
                        .Select(x => x.ID)
                        .ToArrayAsync();

                    var matchingBlockIds = await databaseContext.Blocks
                        .AsNoTracking()
                        .Where(x => x.HASH == qUpper)
                        .Select(x => x.ID)
                        .ToArrayAsync();

                    var matchingTransactionIdsByBlockHash = matchingBlockIds.Length == 0
                        ? Array.Empty<int>()
                        : await databaseContext.Transactions
                            .AsNoTracking()
                            .Where(x => matchingBlockIds.Contains(x.BlockId))
                            .Select(x => x.ID)
                            .ToArrayAsync();

                    var matchingTransactionIds = matchingTransactionIdsByHash
                        .Concat(matchingTransactionIdsByBlockHash)
                        .Distinct()
                        .ToArray();

                    if (matchingTransactionIds.Length == 0)
                    {
                        return new EventsResult
                        {
                            total_results = null,
                            events = Array.Empty<Event>(),
                            next_cursor = null
                        };
                    }

                    query = query.Where(x => matchingTransactionIds.Contains(x.TransactionId));
                }
                else if (isAddress)
                {
                    var resolvedAddressIdsQuery = databaseContext.Addresses
                        .AsNoTracking()
                        .Where(a => a.ADDRESS == qTrimmed);

                    if (chainId.HasValue)
                        resolvedAddressIdsQuery = resolvedAddressIdsQuery.Where(a => a.ChainId == chainId.Value);

                    var resolvedAddressIds = await resolvedAddressIdsQuery
                        .Select(a => a.ID)
                        .ToArrayAsync();

                    if (resolvedAddressIds.Length == 0)
                    {
                        return new EventsResult
                        {
                            total_results = null,
                            events = Array.Empty<Event>(),
                            next_cursor = null
                        };
                    }

                    // Address q uses two indexed branches (AddressId and TargetAddressId) merged by ID.
                    // This avoids a wide OR predicate that can degrade into PK scans for sparse addresses.
                    var sourceEventIdsQuery = databaseContext.Events
                        .AsNoTracking()
                        .Where(x => resolvedAddressIds.Contains(x.AddressId))
                        .Select(x => x.ID);

                    var targetEventIdsQuery = databaseContext.Events
                        .AsNoTracking()
                        .Where(x => x.TargetAddressId.HasValue && resolvedAddressIds.Contains(x.TargetAddressId.Value))
                        .Select(x => x.ID);

                    if (chainId.HasValue)
                    {
                        sourceEventIdsQuery = databaseContext.Events
                            .AsNoTracking()
                            .Where(x => x.ChainId == chainId.Value && resolvedAddressIds.Contains(x.AddressId))
                            .Select(x => x.ID);

                        targetEventIdsQuery = databaseContext.Events
                            .AsNoTracking()
                            .Where(x => x.ChainId == chainId.Value && x.TargetAddressId.HasValue &&
                                        resolvedAddressIds.Contains(x.TargetAddressId.Value))
                            .Select(x => x.ID);
                    }

                    var matchingEventIds = sourceEventIdsQuery
                        .Concat(targetEventIdsQuery)
                        .Distinct();

                    query = query.Where(x => matchingEventIds.Contains(x.ID));
                }
                else if (isNumber)
                {
                    if (!long.TryParse(qTrimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var qHeight))
                        throw new ApiParameterException("Unsupported value for 'q' parameter.");

                    var blockIdsByHeight = databaseContext.Blocks
                        .AsNoTracking()
                        .Where(x => x.HEIGHT == qHeight);

                    if (chainId.HasValue)
                        blockIdsByHeight = blockIdsByHeight.Where(x => x.ChainId == chainId.Value);

                    var transactionIdsByHeight = databaseContext.Transactions
                        .AsNoTracking()
                        .Where(x => blockIdsByHeight.Select(b => b.ID).Contains(x.BlockId))
                        .Select(x => x.ID);

                    query = query.Where(x => transactionIdsByHeight.Contains(x.TransactionId));
                }
                else if (isHashFragment)
                {
                    // Partial hash lookup via q is intentionally disabled to avoid full-scan hash predicates.
                    return new EventsResult
                    {
                        total_results = null,
                        events = Array.Empty<Event>(),
                        next_cursor = null
                    };
                }
                else if (isTextSearch)
                {
                    eventKindIds ??= await EventKindMethods.GetAvailableEventKindIdsAsync(databaseContext, chainId);

                    if (eventKindIds.TryGetValue(qTrimmed, out var exactEventKindIds) && exactEventKindIds.Length > 0)
                    {
                        query = query.Where(x => exactEventKindIds.Contains(x.EventKindId));
                    }
                    else
                    {
                        var partialEventKindIds = eventKindIds
                            .Where(x => x.Key.Contains(qTrimmed, StringComparison.OrdinalIgnoreCase))
                            .SelectMany(x => x.Value)
                            .Distinct()
                            .ToArray();

                        if (partialEventKindIds.Length == 0)
                        {
                            return new EventsResult
                            {
                                total_results = null,
                                events = Array.Empty<Event>(),
                                next_cursor = null
                            };
                        }

                        query = query.Where(x => partialEventKindIds.Contains(x.EventKindId));
                    }
                }
                else
                {
                    return new EventsResult
                    {
                        total_results = null,
                        events = Array.Empty<Event>(),
                        next_cursor = null
                    };
                }
            }

            if (with_nsfw == 0)
                query = query.Where(x => x.NSFW != true);

            if (with_blacklisted == 0)
                query = query.Where(x => x.BLACKLISTED != true);

            if (chainId.HasValue) query = query.Where(x => x.ChainId == chainId.Value);

            if (!string.IsNullOrEmpty(token_id)) query = query.Where(x => x.TOKEN_ID == token_id);

            if (!string.IsNullOrEmpty(contract)) query = query.Where(x => x.Contract.HASH == contract);

            if (!string.IsNullOrEmpty(date_day))
                query = query.Where(x => x.DATE_UNIX_SECONDS == UnixSeconds.FromDateTimeString(date_day));

            if (!string.IsNullOrEmpty(date_less))
                query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS <= UnixSeconds.FromString(date_less));

            if (!string.IsNullOrEmpty(date_greater))
                query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS >= UnixSeconds.FromString(date_greater));

            if (!string.IsNullOrEmpty(event_kind))
            {
                eventKindIds ??= await EventKindMethods.GetAvailableEventKindIdsAsync(databaseContext, chainId);

                if (eventKindIds.TryGetValue(event_kind, out var eventKindIdSet) && eventKindIdSet.Length > 0)
                    query = query.Where(x => eventKindIdSet.Contains(x.EventKindId));
                else
                    return new EventsResult
                    {
                        total_results = null,
                        events = Array.Empty<Event>(),
                        next_cursor = null
                    };
            }

            if (!string.IsNullOrEmpty(event_kind_partial))
                query = query.Where(x => x.EventKind.NAME.Contains(event_kind_partial));

            if (!string.IsNullOrEmpty(nft_name_partial))
                query = query.Where(x => x.Nft.NAME.Contains(nft_name_partial));

            if (!string.IsNullOrEmpty(nft_description_partial))
                query = query.Where(x => x.Nft.DESCRIPTION.Contains(nft_description_partial));

            if (!string.IsNullOrEmpty(address))
            {
                var isValidAddress = PhantasmaPhoenix.Cryptography.Address.IsValidAddress(address);

                var resolvedAddressIdsQuery = databaseContext.Addresses
                    .AsNoTracking()
                    .Where(a => isValidAddress ? a.ADDRESS == address : a.ADDRESS_NAME == address);

                if (chainId.HasValue)
                    resolvedAddressIdsQuery = resolvedAddressIdsQuery.Where(a => a.ChainId == chainId.Value);

                var resolvedAddressIds = await resolvedAddressIdsQuery
                    .Select(a => a.ID)
                    .ToArrayAsync();

                if (resolvedAddressIds.Length == 0)
                {
                    return new EventsResult
                    {
                        total_results = null,
                        events = Array.Empty<Event>(),
                        next_cursor = null
                    };
                }

                query = resolvedAddressIds.Length == 1
                    ? query.Where(x => x.AddressId == resolvedAddressIds[0])
                    : query.Where(x => resolvedAddressIds.Contains(x.AddressId));
            }

            if (!string.IsNullOrEmpty(address_partial))
                query = query.Where(x => x.Address.ADDRESS.Contains(address_partial) ||
                                         x.Address.ADDRESS_NAME.Contains(address_partial) ||
                                         x.Address.USER_NAME.Contains(address_partial));

            if (!string.IsNullOrEmpty(block_hash))
                query = query.Where(x => x.Transaction.Block.HASH == block_hash);

            if (parsedBlockHeightFilter.HasValue)
                query = query.Where(x => x.Transaction.Block.HEIGHT == parsedBlockHeightFilter.Value);

            if (!string.IsNullOrEmpty(transaction_hash))
                query = query.Where(x => x.Transaction.HASH == transaction_hash);

            if (!string.IsNullOrEmpty(event_id) && int.TryParse(event_id, out var parsedEventId))
                query = query.Where(x => x.ID == parsedEventId);

            #endregion

            #region ResultArray

            var pageQuery = query.Select(x => new EventPageItem
            {
                Id = x.ID,
                TimestampUnixSeconds = x.TIMESTAMP_UNIX_SECONDS,
                TokenId = x.TOKEN_ID ?? string.Empty,
                Projection = new EventPayloadMapper.EventProjection
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
                                imageURL = x.Nft.IMAGE,
                                videoURL = x.Nft.VIDEO,
                                infoURL = x.Nft.INFO_URL,
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
                    RawData = x.RAW_DATA,
                    NftMetadata = x.Nft != null ? x.Nft.METADATA : null,
                    SeriesMetadata = x.Nft != null && x.Nft.Series != null ? x.Nft.Series.METADATA : null,
                    NftCreator = x.Nft != null && x.Nft.CreatorAddress != null ? x.Nft.CreatorAddress.ADDRESS : null
                }
            });

            EventPayloadMapper.EventProjection[] eventProjections;

            var cursorFiltered = CursorPagination.ApplyCursor(pageQuery, orderDefinition, sortDirection, cursorToken,
                x => x.Id);
            var orderedQuery =
                CursorPagination.ApplyOrdering(cursorFiltered, orderDefinition, sortDirection, x => x.Id);
            var page = await CursorPagination.ReadPageAsync(orderedQuery, orderDefinition, sortDirection, x => x.Id,
                limit);
            eventProjections = page.Items.Select(x => x.Projection).ToArray();
            nextCursor = page.NextCursor;

            await EventPayloadMapper.ApplyAsync(databaseContext, eventProjections, with_event_data == 1,
                with_fiat == 1, fiatCurrency, fiatPricesInUsd);

            foreach (var projection in eventProjections)
            {
                if (projection.ApiEvent.nft_metadata != null)
                    projection.ApiEvent.nft_metadata.metadata = MetadataMapper.FromNft(
                        projection.NftMetadata,
                        projection.ApiEvent.nft_metadata,
                        projection.NftCreator,
                        projection.ApiEvent.series?.series_id);

                if (projection.ApiEvent.series != null)
                    projection.ApiEvent.series.metadata =
                        MetadataMapper.FromSeries(projection.SeriesMetadata, projection.ApiEvent.series);
            }

            eventsArray = eventProjections.Select(p => p.ApiEvent).ToArray();

            #endregion

            var responseTime = DateTime.Now - startTime;

            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch (ApiParameterException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var logMessage = LogEx.Exception("Events()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new EventsResult
        {
            total_results = null,
            events = eventsArray,
            next_cursor = nextCursor
        };
    }
}
