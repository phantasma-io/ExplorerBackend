using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Service.Api;

public partial class Endpoints
{
    private static async Task<Transaction[]> ProcessAllTransactions(MainDbContext mainDbContext, IQueryable<Database.Main.Transaction> _transactions,
        int with_script, int with_events, int with_event_data, int with_nft, int with_fiat, string fiatCurrency,
        Dictionary<string, decimal> fiatPricesInUsd)
    {
        /*_transactions = _transactions.Include(x => x.Block)
            .ThenInclude(b => b.Chain)
            .Include(x => x.State)
            .Include(x => x.AddressTransactions)
            .ThenInclude(at => at.Address)
            .Include(x => x.Sender)
            .Include(x => x.GasPayer)
            .Include(x => x.GasTarget);

        if ( with_events == 1 )
        {
            _transactions = _transactions
                .Include(x => x.Events)
                .ThenInclude(e => e.EventKind)
                .Include(x => x.Events)
                .ThenInclude(e => e.Chain)
                .Include(x => x.Events)
                .ThenInclude(e => e.Address);
        }
        
        if (with_nft == 1)
        {
            _transactions = _transactions.Include(x => x.Events)
                .ThenInclude(e => e.Nft)
                .ThenInclude(nft => nft.Series)
                .ThenInclude(series => series.CreatorAddress)
                .Include(x => x.Events)
                .ThenInclude(e => e.Nft)
                .ThenInclude(nft => nft.Series)
                .ThenInclude(series => series.SeriesMode);
        }

        if (with_event_data == 1)
        {
            // Include other related entities, such as AddressEvent, ChainEvent, etc.
            _transactions = _transactions.Include(x => x.Events)
                .ThenInclude(e => e.AddressEvent)
                .ThenInclude(ae => ae.Address)
                .Include(x => x.Events)
                .ThenInclude(e => e.HashEvent)
                .Include(x => x.Events)
                .ThenInclude(e => e.Contract)
                .ThenInclude(c => c.Address)
                .Include(x => x.Events)
                .ThenInclude(e => e.ChainEvent)
                .ThenInclude(c => c.Chain)
                .Include(x => x.Events)
                .ThenInclude(e => e.GasEvent)
                .ThenInclude(g => g.Address)
                .Include(x => x.Events)
                .ThenInclude(e => e.HashEvent)
                .Include(x => x.Events)
                .ThenInclude(e => e.InfusionEvent)
                .ThenInclude(i => i.BaseToken)
                .Include(x => x.Events)
                .ThenInclude(e => e.InfusionEvent)
                .ThenInclude(i => i.InfusedToken)
                .Include(x => x.Events)
                .ThenInclude(e => e.SaleEvent)
                .ThenInclude(s => s.SaleEventKind)
                .Include(x => x.Events)
                .ThenInclude(e => e.StringEvent)
                .Include(x => x.Events)
                .ThenInclude(e => e.OrganizationEvent)
                .ThenInclude(o => o.Organization)
                .Include(x => x.Events)
                .ThenInclude(e => e.OrganizationEvent)
                .ThenInclude(o => o.Address)
                .Include(x => x.Events)
                .ThenInclude(e => e.TokenEvent)
                .ThenInclude(t => t.Token)
                .Include(x => x.Events)
                .ThenInclude(e => e.TransactionSettleEvent)
                .ThenInclude(t => t.Platform)
                .Include(x => x.Events)
                .ThenInclude(e => e.MarketEvent)
                .ThenInclude(m => m.MarketEventKind)
                .Include(x => x.Events)
                .ThenInclude(e => e.MarketEvent)
                .ThenInclude(m => m.BaseToken)
                .Include(x => x.Events)
                .ThenInclude(e => e.MarketEvent)
                .ThenInclude(m => m.QuoteToken)
                .Include(x => x.Events)
                .ThenInclude(e => e.MarketEvent)
                .ThenInclude(m => m.MarketEventFiatPrice);
        }

        if (with_fiat == 1)
        {
            // Include other related entities for fiat prices
            _transactions = _transactions.Include(x => x.Events)
                .ThenInclude(e => e.MarketEvent)
                .ThenInclude(me => me.MarketEventFiatPrice);
        }*/
        
        Log.Information("My query: {query}", _transactions.ToQueryString());

        
        Log.Information("Getting transactions from database...");
        //var txs = await _transactions.ToListAsync();
        Log.Information("Transactions retrieved from database");

        Log.Information("Processing transactions...");
        var tasks = new List<Task<Transaction>>();
        foreach (var x in _transactions.AsQueryable())
        {
            tasks.Add(ProcessTransactionAsync(x, with_script, with_events, with_event_data, with_nft, with_fiat, fiatCurrency, fiatPricesInUsd));
        }

        /*var result = _transactions.Select(x => new Transaction
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
            state = x.State.NAME,
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
                : null,
            events = with_events == 1 && x.Events != null
            //    ? CreateEventsForTransaction (x, with_nft, with_event_data, with_fiat, fiatCurrency, fiatPricesInUsd) : null
            
            x.Events.Select(e => 
                    CreateEvent(x.HASH, e, with_nft, with_event_data, with_fiat, fiatCurrency, fiatPricesInUsd)).ToArray() 
                : null
             new Event
                {
                    event_id = e.ID,
                    chain = e.Chain.NAME.ToLower(),
                    date = e.TIMESTAMP_UNIX_SECONDS.ToString(),
                    transaction_hash = x.HASH, //a bit redundant in that case
                    token_id = e.TOKEN_ID,
                    event_kind = e.EventKind.NAME,
                    address = e.Address.ADDRESS,
                    address_name = e.Address.ADDRESS_NAME,
                    contract = new Contract
                    {
                        name = e.Contract.NAME,
                        hash = ContractMethods.Prepend0x(e.Contract.HASH, e.Chain.NAME),
                        symbol = e.Contract.SYMBOL
                    },
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
                            mode_name = e.Nft.Series.SeriesMode != null ? e.Nft.Series.SeriesMode.MODE_NAME : null,
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
                        : null,
                    address_event = with_event_data == 1 && e.AddressEvent != null
                        ? new AddressEvent
                        {
                            address = e.AddressEvent.Address != null
                                ? new Address
                                {
                                    address_name = e.AddressEvent.Address.ADDRESS_NAME,
                                    address = e.AddressEvent.Address.ADDRESS
                                }
                                : null
                        }
                        : null,
                    chain_event = with_event_data == 1 && e.ChainEvent != null
                        ? new ChainEvent
                        {
                            name = e.ChainEvent.NAME,
                            value = e.ChainEvent.VALUE,
                            chain = e.ChainEvent.Chain != null
                                ? new Chain
                                {
                                    chain_name = e.ChainEvent.Chain.NAME
                                }
                                : null
                        }
                        : null,
                    gas_event = with_event_data == 1 && e.GasEvent != null
                        ? new GasEvent
                        {
                            price = e.GasEvent.PRICE,
                            amount = e.GasEvent.AMOUNT,
                            fee = e.GasEvent.FEE,
                            address = e.GasEvent.Address != null
                                ? new Address
                                {
                                    address = e.GasEvent.Address.ADDRESS,
                                    address_name = e.GasEvent.Address.ADDRESS_NAME
                                }
                                : null
                        }
                        : null,
                    hash_event = with_event_data == 1 && e.HashEvent != null
                        ? new HashEvent
                        {
                            hash = e.HashEvent.HASH
                        }
                        : null,
                    infusion_event = with_event_data == 1 && e.InfusionEvent != null
                        ? new InfusionEvent
                        {
                            token_id = e.InfusionEvent.TOKEN_ID,
                            infused_value = e.InfusionEvent.INFUSED_VALUE,
                            infused_value_raw = e.InfusionEvent.INFUSED_VALUE_RAW,
                            base_token = e.InfusionEvent.BaseToken != null
                                ? new Token
                                {
                                    symbol = e.InfusionEvent.BaseToken.SYMBOL,
                                    fungible = e.InfusionEvent.BaseToken.FUNGIBLE,
                                    transferable = e.InfusionEvent.BaseToken.TRANSFERABLE,
                                    finite = e.InfusionEvent.BaseToken.FINITE,
                                    divisible = e.InfusionEvent.BaseToken.DIVISIBLE,
                                    fiat = e.InfusionEvent.BaseToken.FIAT,
                                    fuel = e.InfusionEvent.BaseToken.FUEL,
                                    swappable = e.InfusionEvent.BaseToken.SWAPPABLE,
                                    burnable = e.InfusionEvent.BaseToken.BURNABLE,
                                    stakable = e.InfusionEvent.BaseToken.STAKABLE,
                                    decimals = e.InfusionEvent.BaseToken.DECIMALS
                                }
                                : null,
                            infused_token = e.InfusionEvent.InfusedToken != null
                                ? new Token
                                {
                                    symbol = e.InfusionEvent.InfusedToken.SYMBOL,
                                    fungible = e.InfusionEvent.InfusedToken.FUNGIBLE,
                                    transferable = e.InfusionEvent.InfusedToken.TRANSFERABLE,
                                    finite = e.InfusionEvent.InfusedToken.FINITE,
                                    divisible = e.InfusionEvent.InfusedToken.DIVISIBLE,
                                    fiat = e.InfusionEvent.InfusedToken.FIAT,
                                    fuel = e.InfusionEvent.InfusedToken.FUEL,
                                    swappable = e.InfusionEvent.InfusedToken.SWAPPABLE,
                                    burnable = e.InfusionEvent.InfusedToken.BURNABLE,
                                    stakable = e.InfusionEvent.InfusedToken.STAKABLE,
                                    decimals = e.InfusionEvent.InfusedToken.DECIMALS
                                }
                                : null
                        }
                        : null,
                    market_event = with_event_data == 1 && e.MarketEvent != null
                        ? new MarketEvent
                        {
                            base_token = e.MarketEvent.BaseToken != null
                                ? new Token
                                {
                                    symbol = e.MarketEvent.BaseToken.SYMBOL,
                                    fungible = e.MarketEvent.BaseToken.FUNGIBLE,
                                    transferable = e.MarketEvent.BaseToken.TRANSFERABLE,
                                    finite = e.MarketEvent.BaseToken.FINITE,
                                    divisible = e.MarketEvent.BaseToken.DIVISIBLE,
                                    fiat = e.MarketEvent.BaseToken.FIAT,
                                    fuel = e.MarketEvent.BaseToken.FUEL,
                                    swappable = e.MarketEvent.BaseToken.SWAPPABLE,
                                    burnable = e.MarketEvent.BaseToken.BURNABLE,
                                    stakable = e.MarketEvent.BaseToken.STAKABLE,
                                    decimals = e.MarketEvent.BaseToken.DECIMALS
                                }
                                : null,
                            quote_token = e.MarketEvent.QuoteToken != null
                                ? new Token
                                {
                                    symbol = e.MarketEvent.QuoteToken.SYMBOL,
                                    fungible = e.MarketEvent.QuoteToken.FUNGIBLE,
                                    transferable = e.MarketEvent.QuoteToken.TRANSFERABLE,
                                    finite = e.MarketEvent.QuoteToken.FINITE,
                                    divisible = e.MarketEvent.QuoteToken.DIVISIBLE,
                                    fiat = e.MarketEvent.QuoteToken.FIAT,
                                    fuel = e.MarketEvent.QuoteToken.FUEL,
                                    swappable = e.MarketEvent.QuoteToken.SWAPPABLE,
                                    burnable = e.MarketEvent.QuoteToken.BURNABLE,
                                    stakable = e.MarketEvent.QuoteToken.STAKABLE,
                                    decimals = e.MarketEvent.QuoteToken.DECIMALS
                                }
                                : null,
                            end_price = e.MarketEvent.END_PRICE,
                            price = e.MarketEvent.PRICE,
                            market_event_kind = e.MarketEvent.MarketEventKind.NAME,
                            market_id = e.MarketEvent.MARKET_ID,
                            fiat_price = with_fiat == 1
                                ? new FiatPrice
                                {
                                    fiat_currency = e.MarketEvent.MarketEventFiatPrice.FIAT_NAME,
                                    fiat_price = FiatExchangeRateMethods.Convert(fiatPricesInUsd,
                                        e.MarketEvent.MarketEventFiatPrice.PRICE_USD,
                                        e.MarketEvent.MarketEventFiatPrice.FIAT_NAME,
                                        fiatCurrency).ToString("0.####"),
                                    fiat_price_end = FiatExchangeRateMethods.Convert(fiatPricesInUsd,
                                        e.MarketEvent.MarketEventFiatPrice.PRICE_END_USD,
                                        e.MarketEvent.MarketEventFiatPrice.FIAT_NAME,
                                        fiatCurrency).ToString("0.####")
                                }
                                : null
                        }
                        : null,
                    organization_event = with_event_data == 1 && e.OrganizationEvent != null
                        ? new OrganizationEvent
                        {
                            organization = e.OrganizationEvent.Organization != null
                                ? new Organization
                                {
                                    name = e.OrganizationEvent.Organization.NAME
                                }
                                : null,
                            address = e.OrganizationEvent.Address != null
                                ? new Address
                                {
                                    address = e.OrganizationEvent.Address.ADDRESS,
                                    address_name = e.OrganizationEvent.Address.ADDRESS_NAME
                                }
                                : null
                        }
                        : null,
                    sale_event = with_event_data == 1 && e.SaleEvent != null
                        ? new SaleEvent
                        {
                            hash = e.SaleEvent.HASH,
                            sale_event_kind = e.SaleEvent.SaleEventKind.NAME
                        }
                        : null,
                    string_event = with_event_data == 1 && e.StringEvent != null
                        ? new StringEvent
                        {
                            string_value = e.StringEvent.STRING_VALUE
                        }
                        : null,
                    token_event = with_event_data == 1 && e.TokenEvent != null
                        ? new TokenEvent
                        {
                            token = e.TokenEvent.Token != null
                                ? new Token
                                {
                                    symbol = e.TokenEvent.Token.SYMBOL,
                                    fungible = e.TokenEvent.Token.FUNGIBLE,
                                    transferable = e.TokenEvent.Token.TRANSFERABLE,
                                    finite = e.TokenEvent.Token.FINITE,
                                    divisible = e.TokenEvent.Token.DIVISIBLE,
                                    fiat = e.TokenEvent.Token.FIAT,
                                    fuel = e.TokenEvent.Token.FUEL,
                                    swappable = e.TokenEvent.Token.SWAPPABLE,
                                    burnable = e.TokenEvent.Token.BURNABLE,
                                    stakable = e.TokenEvent.Token.STAKABLE,
                                    decimals = e.TokenEvent.Token.DECIMALS
                                }
                                : null,
                            value = e.TokenEvent.VALUE,
                            value_raw = e.TokenEvent.VALUE_RAW,
                            chain_name = e.TokenEvent.CHAIN_NAME
                        }
                        : null,
                    transaction_settle_event = with_event_data == 1 && e.TransactionSettleEvent != null
                        ? new TransactionSettleEvent
                        {
                            hash = e.TransactionSettleEvent.HASH,
                            platform = e.TransactionSettleEvent.Platform != null
                                ? new Platform
                                {
                                    name = e.TransactionSettleEvent.Platform.NAME,
                                    chain = e.TransactionSettleEvent.Platform.CHAIN,
                                    fuel = e.TransactionSettleEvent.Platform.FUEL
                                    //we do not add other information here for now
                                }
                                : null
                        }
                        : null
                }).ToArray()
                : null

            //ProcessTransaction(_transaction, with_script, with_events, with_event_data, with_nft, with_fiat,
            //    fiatCurrency, fiatPricesInUsd)
        }).ToArray();*/
        var results = await Task.WhenAll(tasks);
        return results;
    }
    
