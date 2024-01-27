using System;
using System.Collections.Generic;
using System.Linq;

namespace Database.Main;

public static class PlatformInteropMethods
{
    public static void InsertIfNotExists(MainDbContext databaseContext, List<Tuple<string, string>> interopList,
        Chain chain, Platform platform)
    {
        //item1 = local address
        var addresses = interopList.Select(tuple => tuple.Item1).ToList();
        var addressMap = AddressMethods.InsertIfNotExists(databaseContext, chain, addresses);

        var platformInteropList = new List<PlatformInterop>();

        foreach ( var (localAddress, externalAddress) in interopList )
        {
            var addressEntry = addressMap.GetValueOrDefault(localAddress);
            var platformInterop =
                databaseContext.PlatformInterops.FirstOrDefault(x =>
                    x.EXTERNAL == externalAddress && x.LocalAddress == addressEntry);
            if ( platformInterop != null ) continue;

            platformInterop = new PlatformInterop
                {EXTERNAL = externalAddress, LocalAddress = addressEntry, Platform = platform};
            platformInteropList.Add(platformInterop);
        }

        databaseContext.PlatformInterops.AddRange(platformInteropList);
    }
}
