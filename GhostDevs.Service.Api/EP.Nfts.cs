using System;
using System.Linq;
using GhostDevs.Service.ApiResults;
using Database.Main;
using GhostDevs.Commons;
using Serilog;

namespace GhostDevs.Service
{
    public partial class Endpoints
    {
        [APIInfo(typeof(NftsResult), "Returns NFTs available on Phantasma blockchain.", false, 10, cacheTag: "nfts")]
        [APIFailCase("address is invalid", "ABCD123")]
        public NftsResult Nfts([APIParameter("Order by [mint_date]", "string")] string order_by = "mint_date",
            [APIParameter("Order direction [asc, desc]", "string")] string order_direction = "asc",
            [APIParameter("Offset", "integer")] int offset = 0,
            [APIParameter("Limit", "integer")] int limit = 50,
            [APIParameter("Return total (slower) or not (faster)", "integer")] int with_total = 0,
            [APIParameter("Fiat currency to calculate Fiat price", "string")] string fiat_currency = "USD",
            [APIParameter("Address of asset creator (multiple values supported, comma-separated)", "string")] string creator = "",
            [APIParameter("Address of asset owner (multiple values supported, comma-separated)", "string")] string owner = "",
            [APIParameter("Token contract hash", "string")] string contract = "",
            [APIParameter("Asset name/description filter (partial match)", "string")] string name = "",
            [APIParameter("Chain name (ex. 'main')", "string")] string chain = "",
            [APIParameter("Symbol (ex. 'TTRS') (multiple values supported, comma-separated)", "string")] string symbol = "",
            [APIParameter("Token ID (multiple values supported, comma-separated)", "string")] string token_id = "",
            [APIParameter("Series ID", "string")] string series_id = "",
            [APIParameter("Infusion status (all/active/infused)", "string")] string status = "")
        {
            // Results of the query
            long totalResults = 0;
            ApiResults.Nft[] nftsArray = null;

            using (var databaseContext = new MainDbContext())
            {
                try
                {
                    #region ARGUMENTS CHECK
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
                    if (!String.IsNullOrEmpty(fiat_currency) && !ArgValidation.CheckSymbol(fiat_currency))
                    {
                        throw new APIException("Unsupported value for 'fiat_currency' parameter.");
                    }
                    if (!String.IsNullOrEmpty(creator) && !ArgValidation.CheckAddress(creator))
                    {
                        throw new APIException("Unsupported value for 'creator' parameter.");
                    }
                    ContractMethods.Drop0x(ref creator);

                    if (!String.IsNullOrEmpty(owner) && !ArgValidation.CheckAddress(owner))
                    {
                        throw new APIException("Unsupported value for 'owner' parameter.");
                    }
                    ContractMethods.Drop0x(ref owner);

                    if (!String.IsNullOrEmpty(contract) && !ArgValidation.CheckAddress(contract))
                    {
                        throw new APIException("Unsupported value for 'contract' parameter.");
                    }
                    ContractMethods.Drop0x(ref contract);
                    if (!String.IsNullOrEmpty(contract) && String.IsNullOrEmpty(chain))
                    {
                        throw new APIException("Pass chain when using contract filter.");
                    }

                    if (!String.IsNullOrEmpty(name) && !ArgValidation.CheckName(name))
                    {
                        throw new APIException("Unsupported value for 'name' parameter.");
                    }

                    if (!String.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain))
                    {
                        throw new APIException("Unsupported value for 'chain' parameter.");
                    }

                    if (!String.IsNullOrEmpty(symbol) && !ArgValidation.CheckSymbol(symbol, true))
                    {
                        throw new APIException("Unsupported value for 'symbol' parameter.");
                    }
                    if (!String.IsNullOrEmpty(token_id) && !ArgValidation.CheckTokenId(token_id))
                    {
                        throw new APIException("Unsupported value for 'token_id' parameter.");
                    }
                    if (!String.IsNullOrEmpty(series_id) && !ArgValidation.CheckSeriesId(series_id))
                    {
                        throw new APIException("Unsupported value for 'series_id' parameter.");
                    }
                    if (!String.IsNullOrEmpty(status) && status != "all" && status != "active" && status != "infused")
                    {
                        throw new APIException("Unsupported value for 'status' parameter.");
                    }
                    #endregion

                    DateTime startTime = DateTime.Now;

                    // Getting exchange rates in advance.
                    var fiatPricesInUSD = FiatExchangeRateMethods.GetPrices(databaseContext);

                    // Getting list of token prices in advance.
                    var tokenPrices = TokenMethods.GetPrices(databaseContext, fiat_currency);

                    var pgConnection = new PostgreSQLConnector(databaseContext.GetConnectionString());

                    var query = new QueryBuilder();

                    query.AddWhere($@"COALESCE(""Nfts"".""BURNED"", FALSE) = FALSE");

                    query.AddWhere($@"""Nfts"".""BLACKLISTED"" = FALSE");

                    if (!String.IsNullOrEmpty(status) && status != "all")
                    {
                        switch (status)
                        {
                            case "active":
                                query.AddWhere($@"""Nfts"".""InfusedIntoId"" is null");
                                break;
                            case "infused":
                                query.AddWhere($@"""Nfts"".""InfusedIntoId"" is not null");
                                break;
                        }
                    }

                    query.AddWhere($@"""Nfts"".""NSFW"" = FALSE", true);

                    if (!String.IsNullOrEmpty(creator))
                    {
                        var ids = AddressMethods.GetIdsFromExtendedFormat(databaseContext, creator, true, chain);

                        var clause = "";
                        for (var i = 0; i < ids.Length; i++)
                        {
                            if (i > 0)
                                clause += " or ";

                            clause += $@"""Nfts"".""CreatorAddressId"" = {ids[i]}";
                        }
                        query.AddWhere(clause, true);
                    }

                    if (!String.IsNullOrEmpty(contract))
                    {
                        query.AddWhere($@"""Contracts"".""HASH"" ilike @contract");
                        query.AddParam("contract", contract);
                    }

                    if (!String.IsNullOrEmpty(name))
                    {
                        query.AddWhere($@"""Nfts"".""NAME"" ilike @name or ""Nfts"".""DESCRIPTION"" ilike @name", true);
                        query.AddParam("name", $"%{name}%");
                    }

                    if (!String.IsNullOrEmpty(chain))
                    {
                        query.AddWhere($@"""NftChains"".""NAME"" ilike @chain");
                        query.AddParam("chain", chain);
                    }

                    if (!String.IsNullOrEmpty(symbol))
                    {
                        // Searching for NFTs listed against symbol.

                        if (symbol.Contains(","))
                        {
                            // It's a list of quote symbols.
                            string[] symbols = symbol.ToUpper().Split(',');

                            string clause = "";

                            for (var i = 0; i < symbols.Length; i++)
                            {
                                if (i > 0)
                                    clause += " or ";

                                clause += $@"""Contracts"".""SYMBOL"" ilike @base_symbol{i}";
                                query.AddParam($"base_symbol{i}", symbols[i]);
                            }

                            query.AddWhere(clause, true);
                        }
                        else
                        {
                            // Single quote symbol.
                            query.AddWhere($@"""Contracts"".""SYMBOL"" ilike @base_symbol");
                            query.AddParam("base_symbol", symbol);
                        }
                    }

                    if (!String.IsNullOrEmpty(token_id))
                    {
                        if (token_id.Contains(","))
                        {
                            string[] token_ids = token_id.ToUpper().Split(',');

                            string clause = "";

                            for (var i = 0; i < token_ids.Length; i++)
                            {
                                if (i > 0)
                                    clause += " or ";

                                clause += $@"""Nfts"".""TOKEN_ID"" = @token_id{i}";
                                query.AddParam($"token_id{i}", token_ids[i]);
                            }

                            query.AddWhere(clause, true);
                        }
                        else
                        {
                            query.AddWhere($@"""Nfts"".""TOKEN_ID"" = @token_id");
                            query.AddParam("token_id", token_id);
                        }
                    }

                    if (!String.IsNullOrEmpty(series_id))
                    {
                        query.AddWhere($@"""Serieses"".""SERIES_ID"" = @series_id");
                        query.AddParam("series_id", series_id);
                    }

                    query.AddFrom("Nfts");

                    query.AddJoin(QueryBuilder.JoinType.INNER,
                        "Contracts", "", "ID",
                        "Nfts", "ContractId");
                    query.AddJoin(QueryBuilder.JoinType.INNER,
                        "Chains", "NftChains", "ID",
                        "Nfts", "ChainId");
                    query.AddJoin(QueryBuilder.JoinType.LEFT,
                        "Serieses", "", "ID",
                        "Nfts", "SeriesId");
                    query.AddJoin(QueryBuilder.JoinType.LEFT,
                        "SeriesModes", "", "ID",
                        "Serieses", "SeriesModeId");
                    query.AddJoin(QueryBuilder.JoinType.LEFT,
                        "Contracts", "SeriesContracts", "ID",
                        "Serieses", "ContractId");
                    query.AddJoin(QueryBuilder.JoinType.LEFT,
                        "Chains", "SeriesChains", "ID",
                        "SeriesContracts", "ChainId");

                    query.AddColumn("Nfts", "ID");
                    query.AddColumn("Nfts", "CreatorAddressId");
                    query.AddColumn("Contracts", "SYMBOL", "BASE_SYMBOL");
                    query.AddColumn("Nfts", "TOKEN_ID");
                    query.AddColumn("NftChains", "NAME", "NFT_CHAIN_NAME");
                    query.AddColumn("Contracts", "HASH", "CONTRACT_HASH");
                    query.AddColumn("Nfts", "InfusedIntoId");
                    query.AddColumn("Nfts", "SeriesId");
                    query.AddColumn("Serieses", "SERIES_ID");
                    query.AddColumn("Serieses", "NAME", "SERIES_NAME");
                    query.AddColumn("Serieses", "DESCRIPTION", "SERIES_DESCRIPTION");
                    query.AddColumn("Serieses", "IMAGE", "SERIES_IMAGE");
                    query.AddColumn("Serieses", "ROYALTIES");
                    query.AddColumn("Serieses", "TYPE", "SERIES_TYPE");
                    query.AddColumn("Serieses", "ATTR_TYPE_1");
                    query.AddColumn("Serieses", "ATTR_VALUE_1");
                    query.AddColumn("Serieses", "ATTR_TYPE_2");
                    query.AddColumn("Serieses", "ATTR_VALUE_2");
                    query.AddColumn("Serieses", "ATTR_TYPE_3");
                    query.AddColumn("Serieses", "ATTR_VALUE_3");
                    query.AddColumn("Serieses", "STATS_SOLD");
                    query.AddColumn("SeriesModes", "MODE_NAME");
                    query.AddColumn("Serieses", "CURRENT_SUPPLY", "SERIES_CURRENT_SUPPLY");
                    query.AddColumn("Serieses", "MAX_SUPPLY", "SERIES_MAX_SUPPLY");

                    query.AddColumn("Nfts", "DESCRIPTION");
                    query.AddColumn("Nfts", "NAME");
                    query.AddColumn("Nfts", "IMAGE");
                    query.AddColumn("Nfts", "VIDEO");
                    // query.AddColumn("Nfts", "ROM");
                    // query.AddColumn("Nfts", "RAM");
                    query.AddColumn("Nfts", "MINT_DATE_UNIX_SECONDS");
                    query.AddColumn("Nfts", "MINT_NUMBER");
                    query.AddColumn("Nfts", "CHAIN_API_RESPONSE");
                    query.AddColumn("Nfts", "OFFCHAIN_API_RESPONSE");

                    query.AddSubselect(@"select ""ADDRESS"" as ""SERIES_CREATOR_ADDRESS"" from ""Addresses"" where ""Addresses"".""ID"" = ""Serieses"".""CreatorAddressId""");

                    if (order_by.ToUpper() == "MINT_DATE")
                    {
                        query.SetOrderBy("Nfts", "MINT_DATE_UNIX_SECONDS");
                    }
                    else
                    {
                        query.SetOrderBy("", order_by.ToUpper());
                    }

                    query.SetOrderDirection(order_direction);
                    query.SetLimit(limit);
                    query.SetOffset(offset);

                    var resultRoot = pgConnection.ExecQueryDN(query.GetQuery(), query.GetParams());

                    if (with_total == 1)
                    {
                        //DateTime totalStartTime = DateTime.Now;
                        totalResults = (long)pgConnection.ExecQueryI(query.GetCountQuery("total_results"), query.GetParams());

                        //TimeSpan totalTime = DateTime.Now - totalStartTime;
                        //Log.Write($"Assets total calculated in {Math.Round(totalTime.TotalSeconds, 3)} sec");
                        //Log.Write($"Total args: auction_state: {auction_state} auction_type: {auction_type} bidder: {bidder} creator: {creator} owner: {owner} contract: {contract} name: {name} chain: {chain} symbol: {symbol} quote_symbol: {quote_symbol} collection_slug: {collection_slug} token_id: {token_id} series_id: {series_id} grouping: {grouping} only_verified: {only_verified} status: {status} filter1value: {filter1value} price_similar: {price_similar}");
                    }

                    nftsArray = resultRoot.Select(x =>
                    {
                        var infusionData = pgConnection.ExecQueryDN($@"select ""KEY"", ""VALUE"" from ""Infusions"" where ""NftId"" = {x.GetString("ID")}")
                            .Select(x => new ApiResults.Infusion { key = x.GetString("KEY"), value = x.GetString("VALUE") }).ToArray();

                        ApiResults.InfusedInto? infusionIntoData = null;
                        var infusedIntoId = x.GetString("InfusedIntoId", null);
                        if (infusedIntoId != null)
                        {
                            infusionIntoData = pgConnection.ExecQueryDN($@"select ""TOKEN_ID"", ""NAME"", ""HASH"" from ""Nfts"", ""Chains"", ""Contracts"" where ""Nfts"".""ID"" = {infusedIntoId} and ""Nfts"".""ChainId"" = ""Chains"".""ID"" and ""Nfts"".""ContractId"" = ""Contracts"".""ID""")
                                .Select(x => new ApiResults.InfusedInto { token_id = x.GetString("TOKEN_ID"), chain = x.GetString("NAME").ToLower(), contract = ContractMethods.Prepend0x(x.GetString("HASH"), x.GetString("NFT_INFUSED_INTO_CHAIN"))}).FirstOrDefault();
                        }

                        var creatorAddress = databaseContext.Addresses.Where(a => a.ID == x.GetInt32("CreatorAddressId", 0)).FirstOrDefault();

                        // We use it to determine if item has metadata.
                        var metadataName = x.GetString("NAME", null);

                        return new ApiResults.Nft()
                        {
                            token_id = x.GetString("TOKEN_ID"),
                            chain = x.GetString("NFT_CHAIN_NAME").ToLower(),
                            symbol = x.GetString("BASE_SYMBOL"),
                            creator_address = creatorAddress == null ? null : AddressMethods.Prepend0x(creatorAddress.ADDRESS, x.GetString("NFT_CHAIN_NAME")),
                            creator_onchain_name = creatorAddress == null ? null : creatorAddress.ADDRESS_NAME,
                            owners = databaseContext.NftOwnerships.Where(o => o.NftId == x.GetInt32("ID", 0)).Select(o => new NftOwnershipResult { address = AddressMethods.Prepend0x(o.Address.ADDRESS, x.GetString("NFT_CHAIN_NAME", null), true), onchain_name = o.Address.ADDRESS_NAME, offchain_name = o.Address.USER_NAME, amount = o.AMOUNT }).ToArray(),
                            contract = ContractMethods.Prepend0x(x.GetString("CONTRACT_HASH"), x.GetString("NFT_CHAIN_NAME")),
                            nft_metadata = new ApiResults.NftMetadata()
                            {
                                name = metadataName,
                                description = x.GetString("DESCRIPTION", null),
                                image = x.GetString("IMAGE", null),
                                video = x.GetString("VIDEO", null),
                                info_url = x.GetString("INFO_URL", null),
                                /*rom = x.GetString("ROM", null),
                                ram = x.GetString("RAM", null),*/
                                mint_date = metadataName != null ? x.GetInt64("MINT_DATE_UNIX_SECONDS", 0).ToString() : null,
                                mint_number = metadataName != null ? x.GetString("MINT_NUMBER", null) : null,
                            },
                            series = new ApiResults.Series()
                            {
                                id = x.GetString("SERIES_ID"),
                                creator = AddressMethods.Prepend0x(x.GetString("SERIES_CREATOR_ADDRESS"), x.GetString("NFT_CHAIN_NAME")),
                                current_supply = x.GetInt32("SERIES_CURRENT_SUPPLY", 0),
                                max_supply = x.GetInt32("SERIES_MAX_SUPPLY", 0),
                                mode_name = x.GetString("MODE_NAME"),
                                name = x.GetString("SERIES_NAME"),
                                description = x.GetString("SERIES_DESCRIPTION"),
                                image = x.GetString("SERIES_IMAGE"),
                                royalties = x.GetString("ROYALTIES", "0"),
                                type = x.GetInt32("SERIES_TYPE", 0),
                                attrType1 = x.GetString("ATTR_TYPE_1"),
                                attrValue1 = x.GetString("ATTR_VALUE_1"),
                                attrType2 = x.GetString("ATTR_TYPE_2"),
                                attrValue2 = x.GetString("ATTR_VALUE_2"),
                                attrType3 = x.GetString("ATTR_TYPE_3"),
                                attrValue3 = x.GetString("ATTR_VALUE_3")
                            },
                            infusion = infusionData,
                            infused_into = infusionIntoData
                        };
                    }).ToArray();

                    TimeSpan responseTime = DateTime.Now - startTime;

                    Log.Information($"API result generated in {Math.Round(responseTime.TotalSeconds, 3)} sec");
                }
                catch (APIException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    var logMessage = LogEx.Exception("Nfts()", e);

                    throw new APIException(logMessage, e);
                }
            }

            return new NftsResult { total_results = with_total == 1 ? totalResults : null, nfts = nftsArray };
        }
    }
}