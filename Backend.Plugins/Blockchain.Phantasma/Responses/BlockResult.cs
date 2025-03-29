using System.Collections.Generic;
using System.Numerics;

namespace Backend.Blockchain.Responses;

public class BlockResult
{
    public string error { get; set; }

    public string hash { get; set; }
    public string previousHash { get; set; }
    public uint timestamp { get; set; }
    public uint height { get; set; }
    public string chainAddress { get; set; }
    public uint protocol { get; set; }
    public TransactionResult[] txs { get; set; }
    public string validatorAddress { get; set; }
    public string reward { get; set; }
    public EventResult[] events { get; set; }
    public OracleResult[] oracles { get; set; }

    public void ParseData()
    {
        txs.ParseData(new BigInteger(height));
    }
    public List<string> GetContracts()
    {
        return txs.GetContracts();
    }
}
