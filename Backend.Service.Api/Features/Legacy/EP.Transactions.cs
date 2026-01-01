using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Service.Api;

public static class GetTransactions
{
    private sealed class TransactionPageItem
    {
        public int Id { get; init; }
        public string Hash { get; init; } = string.Empty;
        public int Index { get; init; }
        public EventPayloadMapper.TransactionProjection Projection { get; init; }
    }

    [ProducesResponseType(typeof(TransactionResult), (int)HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(TransactionResult), "Returns the transaction on the backend.", false, 60, cacheTag: "transactions")]
    public static async Task<TransactionResult> Execute(
        // ReSharper disable InconsistentNaming
        string order_by = "id",
        string order_direction = "asc",
        int offset = 0,
        int limit = 50,
        string cursor = "",
        string hash = "",
        string hash_partial = "",
        string address = "",
        string q = "",
        string date_less = "",
        string date_greater = "",
        string block_hash = "",
        string block_height = "",
        string chain = "main",
        int with_nft = 0,
        int with_events = 0,
        int with_event_data = 0,
        int with_fiat = 0,
        int with_script = 0,
        int with_neighbors = 0,
        int with_total = 0
    // ReSharper enable InconsistentNaming
    )
    {
        long totalResults = 0;
        Transaction[] transactions = null;
        string previousHash = null;
        string nextHash = null;
        string? nextCursor = null;
        var useCursor = false;
        var hashUpper = string.IsNullOrEmpty(hash) ? string.Empty : hash.ToUpper();
        var hashPartialUpper = string.IsNullOrEmpty(hash_partial) ? string.Empty : hash_partial.ToUpper();
        var qTrimmed = string.IsNullOrWhiteSpace(q) ? string.Empty : q.Trim();
        var qUpper = string.IsNullOrEmpty(qTrimmed) ? string.Empty : qTrimmed.ToUpperInvariant();

        const string fiatCurrency = "USD";
        var filter = !string.IsNullOrEmpty(hashUpper) || !string.IsNullOrEmpty(hashPartialUpper) ||
                     !string.IsNullOrEmpty(address) || !string.IsNullOrEmpty(date_less) ||
                     !string.IsNullOrEmpty(date_greater)
                     || !string.IsNullOrEmpty(block_hash) || !string.IsNullOrEmpty(block_height)
                     || !string.IsNullOrEmpty(qTrimmed);

        try
        {
            #region ArgValidation

            if (!string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by))
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            if (!ArgValidation.CheckOrderDirection(order_direction))
                throw new ApiParameterException("Unsupported value for 'order_direction' parameter.");

            if (!ArgValidation.CheckLimit(limit, filter))
                throw new ApiParameterException("Unsupported value for 'limit' parameter.");

            if (!ArgValidation.CheckOffset(offset))
                throw new ApiParameterException("Unsupported value for 'offset' parameter.");

            if (!string.IsNullOrEmpty(hashUpper) && !ArgValidation.CheckHash(hashUpper))
                throw new ApiParameterException("Unsupported value for 'hash' parameter.");

            if (!string.IsNullOrEmpty(hashPartialUpper) && !ArgValidation.CheckHash(hashPartialUpper))
                throw new ApiParameterException("Unsupported value for 'hash_partial' parameter.");

            if (!string.IsNullOrEmpty(qTrimmed) && !ArgValidation.CheckGeneralSearch(qTrimmed))
                throw new ApiParameterException("Unsupported value for 'q' parameter.");

            if (!string.IsNullOrEmpty(address) && !ArgValidation.CheckAddress(address))
                throw new ApiParameterException("Unsupported value for 'address' parameter.");

            if (!string.IsNullOrEmpty(date_less) && !ArgValidation.CheckNumber(date_less))
                throw new ApiParameterException("Unsupported value for 'date_less' parameter.");

            if (!string.IsNullOrEmpty(date_greater) && !ArgValidation.CheckNumber(date_greater))
                throw new ApiParameterException("Unsupported value for 'date_greater' parameter.");

            if (!string.IsNullOrEmpty(block_hash) && !ArgValidation.CheckHash(block_hash.ToUpper()))
                throw new ApiParameterException("Unsupported value for 'block_hash' parameter.");

            if (!string.IsNullOrEmpty(block_height) && !ArgValidation.CheckNumber(block_height))
                throw new ApiParameterException("Unsupported value for 'block_height' parameter.");

            if (!string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain))
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            #endregion

