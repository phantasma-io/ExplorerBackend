namespace Database.Main;

public static class BlockOracleMethods
{
    public static BlockOracle Upsert(MainDbContext databaseContext, string url, string content, Block block,
        bool saveChanges = true)
    {
        if ( string.IsNullOrEmpty(url) || string.IsNullOrEmpty(content) || block is null ) return null;

        var oracle = OracleMethods.Upsert(databaseContext, url, content, saveChanges);

        var blockOracle = new BlockOracle
        {
            Oracle = oracle,
            Block = block
        };

        databaseContext.BlockOracles.Add(blockOracle);
        if ( saveChanges ) databaseContext.SaveChanges();

        return blockOracle;
    }
}
