using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Service.Api;

public static class GetNfts
{
    private sealed class NftPageItem
    {
        public int Id { get; init; }
        public long MintDate { get; init; }
        public Nft ApiNft { get; init; }
        public JsonDocument NftMetadata { get; init; }
        public JsonDocument SeriesMetadata { get; init; }
    }

    [ProducesResponseType(typeof(NftsResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(NftsResult), "Returns NFTs available on Phantasma blockchain.", false, 10, cacheTag: "nfts")]
    public static async Task<NftsResult> Execute(
        // ReSharper disable InconsistentNaming
        string order_by = "mint_date",
        string order_direction = "asc",
        int offset = 0,
        int limit = 50,
        string cursor = "",
        string creator = "",
        string owner = "",
        string contract_hash = "",
        string name = "",
        string q = "",
        string chain = "main",
        string symbol = "",
        string token_id = "",
        string series_id = "",
        string status = "all",
        int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        // Results of the query
        long totalResults = 0;
        Nft[] nftArray;
        string? nextCursor = null;
        var useCursor = false;
        var qTrimmed = string.IsNullOrWhiteSpace(q) ? string.Empty : q.Trim();

        try
        {
            #region ArgValidations

            if ( !ArgValidation.CheckLimit(limit, false) )
                throw new ApiParameterException("Unsupported value for 'limit' parameter.");

            if ( !ArgValidation.CheckOffset(offset) )
                throw new ApiParameterException("Unsupported value for 'offset' parameter.");

            if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            if ( !ArgValidation.CheckOrderDirection(order_direction) )
                throw new ApiParameterException("Unsupported value for 'order_direction' parameter.");

            if ( !string.IsNullOrEmpty(creator) && !ArgValidation.CheckAddress(creator) )
                throw new ApiParameterException("Unsupported value for 'creator' parameter.");

            if ( !string.IsNullOrEmpty(owner) && !ArgValidation.CheckAddress(owner) )
                throw new ApiParameterException("Unsupported value for 'owner' parameter.");

            if ( !string.IsNullOrEmpty(contract_hash) && !ArgValidation.CheckHash(contract_hash, true) )
                throw new ApiParameterException("Unsupported value for 'contract' parameter.");

            if ( !string.IsNullOrEmpty(contract_hash) && string.IsNullOrEmpty(chain) )
                throw new ApiParameterException("Pass chain when using contract filter.");

            if ( !string.IsNullOrEmpty(name) && !ArgValidation.CheckName(name) )
                throw new ApiParameterException("Unsupported value for 'name' parameter.");

            if ( !string.IsNullOrEmpty(qTrimmed) && !ArgValidation.CheckGeneralSearch(qTrimmed) )
                throw new ApiParameterException("Unsupported value for 'q' parameter.");

            if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            if ( !string.IsNullOrEmpty(symbol) && !ArgValidation.CheckSymbol(symbol) )
                throw new ApiParameterException("Unsupported value for 'symbol' parameter.");

            if ( !string.IsNullOrEmpty(token_id) && !ArgValidation.CheckTokenId(token_id) )
                throw new ApiParameterException("Unsupported value for 'token_id' parameter.");

            if ( !string.IsNullOrEmpty(series_id) && !ArgValidation.CheckNumber(series_id) )
                throw new ApiParameterException("Unsupported value for 'series_id' parameter.");

            if ( !string.IsNullOrEmpty(status) && status != "all" && status != "active" && status != "infused" )
                throw new ApiParameterException("Unsupported value for 'status' parameter.");

            #endregion

            var cursorToken = CursorPagination.ParseCursor(cursor);
            var sortDirection = CursorPagination.ParseSortDirection(order_direction);
            var orderBy = string.IsNullOrWhiteSpace(order_by) ? "mint_date" : order_by;

            var orderDefinitions =
                new Dictionary<string, CursorOrderDefinition<NftPageItem>>(StringComparer.OrdinalIgnoreCase)
                {
                    {
                        "mint_date",
                        new CursorOrderDefinition<NftPageItem>(
                            "mint_date",
                            new CursorOrderSegment<NftPageItem, long>(
                                x => x.MintDate,
                                value => long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)))
                    },
                    {
                        "id",
                        new CursorOrderDefinition<NftPageItem>(
                            "id",
                            new CursorOrderSegment<NftPageItem, int>(
                                x => x.Id,
                                value => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)))
                    }
                };

            if ( !orderDefinitions.TryGetValue(orderBy, out var orderDefinition) )
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            useCursor = CursorPagination.ShouldUseCursor(cursorToken, offset, with_total);

            var startTime = DateTime.Now;
            await using MainDbContext databaseContext = new();
            var query = databaseContext.Nfts.AsQueryable().AsNoTracking();

            #region Filtering

            query = query.Where(x =>
                x.NSFW == false && ( x.BURNED == null || x.BURNED == false ) && x.BLACKLISTED == false);

            var qUpper = string.IsNullOrEmpty(qTrimmed) ? string.Empty : qTrimmed.ToUpperInvariant();

            if ( !string.IsNullOrEmpty(qUpper) )
            {
                var isHex = ArgValidation.CheckBase16(qTrimmed);
                var isAddress = PhantasmaPhoenix.Cryptography.Address.IsValidAddress(qTrimmed);
                var isNumber = ArgValidation.CheckNumber(qTrimmed);

                query = query.Where(x =>
                    EF.Functions.ILike(x.NAME, $"%{qTrimmed}%") ||
                    EF.Functions.ILike(x.DESCRIPTION, $"%{qTrimmed}%") ||
                    EF.Functions.ILike(x.TOKEN_ID, $"%{qTrimmed}%") ||
                    EF.Functions.ILike(x.Contract.SYMBOL, $"%{qTrimmed}%") ||
                    ( x.Series != null && EF.Functions.ILike(x.Series.SERIES_ID, $"%{qTrimmed}%") ) ||
                    ( x.Series != null && EF.Functions.ILike(x.Series.NAME, $"%{qTrimmed}%") ) ||
                    ( isHex && x.Contract.HASH.Contains(qUpper) ) ||
                    ( isNumber && x.Series != null && x.Series.SERIES_ID == qTrimmed ) ||
                    ( isAddress && ( x.CreatorAddress.ADDRESS == qTrimmed ||
                                     ( x.NftOwnerships != null &&
                                       x.NftOwnerships.Any(o => o.Address.ADDRESS == qTrimmed) ) ) ));
            }

            if ( !string.IsNullOrEmpty(status) )
                query = status switch
                {
                    "active" => query.Where(x => x.InfusedInto == null),
                    "infused" => query.Where(x => x.InfusedInto != null),
                    _ => query
                };

            if ( !string.IsNullOrEmpty(creator) ) query = query.Where(x => x.CreatorAddress.ADDRESS == creator);

            if ( !string.IsNullOrEmpty(contract_hash) ) query = query.Where(x => x.Contract.HASH == contract_hash);

            if ( !string.IsNullOrEmpty(chain) ) query = query.Where(x => x.Chain.NAME == chain);

            if ( !string.IsNullOrEmpty(symbol) ) query = query.Where(x => x.Contract.SYMBOL == symbol);

            if ( !string.IsNullOrEmpty(token_id) ) query = query.Where(x => x.TOKEN_ID == token_id);

            if ( !string.IsNullOrEmpty(series_id) ) query = query.Where(x => x.Series.SERIES_ID == series_id);

            if ( !string.IsNullOrEmpty(name) )
                query = query.Where(x => x.NAME.Contains(name) || x.DESCRIPTION.Contains(name));

            if ( !string.IsNullOrEmpty(owner) )
            {
                var ids = NftMethods.GetIdsByOwnerAddress(databaseContext, owner, chain);

                query = query.Where(x => ids.Contains(x.ID));
            }

            #endregion

            if ( !useCursor && with_total == 1 )
                totalResults = await query.CountAsync();

            #region ResultArray

            var pageQuery = query.Select(x => new NftPageItem
            {
                Id = x.ID,
                MintDate = x.MINT_DATE_UNIX_SECONDS,
                NftMetadata = x.METADATA,
                SeriesMetadata = x.Series != null ? x.Series.METADATA : null,
                ApiNft = new Nft
                {
                    token_id = x.TOKEN_ID,
                    chain = x.Chain.NAME,
                    symbol = x.Contract.SYMBOL,
                    creator_address = x.CreatorAddress != null ? x.CreatorAddress.ADDRESS : null,
                    creator_onchain_name = x.CreatorAddress != null ? x.CreatorAddress.ADDRESS_NAME : null,
                    owners = x.NftOwnerships != null
                        ? x.NftOwnerships.Select(n => new NftOwnershipResult
                        {
                            address = n.Address.ADDRESS,
                            onchain_name = n.Address.ADDRESS_NAME,
                            offchain_name = n.Address.USER_NAME,
                            amount = n.AMOUNT
                        }).ToArray()
                        : null,
                    contract = new Contract
                    {
                        name = x.Contract.NAME,
                        hash = x.Contract.HASH,
                        symbol = x.Contract.SYMBOL
                    },
                    nft_metadata = new NftMetadata
                    {
                        name = x.NAME,
                        description = x.DESCRIPTION,
                        image = x.IMAGE,
                        video = x.VIDEO,
                        info_url = x.INFO_URL,
                        rom = x.ROM,
                        ram = x.RAM,
                        mint_date = x.NAME != null ? x.MINT_DATE_UNIX_SECONDS.ToString() : null,
                        mint_number = x.NAME != null ? x.MINT_NUMBER.ToString() : null
                    },
                    series = x.Series != null
                        ? new Series
                        {
                            id = x.Series.ID,
                            series_id = x.Series.SERIES_ID,
                            creator = x.Series.CreatorAddress.ADDRESS,
                            current_supply = x.Series.CURRENT_SUPPLY,
                            max_supply = x.Series.MAX_SUPPLY,
                            mode_name = x.Series.SeriesMode.MODE_NAME,
                            name = x.Series.NAME,
                            description = x.Series.DESCRIPTION,
                            image = x.Series.IMAGE,
                            royalties = x.Series.ROYALTIES.ToString(CultureInfo.InvariantCulture),
                            type = x.Series.TYPE,
                            attr_type_1 = x.Series.ATTR_TYPE_1,
                            attr_value_1 = x.Series.ATTR_VALUE_1,
                            attr_type_2 = x.Series.ATTR_TYPE_2,
                            attr_value_2 = x.Series.ATTR_VALUE_2,
                            attr_type_3 = x.Series.ATTR_TYPE_3,
                            attr_value_3 = x.Series.ATTR_VALUE_3
                        }
                        : null,
                    infusion = x.Infusions != null
                        ? x.Infusions.Select(i => new Infusion
                        {
                            key = i.KEY,
                            value = i.VALUE
                        }).ToArray()
                        : null,
                    infused_into = x.InfusedInto != null
                        ? new InfusedInto
                        {
                            token_id = x.InfusedInto.TOKEN_ID,
                            chain = x.InfusedInto.Chain.NAME,
                            contract = new Contract
                            {
                                name = x.InfusedInto.Contract.NAME,
                                hash = x.InfusedInto.Contract.HASH,
                                symbol = x.InfusedInto.Contract.SYMBOL
                            }
                        }
                        : null
                }
            });

            if ( useCursor )
            {
                var cursorFiltered = CursorPagination.ApplyCursor(pageQuery, orderDefinition, sortDirection, cursorToken,
                    x => x.Id);
                var orderedQuery = CursorPagination.ApplyOrdering(cursorFiltered, orderDefinition, sortDirection,
                    x => x.Id);
                var page = await CursorPagination.ReadPageAsync(orderedQuery, orderDefinition, sortDirection, x => x.Id,
                    limit);
                foreach ( var item in page.Items )
                {
                    if ( item.ApiNft?.nft_metadata != null )
                        item.ApiNft.nft_metadata.metadata =
                            MetadataMapper.FromNft(item.NftMetadata, item.ApiNft);

                    if ( item.ApiNft?.series != null )
                        item.ApiNft.series.metadata =
                            MetadataMapper.FromSeries(item.SeriesMetadata, item.ApiNft.series);
                }

                nftArray = page.Items.Select(x => x.ApiNft).ToArray();
                nextCursor = page.NextCursor;
            }
            else
            {
                var orderedQuery = CursorPagination.ApplyOrdering(pageQuery, orderDefinition, sortDirection, x => x.Id);
                var pageItems = limit > 0 ? orderedQuery.Skip(offset).Take(limit) : orderedQuery;
                var materializedPage = await pageItems.ToArrayAsync();

                foreach ( var item in materializedPage )
                {
                    if ( item.ApiNft?.nft_metadata != null )
                        item.ApiNft.nft_metadata.metadata =
                            MetadataMapper.FromNft(item.NftMetadata, item.ApiNft);

                    if ( item.ApiNft?.series != null )
                        item.ApiNft.series.metadata =
                            MetadataMapper.FromSeries(item.SeriesMetadata, item.ApiNft.series);
                }

                nftArray = materializedPage.Select(x => x.ApiNft).ToArray();
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
            var logMessage = LogEx.Exception("Nfts()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new NftsResult
        {
            total_results = !useCursor && with_total == 1 ? totalResults : null,
            nfts = nftArray,
            next_cursor = nextCursor
        };
    }
}
