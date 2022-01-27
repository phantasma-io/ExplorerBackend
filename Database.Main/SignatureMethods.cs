namespace Database.Main;

public static class SignatureMethods
{
    public static Signature Upsert(MainDbContext databaseContext, string kind, string data, Transaction transaction,
        bool saveChanges = true)
    {
        if ( string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(data) || transaction is null ) return null;

        var signatureKind = SignatureKindMethods.Upsert(databaseContext, kind, saveChanges);

        var signature = new Signature
        {
            SignatureKind = signatureKind,
            DATA = data,
            Transaction = transaction
        };

        databaseContext.Signatures.Add(signature);
        if ( saveChanges ) databaseContext.SaveChanges();

        return signature;
    }
}
