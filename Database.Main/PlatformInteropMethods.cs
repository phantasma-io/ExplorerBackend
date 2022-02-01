using System.Linq;

namespace Database.Main;

public static class PlatformInteropMethods
{
    public static void Upsert(MainDbContext databaseContext, string localAddress, string externalAddress, int chainId,
        Platform platform, bool saveChanges = true)
    {
        var addressEntry = AddressMethods.Upsert(databaseContext, chainId, localAddress, saveChanges);

        var platformInterop =
            databaseContext.PlatformInterops.FirstOrDefault(x =>
                string.Equals(x.EXTERNAL.ToUpper(), externalAddress.ToUpper()) && x.LocalAddressId == addressEntry.ID);
        if ( platformInterop != null )
            return;


        platformInterop = new PlatformInterop
            {EXTERNAL = externalAddress, LocalAddress = addressEntry, Platform = platform};

        databaseContext.PlatformInterops.Add(platformInterop);
        if ( saveChanges ) databaseContext.SaveChanges();
    }


    public static PlatformInterop Get(MainDbContext databaseContext, string externalAddress)
    {
        return databaseContext.PlatformInterops
            .FirstOrDefault(x => x.EXTERNAL == externalAddress);
    }
}
