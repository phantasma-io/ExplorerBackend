using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Backend.Blockchain.Responses;

public class TxSignature
{
    public string kind { get; set; }
    public string data { get; set; }
}

public class TransactionResult
{
    public string hash { get; set; }
    public string chainAddress { get; set; }
    public UInt64 timestamp { get; set; }
    public UInt64 blockHeight { get; set; }
    public string blockHash { get; set; }
    public string script { get; set; }
    public string payload { get; set; }
    public string? debugComment { get; set; }
    public EventResult[] events { get; set; }
    public string result { get; set; }
    public string fee { get; set; }
    public string state { get; set; }
    public TxSignature[]? signatures { get; set; }
    public string sender { get; set; } = "NULL"; // Initialized as in original Phantasma code.
    public string gasPayer { get; set; }
    public string gasTarget { get; set; } = "NULL";
    public string gasPrice { get; set; } = "";
    public string gasLimit { get; set; }
    public UInt64 expiration { get; set; }

    public void ParseData(BigInteger blockHeight)
    {
        events.ParseData(blockHeight);
    }

    public List<string> GetContracts()
    {
        return events.GetContracts();
    }
}

public static class TransactionResultExtensions
{
    public static void ParseData(this ICollection<TransactionResult> transactionResults, BigInteger blockHeight)
    {
        if (transactionResults == null)
            return;

        foreach (var t in transactionResults)
        {
            t.ParseData(blockHeight);
        }
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
}