    private static async Task<Transaction> ProcessTransactionAsync(Database.Main.Transaction x, int with_script, int with_events, int with_event_data, int with_nft, int with_fiat, string fiatCurrency,
        Dictionary<string, decimal> fiatPricesInUsd)
    {
        Log.Information("Transactions retrieved from database, processing transaction {hash}", x.HASH);
        await using MainDbContext databaseContext2 = new();
        
        x = await databaseContext2.Transactions.FindAsync(x.ID);
        if ( x == null )
            return null;

        var transaction = new Transaction
        {
            hash = x.HASH,
            block_hash = x?.Block.HASH,
            block_height = x?.Block.HEIGHT,
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
            state = x?.State?.NAME,
            sender = x.Sender != null
                ? new Address
                {
                    address_name = x?.Sender.ADDRESS_NAME,
                    address = x?.Sender.ADDRESS
                }
                : null,
            gas_payer = x.GasPayer != null
                ? new Address
                {
                    address_name = x?.GasPayer.ADDRESS_NAME,
                    address = x?.GasPayer.ADDRESS
                }
                : null,
            gas_target = x.GasTarget != null 
                ? new Address
                {
                    address_name = x?.GasTarget?.ADDRESS_NAME,
                    address = x?.GasTarget?.ADDRESS
                }
                : null
        };
        
        var events = with_events == 1
            ? await CreateEventsForTransaction(x, with_nft, with_event_data, with_fiat, fiatCurrency, fiatPricesInUsd)
            : null;

        transaction.events = events;

        return transaction;
    }
    
