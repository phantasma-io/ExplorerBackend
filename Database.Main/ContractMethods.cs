using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Commons;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Database.Main;

public static class ContractMethods
{
    // Checks if "Contracts" table has entry with given hash,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.


    public static Contract Get(MainDbContext databaseContext, int chainId, string hash)
    {
        return databaseContext.Contracts.FirstOrDefault(x => x.ChainId == chainId && x.HASH == hash);
    }


    public static int GetId(MainDbContext databaseContext, int chainId, string hash)
    {
        var contract = Get(databaseContext, chainId, hash);

        return contract?.ID ?? 0;
    }


    public static void InsertIfNotExistList(MainDbContext databaseContext, List<Tuple<string, string>> contractInfoList,
        Chain chain, string symbol)
    {
        if (!contractInfoList.Any() || string.IsNullOrEmpty(symbol)) return;

        var contractList = new List<Contract>();
        //name, hash
        foreach (var (name, hash) in contractInfoList)
        {
            var contract =
                databaseContext.Contracts.FirstOrDefault(x =>
                    x.Chain == chain && x.HASH == hash && x.SYMBOL == symbol) ?? DbHelper
                    .GetTracked<Contract>(databaseContext).FirstOrDefault(x =>
                        x.Chain == chain && x.HASH == hash && x.SYMBOL == symbol);

            if (contract != null) continue;

            contract = new Contract { NAME = name, Chain = chain, HASH = hash, SYMBOL = symbol };
            contractList.Add(contract);
        }

        databaseContext.Contracts.AddRange(contractList);
    }


    public static async Task<Contract> UpsertAsync(MainDbContext databaseContext, string name, Chain chain, string hash, string symbol)
    {
        //also check data in cache
        var contract =
            await databaseContext.Contracts.FirstOrDefaultAsync(x => x.Chain == chain && x.HASH == hash && x.SYMBOL == symbol) ??
            DbHelper.GetTracked<Contract>(databaseContext)
                .FirstOrDefault(x => x.Chain == chain && x.HASH == hash && x.SYMBOL == symbol);

        // Fallback: existing contract with same hash but different symbol (or null symbol)
        if (contract == null)
        {
            contract = await databaseContext.Contracts.FirstOrDefaultAsync(x => x.Chain == chain && x.HASH == hash) ??
                       DbHelper.GetTracked<Contract>(databaseContext)
                           .FirstOrDefault(x => x.Chain == chain && x.HASH == hash);
        }

        if (contract != null)
        {
            if (!string.IsNullOrEmpty(name))
                contract.NAME = name;
            if (!string.IsNullOrEmpty(symbol))
                contract.SYMBOL = symbol;

            return contract;
        }

        contract = new Contract { NAME = name, Chain = chain, HASH = hash, SYMBOL = symbol };

        await databaseContext.Contracts.AddAsync(contract);

        return contract;
    }

    public static Contract Get(MainDbContext databaseContext, Chain chain, string hash)
    {
        var contract = databaseContext.Contracts.FirstOrDefault(x => x.Chain == chain && x.HASH == hash);
        if (contract != null) return contract;

        contract = DbHelper.GetTracked<Contract>(databaseContext)
            .FirstOrDefault(x => x.Chain == chain && x.HASH == hash);
        return contract;
    }

