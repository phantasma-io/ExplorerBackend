using System;
using System.Globalization;
using System.Linq;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Event = GhostDevs.Service.ApiResults.Event;
using Series = GhostDevs.Service.ApiResults.Series;
using Transaction = GhostDevs.Service.ApiResults.Transaction;

namespace GhostDevs.Service;

public partial class Endpoints
{
    [APIInfo(typeof(TransactionResult), "Returns the transaction on the backend.", false, 10)]
    public TransactionResult Transactions(
        [APIParameter("Order by [id, hash]", "string")]
        string order_by = "id",
        [APIParameter("Order direction [asc, desc]", "string")]
        string order_direction = "asc",
        [APIParameter("Offset", "integer")] int offset = 0,
        [APIParameter("Limit", "integer")] int limit = 50,
        [APIParameter("hash", "string")] string hash = "",
        [APIParameter("Return total (slower) or not (faster)", "integer")]
        int with_total = 0,
        [APIParameter("Return data with nft metadata", "integer")]
        int with_nft = 0
    )
    {
        long totalResults = 0;
        Transaction[] transactionArray;
        var fiat_currency = "USD";

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
                    throw new APIException("Unsupported value for 'address' parameter.");

                var startTime = DateTime.Now;

                var fiatPricesInUsd = FiatExchangeRateMethods.GetPrices(databaseContext);

                var query = databaseContext.Transactions
                    .Include(x => x.Block)
                    .Include(x => x.Events)
                    .Include(x => x.Events).ThenInclude(x => x.EventKind)
                    .Include(x => x.Events).ThenInclude(x => x.Chain)
                    .Include(x => x.Events).ThenInclude(x => x.Contract)
                    .Include(x => x.Events).ThenInclude(x => x.Address)
                    .AsQueryable();
                if ( with_nft == 1 )
                    //not sure i need the following 3
                    query = query.Include(x => x.Events).ThenInclude(x => x.SourceAddress)
                        .Include(x => x.Events).ThenInclude(x => x.QuoteSymbol)
                        .Include(x => x.Events).ThenInclude(x => x.InfusedSymbol)
                        //ntf, series stuff
                        .Include(x => x.Events).ThenInclude(x => x.Nft).ThenInclude(x => x.Series)
                        .ThenInclude(x => x.Contract)
                        .ThenInclude(x => x.Chain)
                        .Include(x => x.Events).ThenInclude(x => x.Nft).ThenInclude(x => x.Series)
                        .ThenInclude(x => x.CreatorAddress)
                        .Include(x => x.Events).ThenInclude(x => x.Nft).ThenInclude(x => x.Series)
                        .ThenInclude(x => x.SeriesMode)
                        .AsQueryable();


                if ( !string.IsNullOrEmpty(hash) )
                    query = query.Where(x => string.Equals(x.HASH.ToUpper(), hash.ToUpper()));

                // Count total number of results before adding order and limit parts of query.
                if ( with_total == 1 )
                    totalResults = query.Count();

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

                transactionArray = ( from x in queryResults
                    let events = x.Events
                    select new Transaction
                    {
                        hash = x.HASH,
                        blockHeight = x.Block.HEIGHT,
                        index = x.INDEX,
                        events = events.Select(e => new Event
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
                                    .Convert(fiatPricesInUsd, e.PRICE_USD, "USD", fiat_currency)
                                    .ToString("0.####"),
                                fiat_currency = fiat_currency,
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
                            })
                            .ToArray()
                    } ).ToArray();

                var responseTime = DateTime.Now - startTime;

                Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
            }
            catch ( APIException )
            {
                throw;
            }
            catch ( Exception exception )
            {
                var logMessage = LogEx.Exception("Address()", exception);

                throw new APIException(logMessage, exception);
            }
        }

        return new TransactionResult
            {total_results = with_total == 1 ? totalResults : null, transactions = transactionArray};
    }
}
