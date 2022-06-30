using System;
using System.Globalization;
using System.Linq;
using System.Net;
using Database.Main;
using GhostDevs.Commons;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace GhostDevs.Service.Api;

public partial class Endpoints
{
    //TODO change order_by and order_direction maybe to enum
    /// <summary>
    ///     Returns NFTs available on Phantasma blockchain.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-NftsResult'>SeriesResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are id or mint_date</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="creator">Address of asset creator</param>
    /// <param name="owner">Address of asset owner</param>
    /// <param name="contract_hash" example="SOUL">Token contract hash</param>
    /// <param name="name">Asset name/description filter (partial match)</param>
    /// <param name="chain" example="main">Chain name</param>
    /// <param name="symbol" example="TTRS"></param>
    /// <param name="token_id">Token ID</param>
    /// <param name="series_id">Series ID</param>
    /// <param name="status" example="all">Infusion status (all/active/infused)</param>
    /// <param name="with_total" example="0">Returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [ProducesResponseType(typeof(NftsResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(NftsResult), "Returns NFTs available on Phantasma blockchain.", false, 10, cacheTag: "nfts")]
    public NftsResult Nfts(
        // ReSharper disable InconsistentNaming
        string order_by = "mint_date",
        string order_direction = "asc",
        int offset = 0,
        int limit = 50,
        string creator = "",
        string owner = "",
        string contract_hash = "",
        string name = "",
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

            ContractMethods.Drop0x(ref creator);

            if ( !string.IsNullOrEmpty(owner) && !ArgValidation.CheckAddress(owner) )
                throw new ApiParameterException("Unsupported value for 'owner' parameter.");

            ContractMethods.Drop0x(ref owner);

            if ( !string.IsNullOrEmpty(contract_hash) && !ArgValidation.CheckHash(contract_hash, true) )
                throw new ApiParameterException("Unsupported value for 'contract' parameter.");

            ContractMethods.Drop0x(ref contract_hash);
            if ( !string.IsNullOrEmpty(contract_hash) && string.IsNullOrEmpty(chain) )
                throw new ApiParameterException("Pass chain when using contract filter.");

            if ( !string.IsNullOrEmpty(name) && !ArgValidation.CheckName(name) )
                throw new ApiParameterException("Unsupported value for 'name' parameter.");

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

            var startTime = DateTime.Now;
            using MainDbContext databaseContext = new();
            var query = databaseContext.Nfts.AsQueryable().AsNoTracking();

            #region Filtering

            query = query.Where(x =>
                x.NSFW == false && ( x.BURNED == null || x.BURNED == false ) && x.BLACKLISTED == false);

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

            if ( order_direction == "asc" )
                query = order_by switch
                {
                    "mint_date" => query.OrderBy(x => x.MINT_DATE_UNIX_SECONDS),
                    "id" => query.OrderBy(x => x.ID),
                    _ => query
                };
            else
                query = order_by switch
                {
                    "mint_date" => query.OrderByDescending(x => x.MINT_DATE_UNIX_SECONDS),
                    "id" => query.OrderByDescending(x => x.ID),
                    _ => query
                };

            if ( with_total == 1 )
                totalResults = query.Count();

            #region ResultArray

            nftArray = query.Skip(offset).Take(limit).Select(x => new Nft
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
                    hash = ContractMethods.Prepend0x(x.Contract.HASH, x.Chain.NAME),
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
                            hash = ContractMethods.Prepend0x(x.InfusedInto.Contract.HASH, x.InfusedInto.Chain.NAME),
                            symbol = x.InfusedInto.Contract.SYMBOL
                        }
                    }
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
            var logMessage = LogEx.Exception("Nfts()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new NftsResult {total_results = with_total == 1 ? totalResults : null, nfts = nftArray};
    }
}