    private static async Task<Event[]> CreateEventsForTransaction(Database.Main.Transaction x, int with_nft, int with_event_data, int with_fiat, string fiatCurrency, Dictionary<string, decimal> fiatPricesInUsd)
    {
        if (x.Events == null)
        {
            return null;
        }
        
        //var tasks = new List<Task<Event>>();
        var tasksEvents = new List<Task<Event[]>>();
        await using MainDbContext databaseContext = new();

        var events = databaseContext.Events.Where(e => e.TransactionId == x.ID)
            .Select(e=>new Database.Main.Event
            {
                ID = e.ID
            }).AsNoTracking();
        
        var count = await events.CountAsync();

        var chunks = events.AsEnumerable().Chunk(50);

        Log.Information("Events retrieved from database, processing {count} events for transaction {hash}", count, x.HASH);
        foreach ( var chunk in chunks )
        {
            Log.Information("Processing event {id} to {id_2} ", chunk.First().ID, chunk.Last().ID);

            tasksEvents.Add(Task.Run( () => LoadFromChunk(chunk, x, with_nft, with_event_data, with_fiat, fiatCurrency,
                fiatPricesInUsd)));
        }
        
        var resultsEvents = await Task.WhenAll(tasksEvents);
        
        return resultsEvents.SelectMany(a => a).ToArray();
    }


    private static async Task<Event[]> LoadFromChunk(Database.Main.Event[] chunk, Database.Main.Transaction x, int with_nft, int with_event_data, int with_fiat, string fiatCurrency, Dictionary<string, decimal> fiatPricesInUsd)
    {
        var tasks = new List<Event>();
        
        await using MainDbContext databaseContext = new();

        foreach (var e in chunk.AsEnumerable())
        {
            tasks.Add(CreateEventWihoutTask(databaseContext, x, e, with_nft, with_event_data, with_fiat, fiatCurrency,
                fiatPricesInUsd));
        }

        await databaseContext.DisposeAsync();

        return tasks.ToArray();
    }
    
    private static Event CreateEventWihoutTask(MainDbContext databaseContext, Database.Main.Transaction x, Database.Main.Event e, int with_nft, int with_event_data, int with_fiat,
        string fiatCurrency, Dictionary<string, decimal> fiatPricesInUsd)
    {
        e = databaseContext.Events.FirstOrDefault(_event => _event.ID == e.ID);
        if ( e == null)
        {
            return null;
        }

        var chainName = e.Chain.NAME.ToLower();
        
        return new Event
        {
            event_id = e.ID,
            chain = chainName,
            date = e.TIMESTAMP_UNIX_SECONDS.ToString(),
            transaction_hash = x.HASH,
            token_id = e.TOKEN_ID,
            event_kind = e.EventKind.NAME,
            address = e.Address.ADDRESS,
            address_name = e.Address.ADDRESS_NAME,
            contract = CreateContract(databaseContext, e, chainName),
            nft_metadata = with_nft == 1 && e.Nft != null ?  CreateNftMetadata(databaseContext, e) : null,
            series = with_nft == 1 && e.Nft != null && e.Nft.Series != null ?  CreateSeries(databaseContext, e) : null,
            address_event = with_event_data == 1 && e.TargetAddress != null ?  CreateAddressEvent(databaseContext, e) : null,
            chain_event = with_event_data == 1 && e.ChainEvent != null  ?  CreateChainEvent(databaseContext, e, chainName) : null,
            gas_event = with_event_data == 1 && e.GasEvent != null ?  CreateGasEvent(databaseContext, e) : null,
            hash_event = with_event_data == 1 && e.HashEvent != null  ?  CreateHashEvent(databaseContext, e) : null,
            infusion_event = with_event_data == 1 && e.InfusionEvent != null  ?  CreateInfusionEvent(databaseContext, e) : null,
            market_event = with_event_data == 1 && e.MarketEvent != null ?  CreateMarketEvent(databaseContext, e, with_fiat, fiatCurrency, fiatPricesInUsd) : null,
            organization_event = with_event_data == 1 && e.OrganizationEvent != null?  CreateOrganizationEvent(databaseContext, e) : null,
            sale_event = with_event_data == 1 && e.SaleEvent != null  ?  CreateSaleEvent(databaseContext, e) : null,
            string_event = with_event_data == 1 && e.StringEvent != null  ?  CreateStringEvent(databaseContext, e) : null,
            token_event = with_event_data == 1 && e.TokenEvent != null ?  CreateTokenEvent(databaseContext, e) : null,
            transaction_settle_event = with_event_data == 1 && e.TransactionSettleEvent != null  ?  CreateTransactionSettleEvent(databaseContext, e) : null
        };
    }

