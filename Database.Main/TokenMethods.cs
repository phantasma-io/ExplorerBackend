using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Commons;
using Microsoft.EntityFrameworkCore;

namespace Database.Main;

public static class TokenMethods
{
    private static Token kcalToken;


    private static Token soulToken;
    // Checks if "Token" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.


    public static async Task<Token> UpsertAsync(MainDbContext databaseContext, Chain chain, string contractHash, string name, string symbol,
        int decimals, bool fungible, bool transferable, bool finite, bool divisible, bool fuel, bool stakable,
        bool fiat, bool swappable, bool burnable, bool mintable, string address, string owner, string currentSupply,
        string maxSupply,
        string burnedSupply, string scriptRaw)
    {
        var contractEntry = await ContractMethods.UpsertAsync(databaseContext, symbol, chain, contractHash, symbol);

        var addressEntry = await AddressMethods.UpsertAsync(databaseContext, chain, address);
        var ownerEntry = await AddressMethods.UpsertAsync(databaseContext, chain, owner);


        var entry = await GetAsync(databaseContext, chain, symbol);


        if ( entry != null )
        {
            entry.NAME = name;
            entry.DECIMALS = decimals;
            entry.FUNGIBLE = fungible;
            entry.TRANSFERABLE = transferable;
            entry.FINITE = finite;
            entry.DIVISIBLE = divisible;
            entry.FUEL = fuel;
            entry.STAKABLE = stakable;
            entry.FIAT = fiat;
            entry.SWAPPABLE = swappable;
            entry.BURNABLE = burnable;
            entry.MINTABLE = mintable;
            entry.Address = addressEntry;
            entry.Owner = ownerEntry;
            entry.SCRIPT_RAW = scriptRaw;
        }
        else
        {
            entry = new Token
            {
                Chain = chain,
                Contract = contractEntry,
                NAME = name,
                SYMBOL = symbol,
                DECIMALS = decimals,
                FUNGIBLE = fungible,
                TRANSFERABLE = transferable,
                FINITE = finite,
                DIVISIBLE = divisible,
                FUEL = fuel,
                STAKABLE = stakable,
                FIAT = fiat,
                SWAPPABLE = swappable,
                BURNABLE = burnable,
                MINTABLE = mintable,
                Address = addressEntry,
                Owner = ownerEntry,
                SCRIPT_RAW = scriptRaw
            };
            await databaseContext.Tokens.AddAsync(entry);
        }

        SetSupplies(entry, currentSupply, maxSupply, burnedSupply);

        return entry;
    }

    public static void SetSupplies(Token token,
        string currentSupply,
        string maxSupply,
        string burnedSupply)
    {
        if ( token == null )
        {
            return;
        }

        token.CURRENT_SUPPLY = Utils.ToDecimal(currentSupply, token.DECIMALS);
        token.CURRENT_SUPPLY_RAW = currentSupply;
        token.MAX_SUPPLY = Utils.ToDecimal(maxSupply, token.DECIMALS);
        token.MAX_SUPPLY_RAW = maxSupply;
        token.BURNED_SUPPLY = Utils.ToDecimal(burnedSupply, token.DECIMALS);
        token.BURNED_SUPPLY_RAW = burnedSupply;
    }

    public static Token Get(MainDbContext databaseContext, int chainId, string symbol)
    {
        return databaseContext.Tokens.SingleOrDefault(x => x.ChainId == chainId && x.SYMBOL == symbol);
    }

    // Returns all supported token symbols <chainShortName, tokenSymbol>.
    public static List<Symbol> GetSupportedTokens(MainDbContext databaseContext)
    {
        var tokens = databaseContext.Tokens.ToList();

        return tokens.Select(token => new Symbol {ChainName = token.Chain.NAME, NativeSymbol = token.SYMBOL}).ToList();
    }