    public readonly record struct ChainHashKey(int ChainId, string Hash);
    private static Dictionary<ChainHashKey, int> ChunkUpsert(NpgsqlConnection dbConnection,
        List<(string name, int chainId, string hash, string symbol)> chunk,
        NpgsqlTransaction dbTransaction = null)
    {
        Dictionary<ChainHashKey, int> result = [];

        if (chunk.Count == 0)
        {
            return [];
        }

        using var cmd = new NpgsqlCommand(null, dbConnection, dbTransaction);

        cmd.CommandText = $"insert into \"Contracts\" (\"ChainId\", \"HASH\", \"NAME\", \"SYMBOL\", \"LAST_UPDATED_UNIX_SECONDS\") values ";

        for (var i = 0; i < chunk.Count; i++)
        {
            if (i != 0)
            {
                cmd.CommandText += ",\n";
            }
            cmd.CommandText += "(";
            cmd.CommandText += string.Format("@chainId{0}", i) + ", ";
            cmd.CommandText += string.Format("@hash{0}", i) + ", ";

            cmd.CommandText += string.Format("@name{0}", i) + ", ";
            cmd.CommandText += string.Format("@symbol{0}", i) + ", ";
            cmd.CommandText += "@dm";
            cmd.CommandText += ")";

            // Keys
            cmd.Parameters.Add(string.Format("@chainId{0}", i), NpgsqlDbType.Integer).Value = chunk.ElementAt(i).chainId;
            cmd.Parameters.Add(string.Format("@hash{0}", i), NpgsqlDbType.Text).Value = chunk.ElementAt(i).hash;

            // Others
            cmd.Parameters.Add(string.Format("@name{0}", i), NpgsqlDbType.Text).Value = (object)chunk.ElementAt(i).name ?? DBNull.Value;
            cmd.Parameters.Add(string.Format("@symbol{0}", i), NpgsqlDbType.Text).Value = (object)chunk.ElementAt(i).symbol ?? DBNull.Value;
        }
        cmd.Parameters.Add("@dm", NpgsqlDbType.Bigint).Value = UnixSeconds.Now();

        cmd.CommandText += " on conflict (\"ChainId\",\"HASH\") do update set \"NAME\"=excluded.\"NAME\", \"SYMBOL\"=excluded.\"SYMBOL\", \"LAST_UPDATED_UNIX_SECONDS\"=excluded.\"LAST_UPDATED_UNIX_SECONDS\" returning \"ChainId\",\"HASH\",\"ID\"";

        cmd.ExecuteReaderEx((ref NpgsqlDataReader dr) =>
        {
            var chainId = NpgsqlHelpers.GetField<Int32>(ref dr, 0);
            var hash = NpgsqlHelpers.GetField<string>(ref dr, 1);
            var id = NpgsqlHelpers.GetField<Int32>(ref dr, 2);
            result.Add(new ChainHashKey(chainId, hash), id);
        });

        return result;
    }

    public static Dictionary<ChainHashKey, int> BatchUpsert(NpgsqlConnection dbConnection,
        List<(string name, int chainId, string hash, string symbol)> contracts, int batchSize = 500,
        NpgsqlTransaction dbTransaction = null)
    {
        Dictionary<ChainHashKey, int> result = [];

        if (contracts == null || contracts.Count == 0)
        {
            return [];
        }

        // Contract HASH is a database key. Upstream RPC compatibility events can be
        // malformed, but an empty hash must stop ingest before Npgsql parameters.
        var invalidContracts = contracts
            .Where(x => string.IsNullOrWhiteSpace(x.hash))
            .ToList();
        if (invalidContracts.Count > 0)
        {
            var samples = invalidContracts
                .Take(5)
                .Select(x => $"chainId={x.chainId}, name={x.name ?? "<null>"}, symbol={x.symbol ?? "<null>"}")
                .ToArray();
            throw new ArgumentException(
                $"Contracts batch upsert received {invalidContracts.Count} empty HASH value(s). Samples: {string.Join("; ", samples)}",
                nameof(contracts));
        }

        var validContracts = contracts
            .Where(x => !string.IsNullOrWhiteSpace(x.hash))
            .DistinctBy(x => (x.chainId, x.hash))
            .ToList();

        if (validContracts.Count == 0)
        {
            return [];
        }

        var chunks = validContracts.Chunk(batchSize).ToList();
        foreach (var chunk in chunks)
        {
            var chunkRes = ChunkUpsert(dbConnection, chunk.ToList(), dbTransaction);
            // Merge chunk results in-place to avoid per-chunk dictionary reallocation.
            foreach (var entry in chunkRes)
                result[entry.Key] = entry.Value;
        }

        return result;
    }
}

public static class ContractMethodsExtensions
{
    public static int GetId(this Dictionary<ContractMethods.ChainHashKey, int> contracts, int chainId, string hash)
    {
        var key = new ContractMethods.ChainHashKey(chainId, hash);
        if (contracts.TryGetValue(key, out var id))
            return id;

        throw new KeyNotFoundException($"Contract id not found for chain={chainId}, hash={hash}");
    }
}