    private static async Task<Event> CreateEvent(Database.Main.Transaction x, Database.Main.Event e, int with_nft, int with_event_data, int with_fiat,
        string fiatCurrency, Dictionary<string, decimal> fiatPricesInUsd)
    {
        await using MainDbContext databaseContext = new();
        e = await databaseContext.Events.Where(_evnt => _evnt.ID == e.ID).FirstAsync();
        if ( e == null)
        {
            return null;
        }

        var chainName = e.Chain.NAME.ToLower();
        
        return new Event
        {
            event_id = e.ID,
            chain = chainName,
            date = e.TIMESTAMP_UNIX_SECONDS.ToString(),
            transaction_hash = x.HASH,
            token_id = e.TOKEN_ID,
            event_kind = e.EventKind.NAME,
            address = e.Address.ADDRESS,
            address_name = e.Address.ADDRESS_NAME,
            contract = CreateContract(databaseContext, e, chainName),
            nft_metadata = with_nft == 1 && e.Nft != null ?  CreateNftMetadata(databaseContext, e) : null,
            series = with_nft == 1 && e.Nft != null && e.Nft.Series != null ?  CreateSeries(databaseContext, e) : null,
            address_event = with_event_data == 1 && e.TargetAddress != null ?  CreateAddressEvent(databaseContext, e) : null,
            chain_event = with_event_data == 1 && e.ChainEvent != null  ?  CreateChainEvent(databaseContext, e, chainName) : null,
            gas_event = with_event_data == 1 && e.GasEvent != null ?  CreateGasEvent(databaseContext, e) : null,
            hash_event = with_event_data == 1 && e.HashEvent != null  ?  CreateHashEvent(databaseContext, e) : null,
            infusion_event = with_event_data == 1 && e.InfusionEvent != null  ?  CreateInfusionEvent(databaseContext, e) : null,
            market_event = with_event_data == 1 && e.MarketEvent != null ?  CreateMarketEvent(databaseContext, e, with_fiat, fiatCurrency, fiatPricesInUsd) : null,
            organization_event = with_event_data == 1 && e.OrganizationEvent != null?  CreateOrganizationEvent(databaseContext, e) : null,
            sale_event = with_event_data == 1 && e.SaleEvent != null  ?  CreateSaleEvent(databaseContext, e) : null,
            string_event = with_event_data == 1 && e.StringEvent != null  ?  CreateStringEvent(databaseContext, e) : null,
            token_event = with_event_data == 1 && e.TokenEvent != null ?  CreateTokenEvent(databaseContext, e) : null,
            transaction_settle_event = with_event_data == 1 && e.TransactionSettleEvent != null  ?  CreateTransactionSettleEvent(databaseContext, e) : null
        };
    }


    private static Contract CreateContract(MainDbContext mainDbContext, Database.Main.Event e, string chainName)
    {
        var contract = mainDbContext.Contracts
            .Where(c => c.ID == e.ContractId)
            .Take(1)
            .Select(c => new Contract
            {
                name = c.NAME,
                hash = c.HASH,
                symbol = c.SYMBOL
            })
            .FirstOrDefault();
        
        if ( contract == null)
        {
            return null;
        }

        return contract;
    }


    private static NftMetadata CreateNftMetadata(MainDbContext mainDbContext, Database.Main.Event e)
    {
        var nft = mainDbContext.Nfts.Where(n => n.ID == e.NftId)
            .Take(1)
            .Select(n => new NftMetadata
            {
                name = n.NAME,
                description = n.DESCRIPTION,
                image = n.IMAGE,
                video = n.VIDEO,
                rom = n.ROM,
                ram = n.RAM,
                mint_date = n.MINT_DATE_UNIX_SECONDS.ToString(),
                mint_number = n.MINT_NUMBER.ToString()
            }).FirstOrDefault();
        
        if ( nft == null)
        {
            return null;
        }
        
        return nft;
    }


    private static Series CreateSeries(MainDbContext mainDbContext, Database.Main.Event e)
    {
        //e = mainDbContext.Events.Find(e.ID);
        var series = mainDbContext.Serieses.Where(s => s.ID == e.Nft.Series.ID)
            .Include(s => s.CreatorAddress)
            .Include(s => s.SeriesMode)
            .Take(1)
            .Select(s => new Series
            {
                id = s.ID,
                series_id = s.SERIES_ID,
                creator = s.CreatorAddress != null
                    ? s.CreatorAddress.ADDRESS
                    : null,
                current_supply = s.CURRENT_SUPPLY,
                max_supply = s.MAX_SUPPLY,
                mode_name = s.SeriesMode != null ? s.SeriesMode.MODE_NAME : null,
                name = s.NAME,
                description =s.DESCRIPTION,
                image = s.IMAGE,
                royalties = s.ROYALTIES.ToString(CultureInfo.InvariantCulture),
                type = s.TYPE,
                attr_type_1 = s.ATTR_TYPE_1,
                attr_value_1 = s.ATTR_VALUE_1,
                attr_type_2 = s.ATTR_TYPE_2,
                attr_value_2 = s.ATTR_VALUE_2,
                attr_type_3 = s.ATTR_TYPE_3,
                attr_value_3 = s.ATTR_VALUE_3
            })
            .FirstOrDefault();
        
        if ( series == null)
        {
            return null;
        }

        return series;
    }


    private static AddressEvent CreateAddressEvent(MainDbContext mainDbContext, Database.Main.Event e)
    {
        return e.TargetAddress != null ? new AddressEvent
            {
                address = new Address
                    {
                        address_name = e.TargetAddress.ADDRESS_NAME,
                        address = e.TargetAddress.ADDRESS
                    }
            } : null;
    }


    private static ChainEvent CreateChainEvent(MainDbContext mainDbContext, Database.Main.Event e, string chainName)
    {
        var chainEvent = mainDbContext.ChainEvents.Where(c => c.EventId == e.ID)
            .Take(1)
            .Select(c => new ChainEvent
            {
                name = c.NAME,
                value = c.VALUE,
                chain = c.Chain != null
                    ? new Chain
                    {
                        chain_name = chainName
                    }
                    : null
            })
            .FirstOrDefault();
        
        if ( chainEvent == null)
        {
            return null;
        }

        return chainEvent;
    }