    // Returns all fiat currency symbols currently supported by frontend.
    public static List<string> GetSupportedFiatSymbols()
    {
        return new List<string>
        {
            "AUD",
            "CAD",
            "CNY",
            "EUR",
            "GBP",
            "JPY",
            "RUB",
            "USD"
        };
    }


    // Sets price for token in given fiat currency.
    public static void SetPrice(MainDbContext databaseContext, int chainId, string symbol, string fiatPairSymbol,
        decimal price, bool saveChanges = true)
    {
        var entry = databaseContext.Tokens
            .FirstOrDefault(x => x.ChainId == chainId && x.SYMBOL == symbol);

        if ( entry == null )
            // Token with this symbol is not found, cannot set the price - normal situation, not an error.
            return;

        switch ( fiatPairSymbol.ToUpper() )
        {
            case "AUD":
                entry.PRICE_AUD = price;
                break;
            case "CAD":
                entry.PRICE_CAD = price;
                break;
            case "CNY":
                entry.PRICE_CNY = price;
                break;
            case "EUR":
                entry.PRICE_EUR = price;
                break;
            case "GBP":
                entry.PRICE_GBP = price;
                break;
            case "JPY":
                entry.PRICE_JPY = price;
                break;
            case "RUB":
                entry.PRICE_RUB = price;
                break;
            case "USD":
                entry.PRICE_USD = price;
                break;
            default:
                throw new Exception($"Unknown FIAT currency with symbol '{fiatPairSymbol}', cannot set the price");
        }

        if ( saveChanges ) databaseContext.SaveChanges();
    }


    // Gets token prices dictionary for given fiat currency.
    // Dictionary key contains crypto token symbol, and value contains price in fiat currency.
    public static TokenPrice[] GetPrices(MainDbContext efDatabaseContext, string fiatSymbol)
    {
        return efDatabaseContext.Tokens.Select(x => new TokenPrice
        {
            ChainId = x.ChainId,
            Symbol = x.SYMBOL,
            Price = fiatSymbol.ToUpper() == "AUD" ? x.PRICE_AUD :
                fiatSymbol.ToUpper() == "CAD" ? x.PRICE_CAD :
                fiatSymbol.ToUpper() == "CNY" ? x.PRICE_CNY :
                fiatSymbol.ToUpper() == "EUR" ? x.PRICE_EUR :
                fiatSymbol.ToUpper() == "GBP" ? x.PRICE_GBP :
                fiatSymbol.ToUpper() == "JPY" ? x.PRICE_JPY :
                fiatSymbol.ToUpper() == "RUB" ? x.PRICE_RUB :
                fiatSymbol.ToUpper() == "USD" ? x.PRICE_USD : 0
        }).ToArray();
    }


    public static decimal ToDecimal(string priceInTokens, string tokenSymbol)
    {
        if ( !double.TryParse(priceInTokens, out var priceInTokensDouble) )
            throw new Exception($"{priceInTokens} price cannot be parced.");

        //TODO
        switch ( tokenSymbol.ToUpper() )
        {
            case "SOUL":
                priceInTokensDouble /= Math.Pow(10, 8);
                break;
            case "KCAL":
                priceInTokensDouble /= Math.Pow(10, 10);
                break;
            case "GOATI":
                priceInTokensDouble /= Math.Pow(10, 3);
                break;
            case "GAS":
                priceInTokensDouble /= Math.Pow(10, 8);
                break;
            case "BNEO":
                priceInTokensDouble /= Math.Pow(10, 8);
                break;
            case "ETH":
                priceInTokensDouble /= Math.Pow(10, 18);
                break;
            case "WETH":
                priceInTokensDouble /= Math.Pow(10, 18);
                break;
            case "BNB":
                priceInTokensDouble /= Math.Pow(10, 18);
                break;
            case "WBNB":
                priceInTokensDouble /= Math.Pow(10, 18);
                break;
            case "BUSD":
                priceInTokensDouble /= Math.Pow(10, 18);
                break;
            case "SWTH":
                priceInTokensDouble /= Math.Pow(10, 8);
                break;
            case "CAKE":
                priceInTokensDouble /= Math.Pow(10, 18);
                break;
            case "USDC":
                priceInTokensDouble /= Math.Pow(10, 6);
                break;
            case "DAI":
                priceInTokensDouble /= Math.Pow(10, 18);
                break;
            case "MATIC":
                priceInTokensDouble /= Math.Pow(10, 18);
                break;
            case "DYT":
                priceInTokensDouble /= Math.Pow(10, 18);
                break;
            case "AVAX":
                priceInTokensDouble /= Math.Pow(10, 18);
                break;
            case "WAVAX":
                priceInTokensDouble /= Math.Pow(10, 18);
                break;
        }

        return ( decimal ) priceInTokensDouble;
    }


