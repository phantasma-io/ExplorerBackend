using System;
using System.Collections.Generic;
using System.Linq;

namespace Database.Main;

public static class SignatureMethods
{
    public static void InsertIfNotExists(MainDbContext databaseContext, List<Tuple<string, string>> signatures,
        Transaction transaction, bool saveChanges = true)
    {
        if ( !signatures.Any() || transaction == null ) return;

        //item1 == kind
        var kindList = signatures.Select(tuple => tuple.Item1).ToList();
        var kindMap = SignatureKindMethods.InsertIfNotExists(databaseContext, kindList, saveChanges);

        var signatureList = new List<Signature>();

        foreach ( var (kind, data) in signatures )
        {
            var signature = new Signature
            {
                SignatureKind = kindMap.GetValueOrDefault(kind),
                DATA = data,
                Transaction = transaction
            };
            signatureList.Add(signature);
        }

        databaseContext.Signatures.AddRange(signatureList);
        if ( !saveChanges ) databaseContext.SaveChanges();
    }
}
