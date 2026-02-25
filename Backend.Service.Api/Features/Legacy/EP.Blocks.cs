#nullable enable
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

public static class GetBlocks
{
    private sealed class BlockProjection
    {
        public required Block ApiBlock { get; init; }
        public EventPayloadMapper.TransactionProjection[] TransactionProjections { get; init; } =
            Array.Empty<EventPayloadMapper.TransactionProjection>();
    }

    private sealed class BlockPageItem
    {
        public int Id { get; init; }
        public string Hash { get; init; } = string.Empty;
        public required BlockProjection Projection { get; init; }
    }

    [ProducesResponseType(typeof(BlockResult), (int)HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(BlockResult), "Returns the block information from backend.", false, 10, cacheTag: "block")]
    public static async Task<BlockResult> Execute(
        // ReSharper disable InconsistentNaming
        string order_by = "id",
        string order_direction = "asc",
        int limit = 50,
        string cursor = "",
        string id = "",
        string hash = "",
        string hash_partial = "",
        string height = "",
        string q = "",
        string chain = "main",
        string date_less = "",
        string date_greater = "",
        int with_transactions = 0,
        int with_events = 0,
        int with_event_data = 0,
        int with_nft = 0,
        int with_fiat = 0
    // ReSharper enable InconsistentNaming
    )
    {
        Block[] blockArray;
        const string fiatCurrency = "USD";
        string? nextCursor = null;
        var qTrimmed = string.IsNullOrWhiteSpace(q) ? string.Empty : q.Trim();

        try
        {
            #region ArgValidation

            if (!string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by))
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            if (!ArgValidation.CheckOrderDirection(order_direction))
                throw new ApiParameterException("Unsupported value for 'order_direction' parameter.");

            if (!ArgValidation.CheckLimit(limit))
                throw new ApiParameterException("Unsupported value for 'limit' parameter.");

            if (!string.IsNullOrEmpty(hash) && !ArgValidation.CheckHash(hash))
                throw new ApiParameterException("Unsupported value for 'hash' parameter.");

            if (!string.IsNullOrEmpty(hash_partial) && !ArgValidation.CheckHash(hash_partial))
                throw new ApiParameterException("Unsupported value for 'hash_partial' parameter.");

            if (!string.IsNullOrEmpty(height) && !ArgValidation.CheckNumber(height))
                throw new ApiParameterException("Unsupported value for 'height' parameter.");

            if (!string.IsNullOrEmpty(qTrimmed) && !ArgValidation.CheckGeneralSearch(qTrimmed))
                throw new ApiParameterException("Unsupported value for 'q' parameter.");

            if (!string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain))
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            if (!string.IsNullOrEmpty(date_less) && !ArgValidation.CheckNumber(date_less))
                throw new ApiParameterException("Unsupported value for 'date_less' parameter.");

            if (!string.IsNullOrEmpty(date_greater) && !ArgValidation.CheckNumber(date_greater))
                throw new ApiParameterException("Unsupported value for 'date_greater' parameter.");

            #endregion

            var cursorToken = CursorPagination.ParseCursor(cursor);
            var sortDirection = CursorPagination.ParseSortDirection(order_direction);
            var orderBy = string.IsNullOrWhiteSpace(order_by) ? "id" : order_by;

