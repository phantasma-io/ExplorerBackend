using System.Collections.Generic;
using System.Linq;

namespace Database.Main;

public static class SignatureKindMethods
{
    public static SignatureKind Upsert(MainDbContext databaseContext, string name, bool saveChanges = true)
    {
        var signatureKind = databaseContext.SignatureKinds.FirstOrDefault(x => x.NAME == name);

        if ( signatureKind != null )
            return signatureKind;

        signatureKind = new SignatureKind {NAME = name};

        databaseContext.SignatureKinds.Add(signatureKind);
        if ( saveChanges ) databaseContext.SaveChanges();

        return signatureKind;
    }


    public static Dictionary<string, SignatureKind> InsertIfNotExists(MainDbContext databaseContext, List<string> names,
        bool saveChanges = true)
    {
        if ( !names.Any() ) return null;

        var kindListToInsert = new List<SignatureKind>();
        //we use that to return
        Dictionary<string, SignatureKind> kindMap = new();

        foreach ( var name in names )
        {
            var signatureKind = databaseContext.SignatureKinds.FirstOrDefault(x => x.NAME == name);

            if ( signatureKind == null )
            {
                signatureKind = new SignatureKind {NAME = name};
                kindListToInsert.Add(signatureKind);
            }

            if ( !kindMap.ContainsKey(name) ) kindMap.Add(name, signatureKind);
        }

        databaseContext.SignatureKinds.AddRange(kindListToInsert);
        if ( !saveChanges ) databaseContext.SaveChanges();

        return kindMap;
    }
}