    private static GasEvent CreateGasEvent(MainDbContext mainDbContext, Database.Main.Event e)
    {
        var gasEvent = mainDbContext.GasEvents.Where(g => g.EventId == e.ID)
            .Include(g => g.Address)
            .Take(1)
            .Select(g => new GasEvent
            {
                price = g.PRICE,
                amount = g.AMOUNT,
                fee = g.FEE,
                address = g.Address != null
                    ? new Address
                    {
                        address = g.Address.ADDRESS,
                        address_name = g.Address.ADDRESS_NAME
                    }
                    : null
            }).FirstOrDefault();
        if ( gasEvent == null)
        {
            return null;
        }

        return gasEvent;
    }


    private static HashEvent CreateHashEvent(MainDbContext mainDbContext, Database.Main.Event e)
    {
        var hashEvent =  mainDbContext.HashEvents.Where(h => h.EventId == e.ID)
            .Take(1)
            .Select(h => new HashEvent
            {
                hash = h.HASH
            }).FirstOrDefault();
        
        if ( hashEvent == null)
        {
            return null;
        }

        return hashEvent;
    }


    private static InfusionEvent CreateInfusionEvent(MainDbContext mainDbContext, Database.Main.Event e)
    {
        var infuseEvent = mainDbContext.InfusionEvents.Where(i => i.EventId == e.ID)
            .Include(i => i.BaseToken)
            .Include(i => i.InfusedToken)
            .Take(1)
            .Select(i => new InfusionEvent
            {
                token_id = i.TOKEN_ID,
                infused_value = i.INFUSED_VALUE,
                infused_value_raw = i.INFUSED_VALUE_RAW,
                base_token = i.BaseToken != null
                    ? new Token
                    {
                        symbol = i.BaseToken.SYMBOL,
                        fungible = i.BaseToken.FUNGIBLE,
                        transferable = i.BaseToken.TRANSFERABLE,
                        finite = i.BaseToken.FINITE,
                        divisible = i.BaseToken.DIVISIBLE,
                        fiat = i.BaseToken.FIAT,
                        fuel = i.BaseToken.FUEL,
                        swappable = i.BaseToken.SWAPPABLE,
                        burnable = i.BaseToken.BURNABLE,
                        stakable = i.BaseToken.STAKABLE,
                        decimals = i.BaseToken.DECIMALS
                    }
                    : null,
                infused_token = i.InfusedToken != null
                    ? new Token
                    {
                        symbol = i.InfusedToken.SYMBOL,
                        fungible = i.InfusedToken.FUNGIBLE,
                        transferable = i.InfusedToken.TRANSFERABLE,
                        finite = i.InfusedToken.FINITE,
                        divisible = i.InfusedToken.DIVISIBLE,
                        fiat = i.InfusedToken.FIAT,
                        fuel = i.InfusedToken.FUEL,
                        swappable = i.InfusedToken.SWAPPABLE,
                        burnable = i.InfusedToken.BURNABLE,
                        stakable = i.InfusedToken.STAKABLE,
                        decimals = i.InfusedToken.DECIMALS
                    }
                    : null
            })
            .FirstOrDefault();
        
        if ( infuseEvent == null)
        {
            return null;
        }

        return infuseEvent;
    }


    private static MarketEvent CreateMarketEvent(MainDbContext mainDbContext, Database.Main.Event e, int with_fiat, string fiatCurrency,
        Dictionary<string, decimal> fiatPricesInUsd)
    {
        var marketEvent = mainDbContext.MarketEvents.Where(m => m.EventId == e.ID)
            .Include(m => m.BaseToken)
            .Include(m => m.QuoteToken)
            .Include(m => m.MarketEventKind)
            .Include(m => m.MarketEventFiatPrice)
            .Take(1)
            .Select(m =>  new MarketEvent
            {

                base_token = e.MarketEvent.BaseToken != null
                    ? new Token
                    {
                        symbol = e.MarketEvent.BaseToken.SYMBOL,
                        fungible = e.MarketEvent.BaseToken.FUNGIBLE,
                        transferable = e.MarketEvent.BaseToken.TRANSFERABLE,
                        finite = e.MarketEvent.BaseToken.FINITE,
                        divisible = e.MarketEvent.BaseToken.DIVISIBLE,
                        fiat = e.MarketEvent.BaseToken.FIAT,
                        fuel = e.MarketEvent.BaseToken.FUEL,
                        swappable = e.MarketEvent.BaseToken.SWAPPABLE,
                        burnable = e.MarketEvent.BaseToken.BURNABLE,
                        stakable = e.MarketEvent.BaseToken.STAKABLE,
                        decimals = e.MarketEvent.BaseToken.DECIMALS
                    }
                    : null,
                quote_token = e.MarketEvent.QuoteToken != null
                    ? new Token
                    {
                        symbol = e.MarketEvent.QuoteToken.SYMBOL,
                        fungible = e.MarketEvent.QuoteToken.FUNGIBLE,
                        transferable = e.MarketEvent.QuoteToken.TRANSFERABLE,
                        finite = e.MarketEvent.QuoteToken.FINITE,
                        divisible = e.MarketEvent.QuoteToken.DIVISIBLE,
                        fiat = e.MarketEvent.QuoteToken.FIAT,
                        fuel = e.MarketEvent.QuoteToken.FUEL,
                        swappable = e.MarketEvent.QuoteToken.SWAPPABLE,
                        burnable = e.MarketEvent.QuoteToken.BURNABLE,
                        stakable = e.MarketEvent.QuoteToken.STAKABLE,
                        decimals = e.MarketEvent.QuoteToken.DECIMALS
                    }
                    : null,
                end_price = e.MarketEvent.END_PRICE,
                price = e.MarketEvent.PRICE,
                market_event_kind = e.MarketEvent.MarketEventKind.NAME,
                market_id = e.MarketEvent.MARKET_ID,
                fiat_price = with_fiat == 1
                    ? new FiatPrice
                    {
                        fiat_currency = e.MarketEvent.MarketEventFiatPrice.FIAT_NAME,
                        fiat_price = FiatExchangeRateMethods.Convert(fiatPricesInUsd,
                            e.MarketEvent.MarketEventFiatPrice.PRICE_USD,
                            e.MarketEvent.MarketEventFiatPrice.FIAT_NAME,
                            fiatCurrency).ToString("0.####"),
                        fiat_price_end = FiatExchangeRateMethods.Convert(fiatPricesInUsd,
                            e.MarketEvent.MarketEventFiatPrice.PRICE_END_USD,
                            e.MarketEvent.MarketEventFiatPrice.FIAT_NAME,
                            fiatCurrency).ToString("0.####")
                    }
                    : null
            })
            .FirstOrDefault();
        if ( marketEvent == null)
        {
            return null;
        }

        return marketEvent;
    }


