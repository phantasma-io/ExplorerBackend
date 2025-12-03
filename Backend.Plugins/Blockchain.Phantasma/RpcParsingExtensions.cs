using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using PhantasmaPhoenix.Core;
using PhantasmaPhoenix.Core.Extensions;
using PhantasmaPhoenix.Cryptography;
using PhantasmaPhoenix.Protocol;
using PhantasmaPhoenix.Protocol.Carbon;
using PhantasmaPhoenix.Protocol.Carbon.Blockchain;
using PhantasmaPhoenix.Protocol.ExtendedEvents;
using PhantasmaPhoenix.RPC.Models;
using Serilog;

namespace Backend.Blockchain;

/// <summary>
///     Helpers to parse RPC models (EventResult/TransactionResult/BlockResult) and attach typed payloads.
///     We keep parsed data in a weak table to avoid mutating SDK types or duplicating models.
/// </summary>
internal static class RpcParsingExtensions
{
    private sealed class ParsedEventData
    {
        public EventKind Kind;
        public object Parsed;
    }

    private static readonly ConditionalWeakTable<EventResult, ParsedEventData> ParsedEvents = new();

    public static void ParseData(this BlockResult block)
    {
        if (block?.Txs == null)
            return;

        block.Txs.ParseData(new BigInteger(block.Height));
    }

    public static List<string> GetContracts(this BlockResult block)
    {
        return block?.Txs?.GetContracts() ?? [];
    }

    public static void ParseData(this ICollection<TransactionResult> transactionResults, BigInteger blockHeight)
    {
        if (transactionResults == null)
            return;

        foreach (var t in transactionResults)
        {
            t.ParseData(blockHeight);
        }
    }

    public static void ParseData(this TransactionResult transaction, BigInteger blockHeight)
    {
        if (transaction?.Events == null)
            return;

        foreach (var e in transaction.Events)
        {
            e.ParseData(blockHeight);
        }
    }

    public static void ParseData(this EventResult evt, BigInteger blockHeight)
    {
        if (evt == null) return;

        if (!Enum.TryParse<EventKind>(evt.Kind, out var kind))
        {
            Log.Error($"Unsupported event kind {evt.Kind}");
            return;
        }

        try
        {
            object parsed = kind switch
            {
                EventKind.Infusion => Serialization.Unserialize<InfusionEventData>(Base16.Decode(evt.Data)),
                EventKind.TokenMint or EventKind.TokenClaim or EventKind.TokenBurn
                    or EventKind.TokenSend or EventKind.TokenReceive or EventKind.TokenStake
                    or EventKind.CrownRewards or EventKind.Inflation or EventKind.TokenCreate
                    => Serialization.Unserialize<TokenEventData>(Base16.Decode(evt.Data)),
                EventKind.OrderCancelled or EventKind.OrderClosed or EventKind.OrderCreated
                    or EventKind.OrderFilled or EventKind.OrderBid
                    => Serialization.Unserialize<MarketEventData>(Base16.Decode(evt.Data)),
                EventKind.ChainCreate or EventKind.ContractUpgrade
                    or EventKind.AddressRegister or EventKind.ContractDeploy or EventKind.PlatformCreate
                    or EventKind.OrganizationCreate or EventKind.Log or EventKind.AddressUnregister
                    => Serialization.Unserialize<string>(Base16.Decode(evt.Data)),
                EventKind.Crowdsale => Serialization.Unserialize<SaleEventData>(Base16.Decode(evt.Data)),
                EventKind.ChainSwap => Serialization.Unserialize<TransactionSettleEventData>(Base16.Decode(evt.Data)),
                EventKind.ValidatorElect or EventKind.ValidatorPropose
                    => Serialization.Unserialize<TransactionSettleEventData>(Base16.Decode(evt.Data)),
                EventKind.ValueCreate or EventKind.ValueUpdate
                    => Serialization.Unserialize<ChainValueEventData>(Base16.Decode(evt.Data)),
                EventKind.GasEscrow or EventKind.GasPayment
                    => Serialization.Unserialize<GasEventData>(Base16.Decode(evt.Data)),
                EventKind.FileCreate or EventKind.FileDelete
                    => Serialization.Unserialize<Hash>(Base16.Decode(evt.Data)),
                EventKind.OrganizationAdd or EventKind.OrganizationRemove
                    => Serialization.Unserialize<OrganizationEventData>(Base16.Decode(evt.Data)),
                EventKind.GovernanceSetGasConfig => SerializeToJson(CarbonBlob.New<GasConfig>(Base16.Decode(evt.Data))),
                EventKind.GovernanceSetChainConfig => SerializeToJson(CarbonBlob.New<ChainConfig>(Base16.Decode(evt.Data))),
                EventKind.ValidatorSwitch or EventKind.LeaderboardCreate or EventKind.Custom => null,
                _ => null
            };

            ParsedEvents.AddOrUpdate(evt, parsed, kind);
        }
        catch (Exception e)
        {
            Log.Error(e, "[RpcParsingExtensions] Block #{BlockHeight} event processing", blockHeight);
        }
    }

