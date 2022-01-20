using System.Linq;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Database.Main;

public static class DbHelper
{
    // Searches for database entities of type 'type' among tracked changes
    // that are not yet applied to the database.
    // Searches only newly added objects, modified and other states are skipped.
    // Used to fight concurrency/async issues when threads trying
    // to add same objects twice.
    public static Type[] GetTracked<Type>(MainDbContext databaseContext)
    {
        return databaseContext.ChangeTracker.Entries()
            .Where(x => x.State == EntityState.Added &&
                        x.Entity is Type).Select(x => ( Type ) x.Entity).ToArray();
    }


    public static void LogTracked<Type>(MainDbContext databaseContext)
    {
        Log.Information("LogTracked({Type}):", typeof(Type));
        foreach ( var e in databaseContext.ChangeTracker.Entries()
                     .Where(x => x.State == EntityState.Added &&
                                 x.Entity is Type).Select(x => x.Entity) )
            Log.Information("Entry: type {Type}, {String}", e.GetType(), e.ToString());
    }
}