    private static OrganizationEvent CreateOrganizationEvent(MainDbContext mainDbContext, Database.Main.Event e)
    {
        var organizationEvent = mainDbContext.OrganizationEvents.Where(o => o.EventId == e.ID)
            .Include(o => o.Organization)
            .Include(o => o.Address)
            .Take(1)
            .Select(o => new OrganizationEvent
            {
                organization = o.Organization != null
                    ? new Organization
                    {
                        name = o.Organization.NAME
                    }
                    : null,
                address = e.OrganizationEvent.Address != null
                    ? new Address
                    {
                        address = o.Address.ADDRESS,
                        address_name = o.Address.ADDRESS_NAME
                    }
                    : null
            }).FirstOrDefault();
        
        if ( organizationEvent == null)
        {
            return null;
        }

        return organizationEvent;
    }


    private static SaleEvent CreateSaleEvent(MainDbContext mainDbContext, Database.Main.Event e)
    {
        //e = mainDbContext.Events.Find(e.ID);
        var saleEvent = mainDbContext.SaleEvents.Where(s => s.EventId == e.ID)
            .Include(s => s.SaleEventKind)
            .Take(1)
            .Select(s => new SaleEvent
            {
                hash = e.SaleEvent.HASH,
                sale_event_kind = e.SaleEvent.SaleEventKind.NAME
            })
            .FirstOrDefault();
        
        if ( saleEvent == null)
        {
            return null;
        }

        return saleEvent;
    }


    private static StringEvent CreateStringEvent(MainDbContext mainDbContext, Database.Main.Event e)
    {
        //e = mainDbContext.Events.Find(e.ID);
        var stringEvent = mainDbContext.StringEvents.Where(s => s.EventId == e.ID)
            .Take(1)
            .Select(s => new StringEvent
            {
                string_value = e.StringEvent.STRING_VALUE
            })
            .FirstOrDefault();
        
        if ( stringEvent == null)
        {
            return null;
        }

        return stringEvent;
    }


    private static TokenEvent CreateTokenEvent(MainDbContext mainDbContext, Database.Main.Event e)
    {
        var token = mainDbContext.TokenEvents.Where(t => t.EventId == e.ID)
            .Include(t => t.Token)
            .Take(1)
            .Select(t => new TokenEvent
            {
                token = t.Token != null
                    ? new Token
                    {
                        symbol = t.Token.SYMBOL,
                        fungible = t.Token.FUNGIBLE,
                        transferable = t.Token.TRANSFERABLE,
                        finite = t.Token.FINITE,
                        divisible = t.Token.DIVISIBLE,
                        fiat = t.Token.FIAT,
                        fuel = t.Token.FUEL,
                        swappable = t.Token.SWAPPABLE,
                        burnable = t.Token.BURNABLE,
                        stakable = t.Token.STAKABLE,
                        decimals = t.Token.DECIMALS
                    }
                    : null,
                value = t.VALUE,
                value_raw = t.VALUE_RAW,
                chain_name = t.CHAIN_NAME 
            }).FirstOrDefault();
        
        if ( token == null)
        {
            return null;
        }

        return token;
    }


    private static TransactionSettleEvent CreateTransactionSettleEvent(MainDbContext mainDbContext, Database.Main.Event e)
    {
        //e = mainDbContext.Events.Find(e.ID);
        var transactionSettleEvent = mainDbContext.TransactionSettleEvents.Where(t => t.EventId == e.ID)
            .Include(t => t.Platform)
            .Take(1)
            .Select(t => new TransactionSettleEvent
            {
                hash = e.TransactionSettleEvent.HASH,
                platform = e.TransactionSettleEvent.Platform != null
                    ? new Platform
                    {
                        name = e.TransactionSettleEvent.Platform.NAME,
                        chain = e.TransactionSettleEvent.Platform.CHAIN,
                        fuel = e.TransactionSettleEvent.Platform.FUEL
                        //we do not add other information here for now
                    }
                    : null
            }).FirstOrDefault();
        
        if ( transactionSettleEvent == null)
        {
            return null;
        }

        return transactionSettleEvent;
    }

    private static async Task<Transaction> ProcessTransaction(Database.Main.Transaction transaction, int with_script,
        int with_events, int with_event_data, int with_nft, int with_fiat, string fiatCurrency,
        Dictionary<string, decimal> fiatPricesInUsd)
    {
        await using MainDbContext databaseContext = new();
        var tx = new Transaction
        {
            hash = transaction.HASH,
            block_hash = transaction.Block.HASH,
            block_height = transaction.Block.HEIGHT,
            index = transaction.INDEX,
            date = transaction.TIMESTAMP_UNIX_SECONDS.ToString(),
            fee = transaction.FEE,
            fee_raw = transaction.FEE_RAW,
            script_raw = with_script == 1 ? transaction.SCRIPT_RAW : null,
            result = transaction.RESULT,
            payload = transaction.PAYLOAD,
            expiration = transaction.EXPIRATION.ToString(),
            gas_price = transaction.GAS_PRICE,
            gas_price_raw = transaction.GAS_PRICE_RAW,
            gas_limit = transaction.GAS_LIMIT,
            gas_limit_raw = transaction.GAS_LIMIT_RAW,
            state = transaction.State.NAME,
            sender = transaction.Sender != null
                ? new Address
                {
                    address_name = transaction.Sender.ADDRESS_NAME,
                    address = transaction.Sender.ADDRESS
                }
                : null,
            gas_payer = transaction.GasPayer != null
                ? new Address
                {
                    address_name = transaction.GasPayer.ADDRESS_NAME,
                    address = transaction.GasPayer.ADDRESS
                }
                : null,
            gas_target = transaction.GasTarget != null
                ? new Address
                {
                    address_name = transaction.GasTarget.ADDRESS_NAME,
                    address = transaction.GasTarget.ADDRESS
                }
                : null,
            events = Enumerable.Empty<Event>()
                .ToArray() //await HandleEvents(transaction, with_events, with_event_data, with_nft, with_fiat, fiatCurrency,
            //fiatPricesInUsd)
        };

        return tx;
    }


    private static async Task<Event[]> HandleEvents(Database.Main.Transaction transaction, int with_events,
        int with_event_data, int with_nft, int with_fiat, string fiatCurrency,
        Dictionary<string, decimal> fiatPricesInUsd)
    {
        if ( with_events != 1 ) return null;
        if ( transaction.Events == null ) return null;
        if ( transaction.Events.Count == 0 ) return null;
        //if ( transaction.Events.Count > 100 ) throw new ApiParameterException("Too many events in transaction.");

        var tasks = transaction.Events.Select(_transactionEvent => ProcessEvent(_transactionEvent, transaction,
            with_event_data, with_nft, with_fiat, fiatCurrency, fiatPricesInUsd));
        var events = await Task.WhenAll(tasks);
        return events.ToArray();
    }


