using System;
using System.Globalization;
using System.Linq;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Block = GhostDevs.Service.ApiResults.Block;
using Event = GhostDevs.Service.ApiResults.Event;
using Oracle = GhostDevs.Service.ApiResults.Oracle;
using Series = GhostDevs.Service.ApiResults.Series;
using Transaction = GhostDevs.Service.ApiResults.Transaction;

namespace GhostDevs.Service;

public partial class Endpoints
{
    [APIInfo(typeof(BlockResult), "Returns the block information from backend.", false, 10)]
    public BlockResult Blocks(
        [APIParameter("Order by [id, hash]", "string")]
        string order_by = "id",
        [APIParameter("Order direction [asc, desc]", "string")]
        string order_direction = "asc",
        [APIParameter("Offset", "integer")] int offset = 0,
        [APIParameter("Limit", "integer")] int limit = 50,
        [APIParameter("hash", "string")] string hash = "",
        [APIParameter("hash (partial match)", "string")]
        string hash_partial = "",
        [APIParameter("height of the block", "string")]
        string height = "",
        [APIParameter("Return data with oracles of the block", "integer")]
        int with_oracles = 0,
        [APIParameter("Return data with transactions of the block", "integer")]
        int with_transactions = 0,
        [APIParameter("Return data with events of the transaction", "integer")]
        int with_events = 0,
        [APIParameter("Return data with nft metadata", "integer")]
        int with_nft = 0
    )
    {
        Block[] blockArray;
        const string fiatCurrency = "USD";

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

                if ( !string.IsNullOrEmpty(hash) && !ArgValidation.CheckString(hash) )
                    throw new APIException("Unsupported value for 'hash' parameter.");

                if ( !string.IsNullOrEmpty(hash_partial) && !ArgValidation.CheckAddress(hash_partial) )
                    throw new APIException("Unsupported value for 'hash_partial' parameter.");

                if ( !string.IsNullOrEmpty(height) && !ArgValidation.CheckNumber(height) )
                    throw new APIException("Unsupported value for 'height' parameter.");

                var startTime = DateTime.Now;
                var fiatPricesInUsd = FiatExchangeRateMethods.GetPrices(databaseContext);

                var query = databaseContext.Blocks
                    .Include(x => x.ChainAddress)
                    .Include(x => x.ValidatorAddress)
                    .AsQueryable();

                if ( with_oracles == 1 )
                    query = query.Include(x => x.BlockOracles).ThenInclude(x => x.Oracle)
                        .AsQueryable();

                if ( with_transactions == 1 )
                {
                    query = query.Include(x => x.Transactions).AsQueryable();
                    if ( with_events == 1 )
                    {
                        query = query.Include(x => x.Transactions)
                            .ThenInclude(x => x.Events)
                            .Include(x => x.Transactions)
                            .ThenInclude(x => x.Events)
                            .ThenInclude(x => x.EventKind)
                            .Include(x => x.Transactions)
                            .ThenInclude(x => x.Events)
                            .ThenInclude(x => x.Chain)
                            .Include(x => x.Transactions)
                            .ThenInclude(x => x.Events)
                            .ThenInclude(x => x.Contract)
                            .Include(x => x.Transactions)
                            .ThenInclude(x => x.Events)
                            .ThenInclude(x => x.Address)
                            .AsQueryable();

                        if ( with_nft == 1 )
                            //not sure i need the following 3
                            query = query.Include(x => x.Transactions)
                                .ThenInclude(x => x.Events)
                                .ThenInclude(x => x.SourceAddress)
                                .Include(x => x.Transactions)
                                .ThenInclude(x => x.Events)
                                .ThenInclude(x => x.QuoteSymbol)
                                .Include(x => x.Transactions)
                                .ThenInclude(x => x.Events)
                                .ThenInclude(x => x.InfusedSymbol)
                                //ntf, series stuff
                                .Include(x => x.Transactions)
                                .ThenInclude(x => x.Events)
                                .ThenInclude(x => x.Nft)
                                .ThenInclude(x => x.Series)
                                .ThenInclude(x => x.Contract)
                                .ThenInclude(x => x.Chain)
                                .Include(x => x.Transactions)
                                .ThenInclude(x => x.Events)
                                .ThenInclude(x => x.Nft)
                                .ThenInclude(x => x.Series)
                                .ThenInclude(x => x.CreatorAddress)
                                .Include(x => x.Transactions)
                                .ThenInclude(x => x.Events)
                                .ThenInclude(x => x.Nft)
                                .ThenInclude(x => x.Series)
                                .ThenInclude(x => x.SeriesMode)
                                .AsQueryable();
                    }
                }

                if ( !string.IsNullOrEmpty(hash) )
                    query = query.Where(x => string.Equals(x.HASH.ToUpper(), hash.ToUpper()));

                if ( !string.IsNullOrEmpty(hash_partial) )
                    query = query.Where(x => x.HASH.ToUpper().Contains(hash_partial.ToUpper()));

                if ( !string.IsNullOrEmpty(height) )
                    query = query.Where(x => string.Equals(x.HEIGHT, height));

                //in case we add more to sort
                if ( order_direction == "asc" )
                    query = order_by switch
                    {
                        "id" => query.OrderBy(x => x.ID),
                        "hash" => query.OrderBy(x => x.HASH),
                        _ => query
                    };
                else
                    query = order_by switch
                    {
                        "id" => query.OrderByDescending(x => x.ID),
                        "hash" => query.OrderByDescending(x => x.HASH),
                        _ => query
                    };

                var queryResults = query.Skip(offset).Take(limit).ToList();


                blockArray = queryResults.Select(x => new Block
                {
                    height = x.HEIGHT,
                    hash = x.HASH,
                    previous_hash = x.PREVIOUS_HASH,
                    protocol = x.PROTOCOL,
                    chain_address = x.ChainAddress.ADDRESS,
                    validator_address = x.ValidatorAddress.ADDRESS,
                    oracles = ( with_oracles == 1 && x.BlockOracles != null
                        ? x.BlockOracles.Select(o => new Oracle
                        {
                            url = o.Oracle.URL,
                            content = o.Oracle.CONTENT
                        }).ToArray()
                        : null ) ?? Array.Empty<Oracle>(),
                    Transactions = ( with_transactions == 1 && x.Transactions != null
                        ? x.Transactions.Select(t => new Transaction
                        {
                            hash = t.HASH,
                            blockHeight = x.HEIGHT,
                            index = t.INDEX,
                            events = ( with_events == 1 && t.Events != null
                                ? t.Events.Select(e => new Event
                                {
                                    chain = e.Chain.NAME.ToLower(),
                                    contract = ContractMethods.Prepend0x(e.Contract.HASH, e.Chain.NAME),
                                    date = e.TIMESTAMP_UNIX_SECONDS.ToString(),
                                    transaction_hash = x.HASH, //a bit redundant in that case
                                    token_id = e.TOKEN_ID,
                                    token_amount = e.TOKEN_AMOUNT,
                                    event_kind = e.EventKind.NAME,
                                    base_symbol = e.Contract.SYMBOL,
                                    price = e.PRICE,
                                    fiat_price = FiatExchangeRateMethods
                                        .Convert(fiatPricesInUsd, e.PRICE_USD, "USD", fiatCurrency)
                                        .ToString("0.####"),
                                    fiat_currency = fiatCurrency,
                                    quote_symbol = e.QuoteSymbol?.SYMBOL!,
                                    infused_symbol = e.InfusedSymbol?.SYMBOL!,
                                    infused_value = e.INFUSED_VALUE,
                                    address = AddressMethods.Prepend0x(e.Address.ADDRESS, e.Chain.NAME),
                                    onchain_name = e.Address.ADDRESS_NAME,
                                    source_address = AddressMethods.Prepend0x(e.SourceAddress?.ADDRESS, e.Chain.NAME),
                                    source_onchain_name = e.SourceAddress?.ADDRESS_NAME!,
                                    nft_metadata = with_nft == 1 && e.Nft != null
                                        ? new NftMetadata
                                        {
                                            name = e.Nft.NAME,
                                            description = e.Nft.DESCRIPTION,
                                            image = e.Nft.IMAGE,
                                            video = e.Nft.VIDEO,
                                            rom = e.Nft.ROM,
                                            ram = e.Nft.RAM,
                                            mint_date = e.Nft.MINT_DATE_UNIX_SECONDS.ToString(),
                                            mint_number = e.Nft.MINT_NUMBER.ToString()
                                        }
                                        : null,
                                    series = with_nft == 1 && e.Nft?.Series != null
                                        ? new Series
                                        {
                                            id = e.Nft.Series.SERIES_ID,
                                            creator =
                                                AddressMethods.Prepend0x(e.Nft.Series.CreatorAddress?.ADDRESS,
                                                    e.Chain.NAME),
                                            current_supply = e.Nft.Series.CURRENT_SUPPLY,
                                            max_supply = e.Nft.Series.MAX_SUPPLY,
                                            mode_name = e.Nft.Series.SeriesMode?.MODE_NAME!,
                                            name = e.Nft.Series.NAME,
                                            description = e.Nft.Series.DESCRIPTION,
                                            image = e.Nft.Series.IMAGE,
                                            royalties = e.Nft.Series.ROYALTIES.ToString(CultureInfo.InvariantCulture),
                                            type = e.Nft.Series.TYPE,
                                            attrType1 = e.Nft.Series.ATTR_TYPE_1,
                                            attrValue1 = e.Nft.Series.ATTR_VALUE_1,
                                            attrType2 = e.Nft.Series.ATTR_TYPE_2,
                                            attrValue2 = e.Nft.Series.ATTR_VALUE_2,
                                            attrType3 = e.Nft.Series.ATTR_TYPE_3,
                                            attrValue3 = e.Nft.Series.ATTR_VALUE_3
                                        }
                                        : null
                                }).ToArray()
                                : null ) ?? Array.Empty<Event>()
                        }).ToArray()
                        : null ) ?? Array.Empty<Transaction>()
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
                var logMessage = LogEx.Exception("Block()", exception);

                throw new APIException(logMessage, exception);
            }
        }

        return new BlockResult {blocks = blockArray};
    }
}
