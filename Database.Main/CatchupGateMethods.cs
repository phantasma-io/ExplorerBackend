using System;

namespace Database.Main;

public static class CatchupGateMethods
{
    private const string CatchupReadyKeyPrefix = "PHA_BG_CATCHUP_READY";

    public static string GetCatchupReadyKey(int chainId)
    {
        return $"{CatchupReadyKeyPrefix}_{chainId}";
    }

    public static bool TryGetCatchupReady(MainDbContext databaseContext, int chainId, out bool isCatchupReady)
    {
        if (databaseContext == null)
            throw new ArgumentNullException(nameof(databaseContext));

        isCatchupReady = false;
        if (chainId <= 0)
            return false;

        var key = GetCatchupReadyKey(chainId);
        var hasValue = GlobalVariableMethods.AnyAsync(databaseContext, key).GetAwaiter().GetResult();
        if (!hasValue)
        {
            // During startup this key may not be persisted yet.
            // Default to "not ready" so optional background jobs defer instead of
            // competing with initial block catch-up.
            return true;
        }

        isCatchupReady = GlobalVariableMethods.GetLongAsync(databaseContext, key).GetAwaiter().GetResult() != 0;
        return true;
    }

    public static void SetCatchupReady(MainDbContext databaseContext, int chainId, bool isCatchupReady,
        bool saveChanges = true)
    {
        if (databaseContext == null)
            throw new ArgumentNullException(nameof(databaseContext));

        var key = GetCatchupReadyKey(chainId);
        GlobalVariableMethods.UpsertAsync(databaseContext, key, isCatchupReady ? 1 : 0, saveChanges)
            .GetAwaiter().GetResult();
    }
}