            var orderDefinitions = new Dictionary<string, CursorOrderDefinition<BlockPageItem>>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "id",
                    new CursorOrderDefinition<BlockPageItem>(
                        "id",
                        new CursorOrderSegment<BlockPageItem, int>(
                            x => x.Id,
                            value => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)))
                },
                {
                    "hash",
                    new CursorOrderDefinition<BlockPageItem>(
                        "hash",
                        new CursorOrderSegment<BlockPageItem, string>(
                            x => x.Hash,
                            value => value))
                }
            };

            if (!orderDefinitions.TryGetValue(orderBy, out var orderDefinition))
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            var startTime = DateTime.Now;

            await using MainDbContext databaseContext = new();
            var fiatPricesInUsd = FiatExchangeRateMethods.GetPrices(databaseContext);

            //just need that since we build the model so it knows what we can use
            var query = databaseContext.Blocks.AsQueryable().AsNoTracking();

            #region Filtering
            var qUpper = string.IsNullOrEmpty(qTrimmed) ? string.Empty : qTrimmed.ToUpperInvariant();

            if (!string.IsNullOrEmpty(qUpper))
            {
                var isNumber = ArgValidation.CheckNumber(qTrimmed);
                var isHex = ArgValidation.CheckBase16(qTrimmed);
                var isFullHash = isHex && qUpper.Length >= 64;

                if (isFullHash)
                {
                    query = query.Where(x => x.HASH == qUpper);
                }
                else if (isNumber)
                {
                    query = query.Where(x => x.HEIGHT == qTrimmed || x.HASH.Contains(qUpper));
                }
                else
                {
                    query = query.Where(x => x.HASH.Contains(qUpper));
                }
            }

            if (!string.IsNullOrEmpty(id))
            {
                query = id.Length == 64 ? query.Where(x => x.HASH == id || x.HEIGHT == id) : query.Where(x => x.HEIGHT == id);
            }

            if (!string.IsNullOrEmpty(hash))
                query = query.Where(x => x.HASH == hash);

            if (!string.IsNullOrEmpty(hash_partial))
                query = query.Where(x => x.HASH.Contains(hash_partial));

            if (!string.IsNullOrEmpty(height))
                query = query.Where(x => x.HEIGHT == height);

            if (!string.IsNullOrEmpty(date_less))
                query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS <= UnixSeconds.FromString(date_less));

            if (!string.IsNullOrEmpty(date_greater))
                query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS >= UnixSeconds.FromString(date_greater));

            if (!string.IsNullOrEmpty(chain)) query = query.Where(x => x.Chain.NAME == chain);

            #endregion

            #region ResultArray

            var blockQuery = query.Select(x => new BlockPageItem
            {
                Id = x.ID,
                Hash = x.HASH,
                Projection = new BlockProjection
                {
                    ApiBlock = new Block
                    {
                        height = x.HEIGHT,
                        hash = x.HASH,
                        previous_hash = x.PREVIOUS_HASH,
                        protocol = x.PROTOCOL,
                        chain_address = x.ChainAddress.ADDRESS,
                        validator_address = x.ValidatorAddress.ADDRESS,
                        date = x.TIMESTAMP_UNIX_SECONDS.ToString(),
                        reward = x.REWARD
                    },
                    TransactionProjections = with_transactions == 1 && x.Transactions != null
                        ? x.Transactions.Select(t => new EventPayloadMapper.TransactionProjection
                        {
                            ApiTransaction = new Transaction
                            {
                                hash = t.HASH,
                                block_hash = x.HASH,
                                block_height = x.HEIGHT,
                                chain = x.Chain.NAME.ToLower(),
                                index = t.INDEX,
                                date = t.TIMESTAMP_UNIX_SECONDS.ToString(),
                                fee = t.FEE,
                                fee_raw = t.FEE_RAW,
                                script_raw = t.SCRIPT_RAW,
                                result = t.RESULT,
                                payload = t.PAYLOAD,
                                expiration = t.EXPIRATION.ToString(),
                                gas_price = t.GAS_PRICE,
                                gas_price_raw = t.GAS_PRICE_RAW,
                                gas_limit = t.GAS_LIMIT,
                                gas_limit_raw = t.GAS_LIMIT_RAW,
                                state = t.State.NAME,
                                sender = t.Sender != null
                                        ? new Address
                                        {
                                            address_name = t.Sender.ADDRESS_NAME,
                                            address = t.Sender.ADDRESS
                                        }
                                        : null,
                                gas_payer = t.GasPayer != null
                                        ? new Address
                                        {
                                            address_name = t.GasPayer.ADDRESS_NAME,
                                            address = t.GasPayer.ADDRESS
                                        }
                                        : null,
                                gas_target = t.GasTarget != null
                                        ? new Address
                                        {
                                            address_name = t.GasTarget.ADDRESS_NAME,
                                            address = t.GasTarget.ADDRESS
                                        }
                                        : null
                            },
                            EventProjections = with_events == 1 && t.Events != null
                                    ? t.Events.Select(e => new EventPayloadMapper.EventProjection
                                    {
                                        ApiEvent = new Event
                                        {
                                            event_id = e.ID,
                                            chain = e.Chain.NAME.ToLower(),
                                            date = e.TIMESTAMP_UNIX_SECONDS.ToString(),
                                            transaction_hash = t.HASH,
                                            token_id = e.TOKEN_ID,
                                            payload_json = e.PAYLOAD_JSON,
                                            raw_data = e.RAW_DATA,
                                            event_kind = e.EventKind.NAME,
                                            address = e.Address.ADDRESS,
                                            address_name = e.Address.ADDRESS_NAME,
                                            contract = new Contract
                                            {
                                                name = e.Contract.NAME,
                                                hash = e.Contract.HASH,
                                                symbol = e.Contract.SYMBOL
                                            },
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
                                                        mode_name = (e.Nft.Series.SeriesMode != null
                                                            ? e.Nft.Series.SeriesMode.MODE_NAME
                                                            : null)!,
                                                        name = e.Nft.Series.NAME,
                                                        description = e.Nft.Series.DESCRIPTION,
                                                        image = e.Nft.Series.IMAGE,
                                                        royalties = e.Nft.Series.ROYALTIES
                                                            .ToString(CultureInfo.InvariantCulture),
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
                        })
                            .ToArray()
                        : Array.Empty<EventPayloadMapper.TransactionProjection>()
                }
            });

            BlockProjection[] blockProjections;

            var cursorFiltered = CursorPagination.ApplyCursor(blockQuery, orderDefinition, sortDirection,
                cursorToken, x => x.Id);
            var orderedQuery = CursorPagination.ApplyOrdering(cursorFiltered, orderDefinition, sortDirection,
                x => x.Id);
            var page = await CursorPagination.ReadPageAsync(orderedQuery, orderDefinition, sortDirection,
                x => x.Id, limit);
            blockProjections = page.Items.Select(x => x.Projection).ToArray();
            nextCursor = page.NextCursor;

            var allEventProjections = blockProjections.SelectMany(b => b.TransactionProjections)
                .SelectMany(t => t.EventProjections).ToArray();

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

            foreach (var blockProjection in blockProjections)
            {
                foreach (var transactionProjection in blockProjection.TransactionProjections)
                {
                    if (with_events == 1)
                        transactionProjection.ApiTransaction.events =
                            transactionProjection.EventProjections.Select(p => p.ApiEvent).ToArray();
                }

                if (with_transactions == 1)
                    blockProjection.ApiBlock.transactions =
                        blockProjection.TransactionProjections.Select(t => t.ApiTransaction).ToArray();
            }

            blockArray = blockProjections.Select(b => b.ApiBlock).ToArray();

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
            var logMessage = LogEx.Exception("Block()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }


        return new BlockResult
        {
            total_results = null,
            blocks = blockArray,
            next_cursor = nextCursor
        };
    }
}
