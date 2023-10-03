using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Database.Main;
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

        tasksEvents.Add(Task.Run( () => LoadFromChunk(x.Events, x, with_nft, with_event_data, with_fiat, fiatCurrency,
            fiatPricesInUsd)));
        
        var resultsEvents = await Task.WhenAll(tasksEvents);
        
        return resultsEvents.SelectMany(a => a).ToArray();
    }


    private static async Task<Event[]> LoadFromChunk(List<Database.Main.Event> chunk, Database.Main.Transaction x, int with_nft, int with_event_data, int with_fiat, string fiatCurrency, Dictionary<string, decimal> fiatPricesInUsd)
    {
        var tasks = new List<Event>();
        
        foreach (var e in chunk)
        {
            tasks.Add(CreateEventWihoutTask(x, e, with_nft, with_event_data, with_fiat, fiatCurrency,
                fiatPricesInUsd));
        }

        return tasks.ToArray();
    }
    
    private static Event CreateEventWihoutTask(Database.Main.Transaction x, Database.Main.Event e, int with_nft, int with_event_data, int with_fiat,
        string fiatCurrency, Dictionary<string, decimal> fiatPricesInUsd)
    {
        return new Event
        {
            event_id = e.ID,
            chain = e.Chain.NAME.ToLower(),
            date = e.TIMESTAMP_UNIX_SECONDS.ToString(),
            transaction_hash = x.HASH,
            token_id = e.TOKEN_ID,
            event_kind = e.EventKind.NAME,
            address = e.Address.ADDRESS,
            address_name = e.Address.ADDRESS_NAME,
            contract = e.Contract != null ? new Contract
            {
                name = e.Contract.NAME,
                hash = e.Contract.HASH,
                symbol = e.Contract.SYMBOL
            } : null,
            nft_metadata = with_nft == 1 && e.Nft != null ?
                new NftMetadata
                {
                    name = e.Nft.NAME,
                    description = e.Nft.DESCRIPTION,
                    image = e.Nft.IMAGE,
                    video = e.Nft.VIDEO,
                    rom = e.Nft.ROM,
                    ram = e.Nft.RAM,
                    mint_date = e.Nft.MINT_DATE_UNIX_SECONDS.ToString(),
                    mint_number = e.Nft.MINT_NUMBER.ToString()
                } : null,
            series = with_nft == 1 && e.Nft != null && e.Nft.Series != null ?
                new Series
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
                } : null,
            address_event = with_event_data == 1 && e.TargetAddress != null ?
                new AddressEvent
                {
                    address = new Address
                    {
                        address_name = e.TargetAddress.ADDRESS_NAME,
                        address = e.TargetAddress.ADDRESS
                    }
                } : null,
            chain_event = with_event_data == 1 && e.ChainEvent != null ?
                new ChainEvent
                {
                    name = e.ChainEvent.NAME,
                    value = e.ChainEvent.VALUE,
                    chain = e.ChainEvent.Chain != null
                        ? new Chain
                        {
                            chain_name = e.Chain.NAME.ToLower()
                        }
                        : null
                } : null,
            gas_event = with_event_data == 1 && e.GasEvent != null ?
                new GasEvent
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
                } : null,
            hash_event = with_event_data == 1 && e.HashEvent != null ?
                new HashEvent
                {
                    hash = e.HashEvent.HASH
                } : null,
            infusion_event = with_event_data == 1 && e.InfusionEvent != null ?
                new InfusionEvent
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
                } : null,
            market_event = with_event_data == 1 && e.MarketEvent != null ?
                new MarketEvent
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
                        } : null
                } : null,
            organization_event = with_event_data == 1 && e.OrganizationEvent != null ?
                new OrganizationEvent
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
                } : null,
            sale_event = with_event_data == 1 && e.SaleEvent != null ?
                new SaleEvent
                {
                    hash = e.SaleEvent.HASH,
                    sale_event_kind = e.SaleEvent.SaleEventKind.NAME
                } : null,
            string_event = with_event_data == 1 && e.StringEvent != null ?
                new StringEvent
                {
                    string_value = e.StringEvent.STRING_VALUE
                } : null,
            token_event = with_event_data == 1 && e.TokenEvent != null ? 
                new TokenEvent
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
                } : null,
            transaction_settle_event = with_event_data == 1 && e.TransactionSettleEvent != null  ? 
                new TransactionSettleEvent
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
                } : null
        };
    }
}
