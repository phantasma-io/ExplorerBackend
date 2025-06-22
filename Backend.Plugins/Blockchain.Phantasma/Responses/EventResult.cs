using System.Collections.Generic;
using Phantasma.Core.Domain.Events.Structs;

using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Contract.Sale.Structs;
using Phantasma.Core.Numerics;
using Phantasma.Core.Domain.Serializer;
using System;
using Serilog;
using System.Numerics;
using System.Linq;

namespace Backend.Blockchain.Responses;

public class EventResult
{
    public string Address { get; set; }
    public string Contract { get; set; }
    public string Kind { get; set; }
    public string Name { get; set; }
    public string Data { get; set; }

    public EventKind KindParsed;
    public object? DataParsed;

    public void ParseData(BigInteger blockHeight)
    {
        if (!Enum.TryParse<EventKind>(Kind, out KindParsed))
        {
            Log.Error($"Unsupported event kind {Kind}");
            return;
        }

        try
        {
            switch (KindParsed)
            {
                case EventKind.Infusion:
                    {
                        DataParsed = Serialization.Unserialize<InfusionEventData>(Base16.Decode(Data));
                        break;
                    }
                case EventKind.TokenMint or EventKind.TokenClaim or EventKind.TokenBurn
                    or EventKind.TokenSend or EventKind.TokenReceive or EventKind.TokenStake
                    or EventKind.CrownRewards or EventKind.Inflation:
                    {
                        DataParsed = Serialization.Unserialize<TokenEventData>(Base16.Decode(Data));
                        break;
                    }
                case EventKind.OrderCancelled or EventKind.OrderClosed or EventKind.OrderCreated
                    or EventKind.OrderFilled or EventKind.OrderBid:
                    {
                        DataParsed = Serialization.Unserialize<MarketEventData>(Base16.Decode(Data));
                        break;
                    }
                case EventKind.ChainCreate or EventKind.TokenCreate or EventKind.ContractUpgrade
                    or EventKind.AddressRegister or EventKind.ContractDeploy or EventKind.PlatformCreate
                    or EventKind.OrganizationCreate or EventKind.Log or EventKind.AddressUnregister:
                    //or EventKind.Error:
                    {
                        DataParsed = Serialization.Unserialize<string>(Base16.Decode(Data));
                        break;
                    }
                case EventKind.Crowdsale:
                    {
                        DataParsed = Serialization.Unserialize<SaleEventData>(Base16.Decode(Data));
                        break;
                    }
                case EventKind.ChainSwap:
                    {
                        DataParsed = Serialization.Unserialize<TransactionSettleEventData>(Base16.Decode(Data));
                        break;
                    }
                case EventKind.ValidatorElect or EventKind.ValidatorPropose:
                    {
                        DataParsed = Serialization.Unserialize<TransactionSettleEventData>(Base16.Decode(Data));
                        break;
                    }
                case EventKind.ValueCreate or EventKind.ValueUpdate:
                    {
                        DataParsed = Serialization.Unserialize<ChainValueEventData>(Base16.Decode(Data));
                        break;
                    }
                case EventKind.GasEscrow or EventKind.GasPayment:
                    {
                        DataParsed = Serialization.Unserialize<GasEventData>(Base16.Decode(Data));
                        break;
                    }
                case EventKind.FileCreate or EventKind.FileDelete:
                    {
                        DataParsed = Serialization.Unserialize<Hash>(Base16.Decode(Data));
                        break;
                    }
                case EventKind.OrganizationAdd or EventKind.OrganizationRemove:
                    {
                        DataParsed = Serialization.Unserialize<OrganizationEventData>(Base16.Decode(Data));
                        break;
                    }
                //TODO
                case EventKind.ValidatorSwitch or EventKind.LeaderboardCreate or EventKind.Custom:
                    {
                        Log.Verbose("[{Name}][Blocks] Block #{Height}: Event {Kind} not yet supported", Name, blockHeight, Kind);
                        break;
                    }
                default:
                    Log.Warning("[{Name}][Blocks] Block #{BlockHeight} Currently not processing EventKind {Kind} in Block #{Block}",
                        Name, blockHeight, Kind, blockHeight);
                    break;
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "[{Name}][Blocks] Block #{BlockHeight} event processing", Name, blockHeight);
        }
    }

    public T GetParsedData<T>()
    {
        return (T)DataParsed;
    }

    public List<string> GetContracts()
    {
        List<string> result = [Contract];

        if (DataParsed == null)
        {
            return result;
        }
        switch (KindParsed)
        {
            case EventKind.Infusion:
                {
                    result.Add(((InfusionEventData)DataParsed).BaseSymbol);
                    result.Add(((InfusionEventData)DataParsed).InfusedSymbol);
                    break;
                }
            case EventKind.TokenMint or EventKind.TokenClaim or EventKind.TokenBurn
                or EventKind.TokenSend or EventKind.TokenReceive or EventKind.TokenStake
                or EventKind.CrownRewards or EventKind.Inflation:
                {
                    result.Add(((TokenEventData)DataParsed).Symbol);
                    break;
                }
            case EventKind.OrderCancelled or EventKind.OrderClosed or EventKind.OrderCreated
                or EventKind.OrderFilled or EventKind.OrderBid:
                {
                    result.Add(((MarketEventData)DataParsed).BaseSymbol);
                    result.Add(((MarketEventData)DataParsed).QuoteSymbol);
                    break;
                }
            case EventKind.ContractUpgrade:
                {
                    result.Add((string)DataParsed);
                    break;
                }
            case EventKind.TokenCreate:
                {
                    result.Add((string)DataParsed);

                    break;
                }
            case EventKind.ContractDeploy:
                {
                    result.Add((string)DataParsed);

                    break;
                }
        }
        return result;
    }
}

public static class EventResultExtensions
{
    public static void ParseData(this ICollection<EventResult> eventResults, BigInteger blockHeight)
    {
        if (eventResults == null)
            return;

        foreach (var e in eventResults)
        {
            e.ParseData(blockHeight);
        }
    }
    public static List<string> GetContracts(this ICollection<EventResult> eventResults)
    {
        if (eventResults == null)
            return [];

        List<string> result = [];
        foreach (var e in eventResults)
        {
            result.AddRange(e.GetContracts());
        }

        return result.Distinct().ToList();
    }
}
