using System.Linq;

namespace Database.Main;

public static class OracleMethods
{
    public static Oracle Upsert(MainDbContext databaseContext, string url, string content, bool saveChanges = true)
    {
        if ( string.IsNullOrEmpty(url) || string.IsNullOrEmpty(content) ) return null;

        var oracle = databaseContext.Oracles.FirstOrDefault(x =>
            string.Equals(x.URL.ToUpper(), url.ToUpper()) && string.Equals(x.CONTENT.ToUpper(), content.ToUpper()));
        if ( oracle != null )
            return oracle;

        oracle = new Oracle
        {
            URL = url,
            CONTENT = content
        };

        databaseContext.Oracles.Add(oracle);
        if ( saveChanges ) databaseContext.SaveChanges();

        return oracle;
    }
}
