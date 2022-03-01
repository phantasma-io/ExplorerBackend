using System.Linq;

namespace Database.Main;

public static class AddressValidatorKindMethods
{
    public static AddressValidatorKind Upsert(MainDbContext databaseContext, string name, bool saveChanges = true)
    {
        var entry = databaseContext.AddressValidatorKinds.FirstOrDefault(x => x.NAME == name);

        if ( entry != null ) return entry;

        entry = new AddressValidatorKind
        {
            NAME = name
        };
        databaseContext.AddressValidatorKinds.Add(entry);
        if ( saveChanges ) databaseContext.SaveChanges();

        return entry;
    }
}
