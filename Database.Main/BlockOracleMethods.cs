using System;
using System.Collections.Generic;
using System.Linq;

namespace Database.Main;

public static class BlockOracleMethods
{
    public static void InsertIfNotExists(MainDbContext databaseContext, List<Tuple<string, string>> blockOracleList,
        Block block)
    {
        if ( !blockOracleList.Any() || block == null ) return;

        var oracles = OracleMethods.InsertIfNotExists(databaseContext, blockOracleList);

        var blockOracles = oracles.Select(oracle => new BlockOracle {Oracle = oracle, Block = block}).ToList();

        databaseContext.BlockOracles.AddRange(blockOracles);
    }
}