            var cursorToken = CursorPagination.ParseCursor(cursor);
            var sortDirection = CursorPagination.ParseSortDirection(order_direction);
            var orderBy = string.IsNullOrWhiteSpace(order_by) ? "id" : order_by;

            var orderDefinitions =
                new Dictionary<string, CursorOrderDefinition<TransactionPageItem>>(StringComparer.OrdinalIgnoreCase)
                {
                    {
                        "id",
                        new CursorOrderDefinition<TransactionPageItem>(
                            "id",
                            new CursorOrderSegment<TransactionPageItem, int>(
                                x => x.Id,
                                value => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)))
                    },
                    {
                        "hash",
                        new CursorOrderDefinition<TransactionPageItem>(
                            "hash",
                            new CursorOrderSegment<TransactionPageItem, string>(
                                x => x.Hash,
                                value => value))
                    },
                    {
                        "index",
                        new CursorOrderDefinition<TransactionPageItem>(
                            "index",
                            new CursorOrderSegment<TransactionPageItem, int>(
                                x => x.Index,
                                value => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)))
                    }
                };

            if (!orderDefinitions.TryGetValue(orderBy, out var orderDefinition))
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            var useCursorForList = string.IsNullOrEmpty(hashUpper);
            useCursor = useCursorForList && CursorPagination.ShouldUseCursor(cursorToken, offset, with_total);

            var startTime = DateTime.Now;
            await using MainDbContext databaseContext = new();
            var fiatPricesInUsd = FiatExchangeRateMethods.GetPrices(databaseContext);

            var query = databaseContext.Transactions.AsQueryable().AsNoTracking();

            #region Filtering

            if (!string.IsNullOrEmpty(qUpper))
            {
                var isNumber = ArgValidation.CheckNumber(qTrimmed);
                var isHex = ArgValidation.CheckBase16(qTrimmed);
                var isFullHash = isHex && qUpper.Length >= 64;
                var isHexPartial = isHex && !isFullHash;
                var isAddress = PhantasmaPhoenix.Cryptography.Address.IsValidAddress(qTrimmed);
                var treatAsHashPartial = !isNumber && !isAddress && !isFullHash;

                query = query.Where(x =>
                    (isFullHash && x.HASH == qUpper) ||
                    (isHexPartial && x.HASH.Contains(qUpper)) ||
                    (isNumber && x.Block.HEIGHT == qTrimmed) ||
                    (isAddress && x.TransactionAddresses.Any(y => y.Address.ADDRESS == qTrimmed)) ||
                    (treatAsHashPartial && x.HASH.Contains(qUpper)));
            }

            if (!string.IsNullOrEmpty(hashUpper))
                query = query.Where(x => x.HASH == hashUpper);

            if (!string.IsNullOrEmpty(hashPartialUpper))
                query = query.Where(x =>
                    x.HASH.Contains(hashPartialUpper) ||
                    x.Block.HASH.Contains(hashPartialUpper) ||
                    x.Block.HEIGHT == hashPartialUpper ||
                    x.Block.HEIGHT.Contains(hashPartialUpper));

            if (!string.IsNullOrEmpty(date_less))
                query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS <= UnixSeconds.FromString(date_less));

            if (!string.IsNullOrEmpty(date_greater))
                query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS >= UnixSeconds.FromString(date_greater));

            if (!string.IsNullOrEmpty(address))
            {
                var addressId = 0;
                if (PhantasmaPhoenix.Cryptography.Address.IsValidAddress(address))
                {
                    addressId = await databaseContext.Addresses.Where(x => x.ADDRESS == address).Select(x => x.ID).FirstOrDefaultAsync();
                }
                else
                {
                    addressId = await databaseContext.Addresses.Where(x => x.ADDRESS_NAME == address).Select(x => x.ID).FirstOrDefaultAsync();
                }

                query = query.Where(x => x.TransactionAddresses.Any(y => y.AddressId == addressId));
            }

            if (!string.IsNullOrEmpty(block_hash))
                query = query.Where(x => x.Block.HASH == block_hash.ToUpper());

            if (!string.IsNullOrEmpty(block_height))
                query = query.Where(x => x.Block.HEIGHT == block_height);

            if (!string.IsNullOrEmpty(chain)) query = query.Where(x => x.Block.Chain.NAME == chain);

            #endregion

            if (!useCursor && with_total == 1)
                totalResults = await query.CountAsync();

            var pageQuery = query.Select(x => new TransactionPageItem
            {
                Id = x.ID,
                Hash = x.HASH,
                Index = x.INDEX,
                Projection = new EventPayloadMapper.TransactionProjection
                {
                    TransactionId = x.ID,
                    ChainId = x.Block.ChainId,
                    ApiTransaction = new Transaction
                    {
                        hash = x.HASH,
                        block_hash = x.Block.HASH,
                        block_height = x.Block.HEIGHT,
                        index = x.INDEX,
                        date = x.TIMESTAMP_UNIX_SECONDS.ToString(),
                        fee = x.FEE,
                        fee_raw = x.FEE_RAW,
                        script_raw = with_script == 1 ? x.SCRIPT_RAW : null,
                        result = x.RESULT,
                        payload = x.PAYLOAD,
                        expiration = x.EXPIRATION.ToString(),
                        gas_price = x.GAS_PRICE,
                        gas_price_raw = x.GAS_PRICE_RAW,
                        gas_limit = x.GAS_LIMIT,
                        gas_limit_raw = x.GAS_LIMIT_RAW,
                        state = x.State != null ? x.State.NAME : null,
                        sender = x.Sender != null
                            ? new Address
                            {
                                address_name = x.Sender.ADDRESS_NAME,
                                address = x.Sender.ADDRESS
                            }
                            : null,
                        gas_payer = x.GasPayer != null
                            ? new Address
                            {
                                address_name = x.GasPayer.ADDRESS_NAME,
                                address = x.GasPayer.ADDRESS
                            }
                            : null,
                        gas_target = x.GasTarget != null
                            ? new Address
                            {
                                address_name = x.GasTarget.ADDRESS_NAME,
                                address = x.GasTarget.ADDRESS
                            }
                            : null
                    },
                    EventProjections = with_events == 1
                        ? x.Events.Select(e => new EventPayloadMapper.EventProjection
                        {
                            ApiEvent = new Event
                            {
                                event_id = e.ID,
                                chain = e.Chain.NAME.ToLower(),
                                date = e.TIMESTAMP_UNIX_SECONDS.ToString(),
                                transaction_hash = x.HASH,
                                token_id = e.TOKEN_ID,
                                payload_json = e.PAYLOAD_JSON,
                                raw_data = e.RAW_DATA,
                                event_kind = e.EventKind.NAME,
                                address = e.Address.ADDRESS,
                                address_name = e.Address.ADDRESS_NAME,
                                contract = e.Contract != null
                                        ? new Contract
                                        {
                                            name = e.Contract.NAME,
                                            hash = e.Contract.HASH,
                                            symbol = e.Contract.SYMBOL
                                        }
                                        : null,
                                nft_metadata = with_nft == 1 && e.Nft != null
                                        ? new NftMetadata
                                        {
                                            name = e.Nft.NAME,
                                            description = e.Nft.DESCRIPTION,
                                            imageURL = e.Nft.IMAGE,
                                            videoURL = e.Nft.VIDEO,
                                            infoURL = e.Nft.INFO_URL,
                                            rom = e.Nft.ROM,
                                            ram = e.Nft.RAM,
                                            mint_date = e.Nft.MINT_DATE_UNIX_SECONDS.ToString(),
                                            mint_number = e.Nft.MINT_NUMBER.ToString()
                                        }
                                        : null,
                                series = with_nft == 1 && e.Nft != null && e.Nft.Series != null
                                        ? new Series
                                        {
                                            id = e.Nft.Series.ID,
                                            series_id = e.Nft.Series.SERIES_ID,
                                            creator = e.Nft.Series.CreatorAddress != null
                                                ? e.Nft.Series.CreatorAddress.ADDRESS
                                                : null,
                                            current_supply = e.Nft.Series.CURRENT_SUPPLY,
                                            max_supply = e.Nft.Series.MAX_SUPPLY,
                                            mode_name = e.Nft.Series.SeriesMode != null
                                                ? e.Nft.Series.SeriesMode.MODE_NAME
                                                : null,
                                            name = e.Nft.Series.NAME,
                                            description = e.Nft.Series.DESCRIPTION,
                                            image = e.Nft.Series.IMAGE,
                                            royalties = e.Nft.Series.ROYALTIES.ToString(CultureInfo.InvariantCulture),
                                            type = e.Nft.Series.TYPE,
                                            attr_type_1 = e.Nft.Series.ATTR_TYPE_1,
                                            attr_value_1 = e.Nft.Series.ATTR_VALUE_1,
                                            attr_type_2 = e.Nft.Series.ATTR_TYPE_2,
                                            attr_value_2 = e.Nft.Series.ATTR_VALUE_2,
                                            attr_type_3 = e.Nft.Series.ATTR_TYPE_3,
                                            attr_value_3 = e.Nft.Series.ATTR_VALUE_3
                                        }
                                        : null
                            },
                            ChainId = e.ChainId,
                            TimestampUnixSeconds = e.TIMESTAMP_UNIX_SECONDS,
                            PayloadJson = e.PAYLOAD_JSON,
                            RawData = e.RAW_DATA,
                            NftMetadata = e.Nft != null ? e.Nft.METADATA : null,
                            SeriesMetadata = e.Nft != null && e.Nft.Series != null
                                    ? e.Nft.Series.METADATA
                                    : null,
                            NftCreator = e.Nft != null && e.Nft.CreatorAddress != null
                                    ? e.Nft.CreatorAddress.ADDRESS
                                    : null
                        })
                            .ToArray()
                        : Array.Empty<EventPayloadMapper.EventProjection>()
                }
            });

            EventPayloadMapper.TransactionProjection[] transactionProjections;
            string queryString;

            if (useCursor)
            {
                var cursorFiltered = CursorPagination.ApplyCursor(pageQuery, orderDefinition, sortDirection, cursorToken,
                    x => x.Id);
                var orderedQuery =
                    CursorPagination.ApplyOrdering(cursorFiltered, orderDefinition, sortDirection, x => x.Id);
                var page = await CursorPagination.ReadPageAsync(orderedQuery, orderDefinition, sortDirection, x => x.Id,
                    limit);
                transactionProjections = page.Items.Select(x => x.Projection).ToArray();
                nextCursor = page.NextCursor;
                var pageSize = Math.Max(1, limit);
                queryString = orderedQuery.Take(pageSize + 1).ToQueryString();
            }
            else
            {
                var orderedQuery =
                    CursorPagination.ApplyOrdering(pageQuery, orderDefinition, sortDirection, x => x.Id);
                IQueryable<TransactionPageItem> pageItems;

                if (!string.IsNullOrEmpty(hashUpper))
                    pageItems = orderedQuery.Take(1);
                else if (limit > 0)
                    pageItems = orderedQuery.Skip(offset).Take(limit);
                else
                    pageItems = orderedQuery;

                queryString = pageItems.ToQueryString();
                transactionProjections = (await pageItems.ToArrayAsync()).Select(x => x.Projection).ToArray();
            }

            var allEventProjections = transactionProjections.SelectMany(x => x.EventProjections).ToArray();
            await EventPayloadMapper.ApplyAsync(databaseContext, allEventProjections, with_event_data == 1,
                with_fiat == 1, fiatCurrency, fiatPricesInUsd);

            foreach (var projection in allEventProjections)
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

            const string UnlimitedGasRaw = "18446744073709551615"; // TxMsg.NoMaxGas sentinel

            foreach (var projection in transactionProjections)
            {
                if (projection.ApiTransaction.gas_limit_raw == UnlimitedGasRaw)
                    projection.ApiTransaction.gas_limit = null;

                if (with_events == 1)
                    projection.ApiTransaction.events = projection.EventProjections.Select(p => p.ApiEvent).ToArray();
            }

            transactions = transactionProjections.Select(x => x.ApiTransaction).ToArray();

            if (!string.IsNullOrEmpty(hashUpper) && transactionProjections?.Length == 1)
            {
                var anchor = transactionProjections[0];

                previousHash = await databaseContext.Transactions
                    .AsNoTracking()
                    .Where(x => x.Block.ChainId == anchor.ChainId && x.ID < anchor.TransactionId)
                    .OrderByDescending(x => x.ID)
                    .Select(x => x.HASH)
                    .FirstOrDefaultAsync();

                nextHash = await databaseContext.Transactions
                    .AsNoTracking()
                    .Where(x => x.Block.ChainId == anchor.ChainId && x.ID > anchor.TransactionId)
                    .OrderBy(x => x.ID)
                    .Select(x => x.HASH)
                    .FirstOrDefaultAsync();

                if (transactions.Length > 0)
                {
                    transactions[0].previous_hash = previousHash;
                    transactions[0].next_hash = nextHash;
                }
            }

            var responseTime = DateTime.Now - startTime;

            if (responseTime.TotalSeconds > 1)
            {
                Log.Warning($"Slow query: " + queryString);
            }

            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch (ApiParameterException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var logMessage = LogEx.Exception("Transaction()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new TransactionResult
        {
            total_results = !useCursor && with_total == 1 ? totalResults : null,
            transactions = transactions,
            next_cursor = nextCursor
        };
    }
}