    // Returns calculated price for given price in tokens, using token prices dictionary.
    public static double CalculatePrice(IEnumerable<TokenPrice> prices, string priceInTokens, string tokenSymbol)
    {
        if ( string.IsNullOrEmpty(priceInTokens) || string.IsNullOrEmpty(tokenSymbol) ) return 0;

        // Applying decimal points.
        var priceInTokensDecimal = ToDecimal(priceInTokens, tokenSymbol);

        var tokenPrice = prices.Where(x => x.Symbol == tokenSymbol).Select(x => x.Price).FirstOrDefault();

        return ( double ) ( tokenPrice * priceInTokensDecimal );
    }

    public static Token Get(MainDbContext databaseContext, int id)
    {
        return databaseContext.Tokens.SingleOrDefault(x => x.ID == id);
    }


    public static Task<Token> GetAsync(MainDbContext databaseContext, Chain chain, string symbol)
    {
        return databaseContext.Tokens.SingleOrDefaultAsync(x => x.Chain == chain && x.SYMBOL == symbol);
    }


    public static void SetPrice(MainDbContext databaseContext, Token token, string fiatPairSymbol, decimal price,
        bool saveChanges = true)
    {
        switch ( fiatPairSymbol.ToUpper() )
        {
            case "AUD":
                token.PRICE_AUD = price;
                break;
            case "CAD":
                token.PRICE_CAD = price;
                break;
            case "CNY":
                token.PRICE_CNY = price;
                break;
            case "EUR":
                token.PRICE_EUR = price;
                break;
            case "GBP":
                token.PRICE_GBP = price;
                break;
            case "JPY":
                token.PRICE_JPY = price;
                break;
            case "RUB":
                token.PRICE_RUB = price;
                break;
            case "USD":
                token.PRICE_USD = price;
                break;
            default:
                throw new Exception($"Unknown FIAT currency with symbol '{fiatPairSymbol}', cannot set the price");
        }

        if ( saveChanges ) databaseContext.SaveChanges();
    }


    public static IEnumerable<Token> GetTokensWithoutLogo(MainDbContext databaseContext)
    {
        return databaseContext.Tokens.Where(x => !x.TokenLogos.Any()).ToList();
    }


    public static int GetKcalDecimals(MainDbContext databaseContext, Chain chain)
    {
        // TODO async
        kcalToken ??= GetAsync(databaseContext, chain, "KCAL").Result;
        return kcalToken.DECIMALS;
    }


    public static int GetSoulDecimals(MainDbContext databaseContext, Chain chain)
    {
        // TODO async
        soulToken ??= GetAsync(databaseContext, chain, "SOUL").Result;
        return soulToken.DECIMALS;
    }


    // Used in methods below as an argument. Stores symbol information.
    public class Symbol
    {
        public string
            ApiSymbol; // Token symbol, used in external methods to store API-specific symbols, ex. "phantasma" instead of "SOUL" for CoinGecko.

        public string ChainName; // Short name of the chain symbol belongs to, ex. "main".
        public string NativeSymbol; // Token symbol, ex. "SOUL".
    }

    public class TokenPrice
    {
        public int ChainId;
        public decimal Price;
        public string Symbol; // Token symbol, ex. "SOUL".
    }
}