    private static async Task<Event> ProcessEvent(Database.Main.Event _transactionEvent, Database.Main.Transaction transaction,
        int with_event_data, int with_nft, int with_fiat, string fiatCurrency,
        Dictionary<string, decimal> fiatPricesInUsd)
    {
        Event _event = new Event();
        _event.event_id = _transactionEvent.ID;
        _event.chain = _transactionEvent.Chain.NAME.ToLower();
        _event.date = _transactionEvent.TIMESTAMP_UNIX_SECONDS.ToString();
        _event.transaction_hash = transaction.HASH; //a bit redundant in that case
        _event.token_id = _transactionEvent.TOKEN_ID;
        _event.event_kind = _transactionEvent.EventKind.NAME;
        _event.address = _transactionEvent.Address.ADDRESS;
        _event.address_name = _transactionEvent.Address.ADDRESS_NAME;
        _event.contract = new Contract
        {
            name = _transactionEvent.Contract.NAME,
            hash = _transactionEvent.Contract.HASH,
            symbol = _transactionEvent.Contract.SYMBOL
        };
        _event.nft_metadata = with_nft == 1 && _transactionEvent.Nft != null
            ? new NftMetadata
            {
                name = _transactionEvent.Nft.NAME,
                description = _transactionEvent.Nft.DESCRIPTION,
                image = _transactionEvent.Nft.IMAGE,
                video = _transactionEvent.Nft.VIDEO,
                rom = _transactionEvent.Nft.ROM,
                ram = _transactionEvent.Nft.RAM,
                mint_date = _transactionEvent.Nft.MINT_DATE_UNIX_SECONDS.ToString(),
                mint_number = _transactionEvent.Nft.MINT_NUMBER.ToString(),
            }
            : null;
        _event.series = with_nft == 1 && _transactionEvent.Nft != null && _transactionEvent.Nft.Series != null
            ? new Series
            {
                id = _transactionEvent.Nft.Series.ID,
                series_id = _transactionEvent.Nft.Series.SERIES_ID,
                creator = _transactionEvent.Nft.Series.CreatorAddress != null
                    ? _transactionEvent.Nft.Series.CreatorAddress.ADDRESS
                    : null,
                current_supply = _transactionEvent.Nft.Series.CURRENT_SUPPLY,
                max_supply = _transactionEvent.Nft.Series.MAX_SUPPLY,
                mode_name = _transactionEvent.Nft.Series.SeriesMode != null
                    ? _transactionEvent.Nft.Series.SeriesMode.MODE_NAME
                    : null,
                name = _transactionEvent.Nft.Series.NAME,
                description = _transactionEvent.Nft.Series.DESCRIPTION,
                image = _transactionEvent.Nft.Series.IMAGE,
                royalties = _transactionEvent.Nft.Series.ROYALTIES.ToString(CultureInfo.InvariantCulture),
                type = _transactionEvent.Nft.Series.TYPE,
                attr_type_1 = _transactionEvent.Nft.Series.ATTR_TYPE_1,
                attr_value_1 = _transactionEvent.Nft.Series.ATTR_VALUE_1,
                attr_type_2 = _transactionEvent.Nft.Series.ATTR_TYPE_2,
                attr_value_2 = _transactionEvent.Nft.Series.ATTR_VALUE_2,
                attr_type_3 = _transactionEvent.Nft.Series.ATTR_TYPE_3,
                attr_value_3 = _transactionEvent.Nft.Series.ATTR_VALUE_3
            }
            : null;
        _event.address_event = with_event_data == 1 && _transactionEvent.TargetAddress != null
            ? new AddressEvent
            {
                address = _transactionEvent.TargetAddress != null
                    ? new Address
                    {
                        address_name = _transactionEvent.TargetAddress.ADDRESS_NAME,
                        address = _transactionEvent.TargetAddress.ADDRESS
                    }
                    : null
            }
            : null;
        _event.chain_event = with_event_data == 1 && _transactionEvent.ChainEvent != null
            ? new ChainEvent
            {
                name = _transactionEvent.ChainEvent.NAME,
                value = _transactionEvent.ChainEvent.VALUE,
                chain = _transactionEvent.ChainEvent.Chain != null
                    ? new Chain
                    {
                        chain_name = _transactionEvent.ChainEvent.Chain.NAME
                    }
                    : null
            }
            : null;
        _event.gas_event = with_event_data == 1 && _transactionEvent.GasEvent != null
            ? new GasEvent
            {
                price = _transactionEvent.GasEvent.PRICE,
                amount = _transactionEvent.GasEvent.AMOUNT,
                fee = _transactionEvent.GasEvent.FEE,
                address = _transactionEvent.GasEvent.Address != null
                    ? new Address
                    {
                        address = _transactionEvent.GasEvent.Address.ADDRESS,
                        address_name = _transactionEvent.GasEvent.Address.ADDRESS_NAME
                    }
                    : null
            }
            : null;
        _event.hash_event = with_event_data == 1 && _transactionEvent.HashEvent != null
            ? new HashEvent
            {
                hash = _transactionEvent.HashEvent.HASH
            }
            : null;
        _event.infusion_event = with_event_data == 1 && _transactionEvent.InfusionEvent != null
            ? new InfusionEvent
            {
                token_id = _transactionEvent.InfusionEvent.TOKEN_ID,
                infused_value = _transactionEvent.InfusionEvent.INFUSED_VALUE,
                infused_value_raw = _transactionEvent.InfusionEvent.INFUSED_VALUE_RAW,
                base_token = _transactionEvent.InfusionEvent.BaseToken != null
                    ? new Token
                    {
                        symbol = _transactionEvent.InfusionEvent.BaseToken.SYMBOL,
                        fungible = _transactionEvent.InfusionEvent.BaseToken.FUNGIBLE,
                        transferable = _transactionEvent.InfusionEvent.BaseToken.TRANSFERABLE,
                        finite = _transactionEvent.InfusionEvent.BaseToken.FINITE,
                        divisible = _transactionEvent.InfusionEvent.BaseToken.DIVISIBLE,
                        fiat = _transactionEvent.InfusionEvent.BaseToken.FIAT,
                        fuel = _transactionEvent.InfusionEvent.BaseToken.FUEL,
                        swappable = _transactionEvent.InfusionEvent.BaseToken.SWAPPABLE,
                        burnable = _transactionEvent.InfusionEvent.BaseToken.BURNABLE,
                        stakable = _transactionEvent.InfusionEvent.BaseToken.STAKABLE,
                        decimals = _transactionEvent.InfusionEvent.BaseToken.DECIMALS
                    }
                    : null,
                infused_token = _transactionEvent.InfusionEvent.InfusedToken != null
                    ? new Token
                    {
                        symbol = _transactionEvent.InfusionEvent.InfusedToken.SYMBOL,
                        fungible = _transactionEvent.InfusionEvent.InfusedToken.FUNGIBLE,
                        transferable = _transactionEvent.InfusionEvent.InfusedToken.TRANSFERABLE,
                        finite = _transactionEvent.InfusionEvent.InfusedToken.FINITE,
                        divisible = _transactionEvent.InfusionEvent.InfusedToken.DIVISIBLE,
                        fiat = _transactionEvent.InfusionEvent.InfusedToken.FIAT,
                        fuel = _transactionEvent.InfusionEvent.InfusedToken.FUEL,
                        swappable = _transactionEvent.InfusionEvent.InfusedToken.SWAPPABLE,
                        burnable = _transactionEvent.InfusionEvent.InfusedToken.BURNABLE,
                        stakable = _transactionEvent.InfusionEvent.InfusedToken.STAKABLE,
                        decimals = _transactionEvent.InfusionEvent.InfusedToken.DECIMALS
                    }
                    : null
            }
            : null;
        _event.market_event = with_event_data == 1 && _transactionEvent.MarketEvent != null
            ? new MarketEvent
            {
                base_token = _transactionEvent.MarketEvent.BaseToken != null
                    ? new Token
                    {
                        symbol = _transactionEvent.MarketEvent.BaseToken.SYMBOL,
                        fungible = _transactionEvent.MarketEvent.BaseToken.FUNGIBLE,
                        transferable = _transactionEvent.MarketEvent.BaseToken.TRANSFERABLE,
                        finite = _transactionEvent.MarketEvent.BaseToken.FINITE,
                        divisible = _transactionEvent.MarketEvent.BaseToken.DIVISIBLE,
                        fiat = _transactionEvent.MarketEvent.BaseToken.FIAT,
                        fuel = _transactionEvent.MarketEvent.BaseToken.FUEL,
                        swappable = _transactionEvent.MarketEvent.BaseToken.SWAPPABLE,
                        burnable = _transactionEvent.MarketEvent.BaseToken.BURNABLE,
                        stakable = _transactionEvent.MarketEvent.BaseToken.STAKABLE,
                        decimals = _transactionEvent.MarketEvent.BaseToken.DECIMALS
                    }
                    : null,
                quote_token = _transactionEvent.MarketEvent.QuoteToken != null
                    ? new Token
                    {
                        symbol = _transactionEvent.MarketEvent.QuoteToken.SYMBOL,
                        fungible = _transactionEvent.MarketEvent.QuoteToken.FUNGIBLE,
                        transferable = _transactionEvent.MarketEvent.QuoteToken.TRANSFERABLE,
                        finite = _transactionEvent.MarketEvent.QuoteToken.FINITE,
                        divisible = _transactionEvent.MarketEvent.QuoteToken.DIVISIBLE,
                        fiat = _transactionEvent.MarketEvent.QuoteToken.FIAT,
                        fuel = _transactionEvent.MarketEvent.QuoteToken.FUEL,
                        swappable = _transactionEvent.MarketEvent.QuoteToken.SWAPPABLE,
                        burnable = _transactionEvent.MarketEvent.QuoteToken.BURNABLE,
                        stakable = _transactionEvent.MarketEvent.QuoteToken.STAKABLE,
                        decimals = _transactionEvent.MarketEvent.QuoteToken.DECIMALS
                    }
                    : null,
                end_price = _transactionEvent.MarketEvent.END_PRICE,
                price = _transactionEvent.MarketEvent.PRICE,
                market_event_kind = _transactionEvent.MarketEvent.MarketEventKind.NAME,
                market_id = _transactionEvent.MarketEvent.MARKET_ID,
                fiat_price = with_fiat == 1
                    ? new FiatPrice
                    {
                        fiat_currency =
                            _transactionEvent.MarketEvent.MarketEventFiatPrice.FIAT_NAME,
                        fiat_price = FiatExchangeRateMethods.Convert(fiatPricesInUsd,
                            _transactionEvent.MarketEvent.MarketEventFiatPrice.PRICE_USD,
                            _transactionEvent.MarketEvent.MarketEventFiatPrice.FIAT_NAME,
                            fiatCurrency).ToString("0.####"),
                        fiat_price_end = FiatExchangeRateMethods.Convert(fiatPricesInUsd,
                            _transactionEvent.MarketEvent.MarketEventFiatPrice.PRICE_END_USD,
                            _transactionEvent.MarketEvent.MarketEventFiatPrice.FIAT_NAME,
                            fiatCurrency).ToString("0.####")
                    }
                    : null
            }
            : null;
        _event.organization_event = with_event_data == 1 && _transactionEvent.OrganizationEvent != null
            ? new OrganizationEvent
            {
                organization = _transactionEvent.OrganizationEvent.Organization != null
                    ? new Organization
                    {
                        name = _transactionEvent.OrganizationEvent.Organization.NAME
                    }
                    : null,
                address = _transactionEvent.OrganizationEvent.Address != null
                    ? new Address
                    {
                        address = _transactionEvent.OrganizationEvent.Address.ADDRESS,
                        address_name = _transactionEvent.OrganizationEvent.Address.ADDRESS_NAME
                    }
                    : null
            }
            : null;
        _event.sale_event = with_event_data == 1 && _transactionEvent.SaleEvent != null
            ? new SaleEvent
            {
                hash = _transactionEvent.SaleEvent.HASH,
                sale_event_kind = _transactionEvent.SaleEvent.SaleEventKind.NAME
            }
            : null;
        _event.string_event = with_event_data == 1 && _transactionEvent.StringEvent != null
            ? new StringEvent
            {
                string_value = _transactionEvent.StringEvent.STRING_VALUE
            }
            : null;
        _event.token_event = with_event_data == 1 && _transactionEvent.TokenEvent != null
            ? new TokenEvent
            {
                token = _transactionEvent.TokenEvent.Token != null
                    ? new Token
                    {
                        symbol = _transactionEvent.TokenEvent.Token.SYMBOL,
                        fungible = _transactionEvent.TokenEvent.Token.FUNGIBLE,
                        transferable = _transactionEvent.TokenEvent.Token.TRANSFERABLE,
                        finite = _transactionEvent.TokenEvent.Token.FINITE,
                        divisible = _transactionEvent.TokenEvent.Token.DIVISIBLE,
                        fiat = _transactionEvent.TokenEvent.Token.FIAT,
                        fuel = _transactionEvent.TokenEvent.Token.FUEL,
                        swappable = _transactionEvent.TokenEvent.Token.SWAPPABLE,
                        burnable = _transactionEvent.TokenEvent.Token.BURNABLE,
                        stakable = _transactionEvent.TokenEvent.Token.STAKABLE,
                        decimals = _transactionEvent.TokenEvent.Token.DECIMALS
                    }
                    : null,
                value = _transactionEvent.TokenEvent.VALUE,
                value_raw = _transactionEvent.TokenEvent.VALUE_RAW,
                chain_name = _transactionEvent.TokenEvent.CHAIN_NAME
            }
            : null;
        _event.transaction_settle_event = with_event_data == 1 && _transactionEvent.TransactionSettleEvent != null
            ? new TransactionSettleEvent
            {
                hash = _transactionEvent.TransactionSettleEvent.HASH,
                platform = _transactionEvent.TransactionSettleEvent.Platform != null
                    ? new Platform
                    {
                        name = _transactionEvent.TransactionSettleEvent.Platform.NAME,
                        chain = _transactionEvent.TransactionSettleEvent.Platform.CHAIN,
                        fuel = _transactionEvent.TransactionSettleEvent.Platform.FUEL
                        //we do not add other information here for now
                    }
                    : null
            }
            : null;

        return _event;
    }
}
