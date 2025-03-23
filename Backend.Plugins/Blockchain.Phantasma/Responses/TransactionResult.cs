using System;

namespace Backend.Blockchain.Responses;

public class TxEvent
{
    public string address { get; set; }
    public string contract { get; set; }
    public string kind { get; set; }
    public string name { get; set; }
    public string data { get; set; }
}

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
    public TxEvent[] events { get; set; }
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
}
