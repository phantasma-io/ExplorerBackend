using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.EntityFrameworkCore;
using CommonsUtils = Backend.Commons.Utils;
using DbAddress = Database.Main.Address;
using DbChain = Database.Main.Chain;
using DbPlatform = Database.Main.Platform;
using DbToken = Database.Main.Token;

namespace Backend.Service.Api;

internal static class EventPayloadMapper
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string UnlimitedGasRaw = "18446744073709551615"; // TxMsg.NoMaxGas sentinel

    internal sealed class EventProjection
    {
        public Event ApiEvent { get; init; }
        public int ChainId { get; init; }
        public long TimestampUnixSeconds { get; init; }
        public string PayloadJson { get; init; }
        public string RawData { get; init; }
        public JsonDocument NftMetadata { get; init; }
        public JsonDocument SeriesMetadata { get; init; }
        public string NftCreator { get; init; }
    }

    internal sealed class TransactionProjection
    {
        public Transaction ApiTransaction { get; init; }
        public int TransactionId { get; init; }
        public int ChainId { get; init; }
        public EventProjection[] EventProjections { get; init; } = Array.Empty<EventProjection>();
    }

    private record struct ChainSymbolKey(int ChainId, string Symbol);

    private record struct ChainAddressKey(int ChainId, string Address);

    private sealed class EventPayloadContext
    {
        private readonly Dictionary<ChainSymbolKey, DbToken> _tokens;
        private readonly Dictionary<ChainAddressKey, DbAddress> _addresses;
        private readonly Dictionary<string, DbPlatform> _platforms;
        private readonly Dictionary<int, DbChain> _chains;

        public EventPayloadContext(
            IEnumerable<DbToken> tokens,
            IEnumerable<DbAddress> addresses,
            IEnumerable<DbPlatform> platforms,
            IEnumerable<DbChain> chains,
            TokenMethods.TokenPrice[] tokenPrices)
        {
            _tokens = tokens.ToDictionary(x => new ChainSymbolKey(x.ChainId, x.SYMBOL));
            _addresses = addresses.ToDictionary(x => new ChainAddressKey(x.ChainId, x.ADDRESS));
            _platforms = platforms.ToDictionary(x => x.NAME, StringComparer.OrdinalIgnoreCase);
            _chains = chains.ToDictionary(x => x.ID);
            TokenPrices = tokenPrices;
        }

        public TokenMethods.TokenPrice[] TokenPrices { get; }

        public DbToken GetToken(int chainId, string symbol)
        {
            return string.IsNullOrEmpty(symbol)
                ? null
                : _tokens.GetValueOrDefault(new ChainSymbolKey(chainId, symbol));
        }

        public DbAddress GetAddress(int chainId, string address)
        {
            return string.IsNullOrEmpty(address)
                ? null
                : _addresses.GetValueOrDefault(new ChainAddressKey(chainId, address));
        }

        public DbPlatform GetPlatform(string platform)
        {
            return string.IsNullOrEmpty(platform) ? null : _platforms.GetValueOrDefault(platform);
        }

        public DbChain GetChain(int chainId)
        {
            return _chains.GetValueOrDefault(chainId);
        }

        public static async Task<EventPayloadContext> BuildAsync(MainDbContext databaseContext,
            IReadOnlyCollection<EventPayloadEnvelope> events, bool withFiat)
        {
            var chainIds = new HashSet<int>();
            var tokenKeys = new HashSet<ChainSymbolKey>();
            var addressKeys = new HashSet<ChainAddressKey>();
            var platformNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach ( var envelope in events )
            {
                chainIds.Add(envelope.Projection.ChainId);
                var payload = envelope.Payload;
                if ( payload == null ) continue;

                if ( !string.IsNullOrEmpty(payload.TokenEvent?.Token) )
                    tokenKeys.Add(new ChainSymbolKey(envelope.Projection.ChainId, payload.TokenEvent.Token));

                if ( !string.IsNullOrEmpty(payload.TokenSeriesEvent?.Token) )
                    tokenKeys.Add(new ChainSymbolKey(envelope.Projection.ChainId, payload.TokenSeriesEvent.Token));

                if ( !string.IsNullOrEmpty(payload.InfusionEvent?.BaseToken) )
                    tokenKeys.Add(new ChainSymbolKey(envelope.Projection.ChainId, payload.InfusionEvent.BaseToken));

                if ( !string.IsNullOrEmpty(payload.InfusionEvent?.InfusedToken) )
                    tokenKeys.Add(new ChainSymbolKey(envelope.Projection.ChainId, payload.InfusionEvent.InfusedToken));

                if ( !string.IsNullOrEmpty(payload.MarketEvent?.BaseToken) )
                    tokenKeys.Add(new ChainSymbolKey(envelope.Projection.ChainId, payload.MarketEvent.BaseToken));

                if ( !string.IsNullOrEmpty(payload.MarketEvent?.QuoteToken) )
                    tokenKeys.Add(new ChainSymbolKey(envelope.Projection.ChainId, payload.MarketEvent.QuoteToken));

                if ( !string.IsNullOrEmpty(payload.GasEvent?.Address) )
                {
                    addressKeys.Add(new ChainAddressKey(envelope.Projection.ChainId, payload.GasEvent.Address));
                }
                if ( payload.GasEvent != null )
                    tokenKeys.Add(new ChainSymbolKey(envelope.Projection.ChainId, "KCAL"));

                if ( !string.IsNullOrEmpty(payload.TokenSeriesEvent?.Owner) )
                    addressKeys.Add(new ChainAddressKey(envelope.Projection.ChainId, payload.TokenSeriesEvent.Owner));

                if ( !string.IsNullOrEmpty(payload.AddressEvent?.Address) )
                    addressKeys.Add(new ChainAddressKey(envelope.Projection.ChainId, payload.AddressEvent.Address));

                if ( !string.IsNullOrEmpty(payload.OrganizationEvent?.Address) )
                    addressKeys.Add(new ChainAddressKey(envelope.Projection.ChainId, payload.OrganizationEvent.Address));

                if ( !string.IsNullOrEmpty(payload.TokenCreateEvent?.Symbol) )
                    tokenKeys.Add(new ChainSymbolKey(envelope.Projection.ChainId, payload.TokenCreateEvent.Symbol));

                if ( !string.IsNullOrEmpty(payload.TransactionSettleEvent?.Platform) )
                    platformNames.Add(payload.TransactionSettleEvent.Platform);
            }

            var chainIdList = chainIds.ToList();
            var symbolList = tokenKeys.Select(x => x.Symbol).Distinct().ToList();
            var addressList = addressKeys.Select(x => x.Address).Distinct().ToList();

            var tokens = tokenKeys.Count == 0
                ? new List<DbToken>()
                : await databaseContext.Tokens
                    .Where(t => chainIds.Contains(t.ChainId) && symbolList.Contains(t.SYMBOL))
                    .ToListAsync();

            var addresses = addressKeys.Count == 0
                ? new List<DbAddress>()
                : await databaseContext.Addresses
                    .Where(a => chainIds.Contains(a.ChainId) && addressList.Contains(a.ADDRESS))
                    .ToListAsync();

            var platforms = platformNames.Count == 0
                ? new List<DbPlatform>()
                : await databaseContext.Platforms.Where(p => platformNames.Contains(p.NAME)).ToListAsync();

            var chains = chainIdList.Count == 0
                ? new List<DbChain>()
                : await databaseContext.Chains.Where(c => chainIdList.Contains(c.ID)).ToListAsync();

            var tokenPrices = withFiat ? TokenMethods.GetPrices(databaseContext, "USD") : Array.Empty<TokenMethods.TokenPrice>();

            return new EventPayloadContext(tokens, addresses, platforms, chains, tokenPrices);
        }
    }

    private sealed class EventPayloadEnvelope
    {
        public EventProjection Projection { get; init; }
        public EventPayload Payload { get; init; }
    }

    internal static StringEvent ParseStringEvent(string payloadJson)
    {
        return BuildStringEvent(ParsePayload(payloadJson));
    }

    private sealed class EventPayload
    {
        [JsonPropertyName("event_kind")]
        public string EventKind { get; set; }

        [JsonPropertyName("chain")]
        public string Chain { get; set; }

        [JsonPropertyName("contract")]
        public string Contract { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("token_id")]
        public string TokenId { get; set; }

        [JsonPropertyName("address_event")]
        public AddressEventPayload AddressEvent { get; set; }

        [JsonPropertyName("chain_event")]
        public ChainEventPayload ChainEvent { get; set; }

        [JsonPropertyName("gas_event")]
        public GasEventPayload GasEvent { get; set; }

        [JsonPropertyName("governance_gas_config_event")]
        public Dictionary<string, JsonElement> GovernanceGasConfigEvent { get; set; }

        [JsonPropertyName("governance_chain_config_event")]
        public Dictionary<string, JsonElement> GovernanceChainConfigEvent { get; set; }

        [JsonPropertyName("hash_event")]
        public HashEventPayload HashEvent { get; set; }

        [JsonPropertyName("infusion_event")]
        public InfusionEventPayload InfusionEvent { get; set; }

        [JsonPropertyName("market_event")]
        public MarketEventPayload MarketEvent { get; set; }

        [JsonPropertyName("organization_event")]
        public OrganizationEventPayload OrganizationEvent { get; set; }

        [JsonPropertyName("sale_event")]
        public SaleEventPayload SaleEvent { get; set; }

        [JsonPropertyName("string_event")]
        public StringEventPayload StringEvent { get; set; }

        [JsonPropertyName("token_event")]
        public TokenEventPayload TokenEvent { get; set; }

        [JsonPropertyName("token_create_event")]
        public TokenCreateEventPayload TokenCreateEvent { get; set; }

        // Legacy key used in stored payloads.
        [JsonPropertyName("token_create")]
        public TokenCreateEventPayload TokenCreateEventLegacy
        {
            get => TokenCreateEvent;
            set => TokenCreateEvent = value;
        }

        [JsonPropertyName("token_series_event")]
        public TokenSeriesEventPayload TokenSeriesEvent { get; set; }

        [JsonPropertyName("transaction_settle_event")]
        public TransactionSettleEventPayload TransactionSettleEvent { get; set; }
    }

    private sealed class AddressEventPayload
    {
        [JsonPropertyName("address")]
        public string Address { get; set; }
    }

    private sealed class ChainEventPayload
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("chain")]
        public string Chain { get; set; }
    }

    private sealed class GasEventPayload
    {
        [JsonPropertyName("price")]
        public string Price { get; set; }

        [JsonPropertyName("amount")]
        public string Amount { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }
    }

    private sealed class HashEventPayload
    {
        [JsonPropertyName("hash")]
        public string Hash { get; set; }
    }

    private sealed class InfusionEventPayload
    {
        [JsonPropertyName("token_id")]
        public string TokenId { get; set; }

        [JsonPropertyName("base_token")]
        public string BaseToken { get; set; }

        [JsonPropertyName("infused_token")]
        public string InfusedToken { get; set; }

        [JsonPropertyName("infused_value")]
        public string InfusedValue { get; set; }

        [JsonPropertyName("infused_value_raw")]
        public string InfusedValueRaw { get; set; }
    }

    private sealed class MarketEventPayload
    {
        [JsonPropertyName("base_token")]
        public string BaseToken { get; set; }

        [JsonPropertyName("quote_token")]
        public string QuoteToken { get; set; }

        [JsonPropertyName("market_event_kind")]
        public string MarketEventKind { get; set; }

        [JsonPropertyName("market_id")]
        public string MarketId { get; set; }

        [JsonPropertyName("price")]
        public string Price { get; set; }

        [JsonPropertyName("end_price")]
        public string EndPrice { get; set; }
    }

    private sealed class OrganizationEventPayload
    {
        [JsonPropertyName("organization")]
        public string Organization { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }
    }

    private sealed class SaleEventPayload
    {
        [JsonPropertyName("hash")]
        public string Hash { get; set; }

        [JsonPropertyName("sale_event_kind")]
        public string SaleEventKind { get; set; }
    }

    private sealed class StringEventPayload
    {
        [JsonPropertyName("string_value")]
        public string StringValue { get; set; }
    }

    private sealed class TokenEventPayload
    {
        [JsonPropertyName("token")]
        public string Token { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("value_raw")]
        public string ValueRaw { get; set; }

        [JsonPropertyName("chain_name")]
        public string ChainName { get; set; }
    }

    private sealed class TokenCreateEventPayload
    {
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; }

        [JsonPropertyName("max_supply")]
        public string MaxSupply { get; set; }

        [JsonPropertyName("decimals")]
        public string Decimals { get; set; }

        [JsonPropertyName("is_non_fungible")]
        public bool? IsNonFungible { get; set; }

        [JsonPropertyName("carbon_token_id")]
        public string CarbonTokenId { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, string> Metadata { get; set; }
    }

    private sealed class TokenSeriesEventPayload
    {
        [JsonPropertyName("token")]
        public string Token { get; set; }

        [JsonPropertyName("series_id")]
        public string SeriesId { get; set; }

        [JsonPropertyName("max_mint")]
        public string MaxMint { get; set; }

        [JsonPropertyName("max_supply")]
        public string MaxSupply { get; set; }

        [JsonPropertyName("owner")]
        public string Owner { get; set; }

        [JsonPropertyName("carbon_token_id")]
        public string CarbonTokenId { get; set; }

        [JsonPropertyName("carbon_series_id")]
        public string CarbonSeriesId { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, string> Metadata { get; set; }
    }

    private sealed class TransactionSettleEventPayload
    {
        [JsonPropertyName("hash")]
        public string Hash { get; set; }

        [JsonPropertyName("platform")]
        public string Platform { get; set; }

        [JsonPropertyName("chain")]
        public string Chain { get; set; }
    }

    internal static async Task ApplyAsync(MainDbContext databaseContext,
        IReadOnlyCollection<EventProjection> projections,
        bool withEventData,
        bool withFiat,
        string fiatCurrency,
        Dictionary<string, decimal> fiatPricesInUsd)
    {
        if ( !withEventData || projections == null || projections.Count == 0 )
        {
            return;
        }

        var envelopes = projections.Select(p => new EventPayloadEnvelope
            {
                Projection = p,
                Payload = ParsePayload(p.PayloadJson)
            })
            .ToArray();

        var context = await EventPayloadContext.BuildAsync(databaseContext, envelopes, withFiat);

        foreach ( var envelope in envelopes )
        {
            var payload = envelope.Payload;
            var apiEvent = envelope.Projection.ApiEvent;
            var eventKind = apiEvent.event_kind;

            if ( payload == null ) continue;

            apiEvent.address_event = BuildAddressEvent(payload, context, envelope.Projection.ChainId);
            apiEvent.chain_event = BuildChainEvent(payload);
            apiEvent.gas_event = BuildGasEvent(payload, context, envelope.Projection.ChainId);
            apiEvent.governance_gas_config_event =
                string.Equals(eventKind, "GovernanceSetGasConfig", StringComparison.OrdinalIgnoreCase)
                    ? BuildGovernanceGasConfigEvent(payload)
                    : null;
            apiEvent.governance_chain_config_event =
                string.Equals(eventKind, "GovernanceSetChainConfig", StringComparison.OrdinalIgnoreCase)
                    ? BuildGovernanceChainConfigEvent(payload)
                    : null;
            apiEvent.hash_event = BuildHashEvent(payload);
            apiEvent.infusion_event = BuildInfusionEvent(payload, context, envelope.Projection.ChainId);
            apiEvent.market_event = await BuildMarketEventAsync(databaseContext, payload, context,
                envelope.Projection.TimestampUnixSeconds, envelope.Projection.ChainId, withFiat, fiatCurrency,
                fiatPricesInUsd);
            apiEvent.organization_event = BuildOrganizationEvent(payload, context, envelope.Projection.ChainId);
            apiEvent.sale_event = BuildSaleEvent(payload);
            apiEvent.string_event = BuildStringEvent(payload);
            apiEvent.token_event = BuildTokenEvent(payload, context, envelope.Projection.ChainId);
            apiEvent.token_create_event = BuildTokenCreateEvent(payload, context, envelope.Projection.ChainId);
            apiEvent.token_series_event = BuildTokenSeriesEvent(payload, context, envelope.Projection.ChainId);
            apiEvent.transaction_settle_event = BuildTransactionSettleEvent(payload, context);

            var hasSpecific =
                apiEvent.address_event != null ||
                apiEvent.chain_event != null ||
                apiEvent.gas_event != null ||
                apiEvent.governance_gas_config_event != null ||
                apiEvent.governance_chain_config_event != null ||
                apiEvent.hash_event != null ||
                apiEvent.infusion_event != null ||
                apiEvent.market_event != null ||
                apiEvent.organization_event != null ||
                apiEvent.sale_event != null ||
                apiEvent.string_event != null ||
                apiEvent.token_event != null ||
                apiEvent.token_create_event != null ||
                apiEvent.token_series_event != null ||
                apiEvent.transaction_settle_event != null;

            if ( !hasSpecific || string.Equals(eventKind, "ContractDeploy", StringComparison.OrdinalIgnoreCase) )
            {
                apiEvent.unknown_event = new UnknownEvent
                {
                    payload_json = envelope.Projection.PayloadJson,
                    raw_data = envelope.Projection.RawData
                };
            }
        }
    }

    private static EventPayload ParsePayload(string payloadJson)
    {
        if ( string.IsNullOrWhiteSpace(payloadJson) )
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<EventPayload>(payloadJson, PayloadJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractGovernanceValue(Dictionary<string, JsonElement> payload, params string[] keys)
    {
        if ( payload == null )
        {
            return null;
        }

        foreach ( var key in keys )
        {
            if ( !payload.TryGetValue(key, out var element) ) continue;
            if ( element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined )
                return null;
            if ( element.ValueKind == JsonValueKind.String )
                return element.GetString();

            return element.ToString();
        }

        return null;
    }

    private static string ApplyDecimals(string raw, int decimals)
    {
        if ( string.IsNullOrWhiteSpace(raw) )
        {
            return raw;
        }

        return CommonsUtils.ToDecimal(raw, decimals);
    }

    private static AddressEvent BuildAddressEvent(EventPayload payload, EventPayloadContext context, int chainId)
    {
        if ( payload.AddressEvent == null || string.IsNullOrEmpty(payload.AddressEvent.Address) )
            return null;

        var address = context.GetAddress(chainId, payload.AddressEvent.Address);
        return new AddressEvent
        {
            address = new Address
            {
                address = payload.AddressEvent.Address,
                address_name = address?.ADDRESS_NAME
            }
        };
    }

    private static ChainEvent BuildChainEvent(EventPayload payload)
    {
        if ( payload.ChainEvent == null ) return null;

        return new ChainEvent
        {
            name = payload.ChainEvent.Name,
            value = payload.ChainEvent.Value,
            chain = string.IsNullOrEmpty(payload.ChainEvent.Chain)
                ? null
                : new Chain
                {
                    chain_name = payload.ChainEvent.Chain
                }
        };
    }

    private static GasEvent BuildGasEvent(EventPayload payload, EventPayloadContext context, int chainId)
    {
        if ( payload.GasEvent == null ) return null;

        var fee = CalculateGasFee(payload.GasEvent.Price, payload.GasEvent.Amount, context, chainId);
        var address = context.GetAddress(chainId, payload.GasEvent.Address);
        var kcal = context.GetToken(chainId, "KCAL");
        var amountRaw = payload.GasEvent.Amount;

        if ( amountRaw == UnlimitedGasRaw )
        {
            amountRaw = null;
            fee = "0";
        }

        var amount = kcal != null && !string.IsNullOrEmpty(amountRaw)
            ? ApplyDecimals(amountRaw, kcal.DECIMALS)
            : amountRaw;

        return new GasEvent
        {
            price = payload.GasEvent.Price,
            amount = amount,
            fee = string.IsNullOrEmpty(fee) ? "0" : fee,
            address = string.IsNullOrEmpty(payload.GasEvent.Address)
                ? null
                : new Address
                {
                    address = payload.GasEvent.Address,
                    address_name = address?.ADDRESS_NAME
                }
        };
    }

    private static string CalculateGasFee(string price, string amount, EventPayloadContext context, int chainId)
    {
        if ( amount == UnlimitedGasRaw )
            return null;

        if ( string.IsNullOrEmpty(price) || string.IsNullOrEmpty(amount) ) return null;

        if ( !BigInteger.TryParse(price, out var parsedPrice) || !BigInteger.TryParse(amount, out var parsedAmount) )
            return null;

        var kcal = context.GetToken(chainId, "KCAL");
        if ( kcal == null ) return null;

        var fee = ( parsedPrice * parsedAmount ).ToString();
        return CommonsUtils.ToDecimal(fee, kcal.DECIMALS);
    }

    private static GovernanceGasConfigEvent BuildGovernanceGasConfigEvent(EventPayload payload)
    {
        var configPayload = payload.GovernanceGasConfigEvent;
        if (configPayload == null)
        {
            return null;
        }

        return new GovernanceGasConfigEvent
        {
            version = ExtractGovernanceValue(configPayload, "version"),
            max_name_length = ExtractGovernanceValue(configPayload, "max_name_length", "maxNameLength"),
            max_token_symbol_length = ExtractGovernanceValue(configPayload, "max_token_symbol_length", "maxTokenSymbolLength"),
            fee_shift = ExtractGovernanceValue(configPayload, "fee_shift", "feeShift"),
            max_structure_size = ExtractGovernanceValue(configPayload, "max_structure_size", "maxStructureSize"),
            fee_multiplier = ExtractGovernanceValue(configPayload, "fee_multiplier", "feeMultiplier"),
            gas_token_id = ExtractGovernanceValue(configPayload, "gas_token_id", "gasTokenId"),
            data_token_id = ExtractGovernanceValue(configPayload, "data_token_id", "dataTokenId"),
            minimum_gas_offer = ExtractGovernanceValue(configPayload, "minimum_gas_offer", "minimumGasOffer"),
            data_escrow_per_row = ExtractGovernanceValue(configPayload, "data_escrow_per_row", "dataEscrowPerRow"),
            gas_fee_transfer = ExtractGovernanceValue(configPayload, "gas_fee_transfer", "gasFeeTransfer"),
            gas_fee_query = ExtractGovernanceValue(configPayload, "gas_fee_query", "gasFeeQuery"),
            gas_fee_create_token_base = ExtractGovernanceValue(configPayload, "gas_fee_create_token_base", "gasFeeCreateTokenBase"),
            gas_fee_create_token_symbol = ExtractGovernanceValue(configPayload, "gas_fee_create_token_symbol", "gasFeeCreateTokenSymbol"),
            gas_fee_create_token_series = ExtractGovernanceValue(configPayload, "gas_fee_create_token_series", "gasFeeCreateTokenSeries"),
            gas_fee_per_byte = ExtractGovernanceValue(configPayload, "gas_fee_per_byte", "gasFeePerByte"),
            gas_fee_register_name = ExtractGovernanceValue(configPayload, "gas_fee_register_name", "gasFeeRegisterName"),
            gas_burn_ratio_mul = ExtractGovernanceValue(configPayload, "gas_burn_ratio_mul", "gasBurnRatioMul"),
            gas_burn_ratio_shift = ExtractGovernanceValue(configPayload, "gas_burn_ratio_shift", "gasBurnRatioShift")
        };
    }

    private static GovernanceChainConfigEvent BuildGovernanceChainConfigEvent(EventPayload payload)
    {
        var configPayload = payload.GovernanceChainConfigEvent;
        if (configPayload == null)
        {
            return null;
        }

        return new GovernanceChainConfigEvent
        {
            version = ExtractGovernanceValue(configPayload, "version"),
            reserved_1 = ExtractGovernanceValue(configPayload, "reserved_1", "reserved1"),
            reserved_2 = ExtractGovernanceValue(configPayload, "reserved_2", "reserved2"),
            reserved_3 = ExtractGovernanceValue(configPayload, "reserved_3", "reserved3"),
            allowed_tx_types = ExtractGovernanceValue(configPayload, "allowed_tx_types", "allowedTxTypes"),
            expiry_window = ExtractGovernanceValue(configPayload, "expiry_window", "expiryWindow"),
            block_rate_target = ExtractGovernanceValue(configPayload, "block_rate_target", "blockRateTarget")
        };
    }

    private static HashEvent BuildHashEvent(EventPayload payload)
    {
        if ( payload.HashEvent == null ) return null;

        return new HashEvent {hash = payload.HashEvent.Hash};
    }

    private static InfusionEvent BuildInfusionEvent(EventPayload payload, EventPayloadContext context, int chainId)
    {
        if ( payload.InfusionEvent == null ) return null;

        return new InfusionEvent
        {
            token_id = payload.InfusionEvent.TokenId,
            infused_value = payload.InfusionEvent.InfusedValue,
            infused_value_raw = payload.InfusionEvent.InfusedValueRaw,
            base_token = MapToken(context.GetToken(chainId, payload.InfusionEvent.BaseToken)),
            infused_token = MapToken(context.GetToken(chainId, payload.InfusionEvent.InfusedToken))
        };
    }

    private static async Task<MarketEvent> BuildMarketEventAsync(MainDbContext databaseContext, EventPayload payload,
        EventPayloadContext context, long timestampUnixSeconds, int chainId, bool withFiat, string fiatCurrency,
        Dictionary<string, decimal> fiatPricesInUsd)
    {
        if ( payload.MarketEvent == null ) return null;

        FiatPrice fiatPrice = null;
        if ( withFiat )
        {
            var chain = context.GetChain(chainId);
            var priceUsd = await CalculateUsdPriceAsync(databaseContext, chain, timestampUnixSeconds,
                payload.MarketEvent.QuoteToken, payload.MarketEvent.Price, context.TokenPrices);
            var endPriceUsd = await CalculateUsdPriceAsync(databaseContext, chain, timestampUnixSeconds,
                payload.MarketEvent.QuoteToken, payload.MarketEvent.EndPrice, context.TokenPrices);

            if ( priceUsd.HasValue || endPriceUsd.HasValue )
            {
                fiatPrice = new FiatPrice
                {
                    fiat_currency = fiatCurrency,
                    fiat_price = priceUsd.HasValue
                        ? FiatExchangeRateMethods.Convert(fiatPricesInUsd, priceUsd.Value, "USD", fiatCurrency)
                            .ToString("0.####")
                        : null,
                    fiat_price_end = endPriceUsd.HasValue
                        ? FiatExchangeRateMethods.Convert(fiatPricesInUsd, endPriceUsd.Value, "USD", fiatCurrency)
                            .ToString("0.####")
                        : null
                };
            }
        }

        return new MarketEvent
        {
            base_token = MapToken(context.GetToken(chainId, payload.MarketEvent.BaseToken)),
            quote_token = MapToken(context.GetToken(chainId, payload.MarketEvent.QuoteToken)),
            market_event_kind = payload.MarketEvent.MarketEventKind,
            market_id = payload.MarketEvent.MarketId,
            price = payload.MarketEvent.Price,
            end_price = payload.MarketEvent.EndPrice,
            fiat_price = fiatPrice
        };
    }

    private static async Task<decimal?> CalculateUsdPriceAsync(MainDbContext databaseContext, DbChain chain,
        long timestampUnixSeconds, string quoteToken, string rawPrice, TokenMethods.TokenPrice[] tokenPrices)
    {
        if ( chain == null || string.IsNullOrEmpty(quoteToken) || string.IsNullOrEmpty(rawPrice) ) return null;

        var priceUsd = await TokenDailyPricesMethods.CalculateAsync(databaseContext, chain, timestampUnixSeconds,
            quoteToken, rawPrice);
        if ( priceUsd == 0 )
            priceUsd = ( decimal ) TokenMethods.CalculatePrice(tokenPrices, rawPrice, quoteToken);

        return priceUsd == 0 ? null : priceUsd;
    }

    private static OrganizationEvent BuildOrganizationEvent(EventPayload payload, EventPayloadContext context,
        int chainId)
    {
        if ( payload.OrganizationEvent == null ) return null;

        var address = context.GetAddress(chainId, payload.OrganizationEvent.Address);
        return new OrganizationEvent
        {
            organization = string.IsNullOrEmpty(payload.OrganizationEvent.Organization)
                ? null
                : new Organization {name = payload.OrganizationEvent.Organization},
            address = string.IsNullOrEmpty(payload.OrganizationEvent.Address)
                ? null
                : new Address {address = payload.OrganizationEvent.Address, address_name = address?.ADDRESS_NAME}
        };
    }

    private static SaleEvent BuildSaleEvent(EventPayload payload)
    {
        if ( payload.SaleEvent == null ) return null;

        return new SaleEvent {hash = payload.SaleEvent.Hash, sale_event_kind = payload.SaleEvent.SaleEventKind};
    }

    private static StringEvent BuildStringEvent(EventPayload payload)
    {
        if ( payload.StringEvent == null ) return null;

        return new StringEvent {string_value = payload.StringEvent.StringValue};
    }

    private static TokenEvent BuildTokenEvent(EventPayload payload, EventPayloadContext context, int chainId)
    {
        if ( payload.TokenEvent == null ) return null;

        var tokenEntry = context.GetToken(chainId, payload.TokenEvent.Token);
        var valueRaw = payload.TokenEvent.ValueRaw ?? payload.TokenEvent.Value;
        var value = tokenEntry != null && tokenEntry.FUNGIBLE && !string.IsNullOrEmpty(valueRaw)
            ? ApplyDecimals(valueRaw, tokenEntry.DECIMALS)
            : payload.TokenEvent.Value ?? valueRaw;

        return new TokenEvent
        {
            token = MapToken(tokenEntry, payload.TokenEvent.Token),
            value = value,
            value_raw = valueRaw,
            chain_name = payload.TokenEvent.ChainName
        };
    }

    private static TokenCreateEvent BuildTokenCreateEvent(EventPayload payload, EventPayloadContext context,
        int chainId)
    {
        if ( payload.TokenCreateEvent == null ) return null;

        return new TokenCreateEvent
        {
            token = MapToken(context.GetToken(chainId, payload.TokenCreateEvent.Symbol),
                payload.TokenCreateEvent.Symbol),
            max_supply = payload.TokenCreateEvent.MaxSupply,
            decimals = payload.TokenCreateEvent.Decimals,
            is_non_fungible = payload.TokenCreateEvent.IsNonFungible,
            carbon_token_id = payload.TokenCreateEvent.CarbonTokenId,
            metadata = payload.TokenCreateEvent.Metadata
        };
    }

    private static TokenSeriesEvent BuildTokenSeriesEvent(EventPayload payload, EventPayloadContext context,
        int chainId)
    {
        if ( payload.TokenSeriesEvent == null ) return null;

        var ownerEntry = string.IsNullOrEmpty(payload.TokenSeriesEvent.Owner)
            ? null
            : context.GetAddress(chainId, payload.TokenSeriesEvent.Owner);

        return new TokenSeriesEvent
        {
            token = MapToken(context.GetToken(chainId, payload.TokenSeriesEvent.Token), payload.TokenSeriesEvent.Token),
            series_id = payload.TokenSeriesEvent.SeriesId,
            max_mint = payload.TokenSeriesEvent.MaxMint,
            max_supply = payload.TokenSeriesEvent.MaxSupply,
            owner = string.IsNullOrEmpty(payload.TokenSeriesEvent.Owner)
                ? null
                : new Address
                {
                    address = payload.TokenSeriesEvent.Owner,
                    address_name = ownerEntry?.ADDRESS_NAME
                },
            carbon_token_id = payload.TokenSeriesEvent.CarbonTokenId,
            carbon_series_id = payload.TokenSeriesEvent.CarbonSeriesId,
            metadata = payload.TokenSeriesEvent.Metadata
        };
    }

    private static Token MapToken(DbToken token, string symbolFallback = null)
    {
        if ( token != null )
            return new Token
            {
                symbol = token.SYMBOL,
                fungible = token.FUNGIBLE,
                transferable = token.TRANSFERABLE,
                finite = token.FINITE,
                divisible = token.DIVISIBLE,
                fuel = token.FUEL,
                swappable = token.SWAPPABLE,
                burnable = token.BURNABLE,
                stakable = token.STAKABLE,
                fiat = token.FIAT,
                decimals = token.DECIMALS
            };

        return string.IsNullOrEmpty(symbolFallback) ? null : new Token {symbol = symbolFallback};
    }

    private static TransactionSettleEvent BuildTransactionSettleEvent(EventPayload payload,
        EventPayloadContext context)
    {
        if ( payload.TransactionSettleEvent == null ) return null;

        var platform = context.GetPlatform(payload.TransactionSettleEvent.Platform);
        return new TransactionSettleEvent
        {
            hash = payload.TransactionSettleEvent.Hash,
            platform = string.IsNullOrEmpty(payload.TransactionSettleEvent.Platform)
                ? null
                : new Platform
                {
                    name = payload.TransactionSettleEvent.Platform,
                    chain = platform?.CHAIN ?? payload.TransactionSettleEvent.Chain,
                    fuel = platform?.FUEL
                }
        };
    }
}
