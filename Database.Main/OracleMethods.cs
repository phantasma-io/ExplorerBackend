using System;
using System.Collections.Generic;
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


    public static IEnumerable<Oracle> InsertIfNotExists(MainDbContext databaseContext,
        List<Tuple<string, string>> oracleList,
        bool saveChanges = true)
    {
        if ( !oracleList.Any() ) return null;

        var oracleListToReturn = new List<Oracle>();
        var oracleListToInsert = new List<Oracle>();

        foreach ( var (url, content) in oracleList )
        {
            //might be faster than equals + toUpper
            var oracle = databaseContext.Oracles.FirstOrDefault(x =>
                x.URL == url && x.CONTENT == content);

            if ( oracle == null )
            {
                oracle = new Oracle
                {
                    URL = url,
                    CONTENT = content
                };
                oracleListToInsert.Add(oracle);
            }
            oracleListToReturn.Add(oracle);
        }

        databaseContext.Oracles.AddRange(oracleListToInsert);
        if ( saveChanges ) databaseContext.SaveChanges();

        return oracleListToReturn;
    }
}
