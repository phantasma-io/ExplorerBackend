using System;
using System.Globalization;
using System.Linq;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Serilog;
using Contract = GhostDevs.Service.ApiResults.Contract;
using Infusion = GhostDevs.Service.ApiResults.Infusion;
using Nft = GhostDevs.Service.ApiResults.Nft;
using Series = GhostDevs.Service.ApiResults.Series;

namespace GhostDevs.Service;

public partial class Endpoints
{
    [APIInfo(typeof(NftsResult), "Returns NFTs available on Phantasma blockchain.", false, 10, cacheTag: "nfts")]
    public NftsResult Nfts([APIParameter("Order by [mint_date, id]", "string")] string order_by = "mint_date",
        [APIParameter("Order direction [asc, desc]", "string")]
        string order_direction = "asc",
        [APIParameter("Offset", "integer")] int offset = 0,
        [APIParameter("Limit", "integer")] int limit = 50,
        [APIParameter("Return total (slower) or not (faster)", "integer")]
        int with_total = 0,
        [APIParameter("Address of asset creator", "string")]
        string creator = "",
        [APIParameter("Address of asset owner", "string")]
        string owner = "",
        [APIParameter("Token contract hash", "string")]
        string contract_hash = "",
        [APIParameter("Asset name/description filter (partial match)", "string")]
        string name = "",
        [APIParameter("Chain name (ex. 'main')", "string")]
        string chain = "",
        [APIParameter("Symbol (ex. 'TTRS')", "string")]
        string symbol = "",
        [APIParameter("Token ID", "string")] string token_id = "",
        [APIParameter("Series ID", "string")] string series_id = "",
        [APIParameter("Infusion status (all/active/infused)", "string")]
        string status = "all")
    {
        // Results of the query
        long totalResults = 0;
        Nft[] nftArray;

        try
        {
            #region ArgValidations

            if ( !ArgValidation.CheckLimit(limit) )
                throw new APIException("Unsupported value for 'limit' parameter.");

            if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                throw new APIException("Unsupported value for 'order_by' parameter.");

            if ( !ArgValidation.CheckOrderDirection(order_direction) )
                throw new APIException("Unsupported value for 'order_direction' parameter.");

            if ( !string.IsNullOrEmpty(creator) && !ArgValidation.CheckAddress(creator) )
                throw new APIException("Unsupported value for 'creator' parameter.");

            ContractMethods.Drop0x(ref creator);

            if ( !string.IsNullOrEmpty(owner) && !ArgValidation.CheckAddress(owner) )
                throw new APIException("Unsupported value for 'owner' parameter.");

            ContractMethods.Drop0x(ref owner);

            if ( !string.IsNullOrEmpty(contract_hash) && !ArgValidation.CheckHash(contract_hash, true) )
                throw new APIException("Unsupported value for 'contract' parameter.");

            ContractMethods.Drop0x(ref contract_hash);
            if ( !string.IsNullOrEmpty(contract_hash) && string.IsNullOrEmpty(chain) )
                throw new APIException("Pass chain when using contract filter.");

            if ( !string.IsNullOrEmpty(name) && !ArgValidation.CheckName(name) )
                throw new APIException("Unsupported value for 'name' parameter.");

            if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                throw new APIException("Unsupported value for 'chain' parameter.");

            if ( !string.IsNullOrEmpty(symbol) && !ArgValidation.CheckSymbol(symbol) )
                throw new APIException("Unsupported value for 'symbol' parameter.");

            if ( !string.IsNullOrEmpty(token_id) && !ArgValidation.CheckTokenId(token_id) )
                throw new APIException("Unsupported value for 'token_id' parameter.");

            if ( !string.IsNullOrEmpty(series_id) && !ArgValidation.CheckSeriesId(series_id) )
                throw new APIException("Unsupported value for 'series_id' parameter.");

            if ( !string.IsNullOrEmpty(status) && status != "all" && status != "active" && status != "infused" )
                throw new APIException("Unsupported value for 'status' parameter.");

            if ( !string.IsNullOrEmpty(owner) && string.IsNullOrEmpty(chain) )
                throw new APIException("Pass chain when using owner filter.");

            #endregion

            var startTime = DateTime.Now;

            var query = _context.Nfts.AsQueryable();

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

            if ( !string.IsNullOrEmpty(creator) )
            {
                var ids = AddressMethods.GetIdsFromExtendedFormat(_context, creator, true, chain);

                query = query.Where(x => ids.Contains(x.CreatorAddressId ?? 0));
            }

            if ( !string.IsNullOrEmpty(contract_hash) ) query = query.Where(x => x.Contract.HASH == contract_hash);

            if ( !string.IsNullOrEmpty(chain) ) query = query.Where(x => x.Chain.NAME == chain);

            if ( !string.IsNullOrEmpty(symbol) ) query = query.Where(x => x.Contract.SYMBOL == symbol);

            if ( !string.IsNullOrEmpty(token_id) ) query = query.Where(x => x.TOKEN_ID == token_id);

            if ( !string.IsNullOrEmpty(series_id) ) query = query.Where(x => x.Series.SERIES_ID == series_id);

            if ( !string.IsNullOrEmpty(name) )
                query = query.Where(x => x.NAME.Contains(name) || x.DESCRIPTION.Contains(name));

            if ( !string.IsNullOrEmpty(owner) )
            {
                var ids = NftMethods.GetIdsByOwnerAddress(_context, owner, chain);

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
                creator_address = x.CreatorAddress.ADDRESS,
                creator_onchain_name = x.CreatorAddress.ADDRESS_NAME,
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
                series = new Series
                {
                    id = x.Series.SERIES_ID.ToString(),
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
                },
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
        catch ( APIException )
        {
            throw;
        }
        catch ( Exception e )
        {
            var logMessage = LogEx.Exception("Nfts()", e);

            throw new APIException(logMessage, e);
        }

        return new NftsResult {total_results = with_total == 1 ? totalResults : null, nfts = nftArray};
    }
}