    public static T GetParsedData<T>(this EventResult evt)
    {
        var parsed = GetParsedData(evt);
        return parsed is T typed ? typed : default;
    }

    public static object GetParsedData(this EventResult evt)
    {
        return ParsedEvents.TryGetValue(evt, out var data) ? data.Parsed : null;
    }

    public static EventKind? GetParsedKind(this EventResult evt)
    {
        if ( evt == null )
            return null;

        if ( ParsedEvents.TryGetValue(evt, out var data) )
            return data.Kind;

        // allow manually created events to resolve their kind without re-parsing the payload
        if ( Enum.TryParse<EventKind>(evt.Kind, out var kind) )
        {
            ParsedEvents.AddOrUpdate(evt, null, kind);
            return kind;
        }

        return null;
    }

    public static List<string> GetContracts(this ICollection<TransactionResult> transactionResults)
    {
        if (transactionResults == null)
            return [];

        List<string> result = [];
        foreach (var t in transactionResults)
        {
            result.AddRange(t.GetContracts());
        }

        return result.Distinct().ToList();
    }

    public static List<string> GetContracts(this TransactionResult transaction)
    {
        if (transaction == null)
            return [];

        var result = new List<string>();

        if (transaction.Events == null)
            return result;

        foreach (var e in transaction.Events)
        {
            result.Add(e.Contract);

            var parsed = e.GetParsedData();
            if (parsed == null) continue;

            switch (e.GetParsedKind())
            {
                case EventKind.Infusion:
                    result.Add(((InfusionEventData)parsed).BaseSymbol);
                    result.Add(((InfusionEventData)parsed).InfusedSymbol);
                    break;
                case EventKind.TokenMint or EventKind.TokenClaim or EventKind.TokenBurn
                    or EventKind.TokenSend or EventKind.TokenReceive or EventKind.TokenStake
                    or EventKind.CrownRewards or EventKind.Inflation or EventKind.TokenCreate:
                    result.Add(((TokenEventData)parsed).Symbol);
                    break;
                case EventKind.OrderCancelled or EventKind.OrderClosed or EventKind.OrderCreated
                    or EventKind.OrderFilled or EventKind.OrderBid:
                    result.Add(((MarketEventData)parsed).BaseSymbol);
                    result.Add(((MarketEventData)parsed).QuoteSymbol);
                    break;
                case EventKind.ContractUpgrade:
                case EventKind.ContractDeploy:
                    result.Add((string)parsed);
                    break;
            }
        }

        return result.Distinct().ToList();
    }

    private static string SerializeToJson(object obj)
    {
        return obj == null ? null : JsonConvert.SerializeObject(obj, Formatting.None);
    }

    private static void AddOrUpdate(this ConditionalWeakTable<EventResult, ParsedEventData> table,
        EventResult evt, object parsed, EventKind kind)
    {
        if (table.TryGetValue(evt, out var existing))
        {
            existing.Kind = kind;
            existing.Parsed = parsed;
        }
        else
        {
            table.Add(evt, new ParsedEventData {Kind = kind, Parsed = parsed});
        }
    }
}
