using System.Linq;

namespace Database.Main;

public static class SignatureKindMethods
{
    public static SignatureKind Upsert(MainDbContext databaseContext, string name, bool saveChanges = true)
    {
        var signatureKind =
            databaseContext.SignatureKinds.FirstOrDefault(x => string.Equals(x.NAME.ToUpper(), name.ToUpper()));

        if ( signatureKind != null )
            return signatureKind;

        signatureKind = new SignatureKind {NAME = name};

        databaseContext.SignatureKinds.Add(signatureKind);
        if ( saveChanges ) databaseContext.SaveChanges();

        return signatureKind;
    }
}
