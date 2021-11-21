using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Serilog;
using System;
using System.Linq;

namespace Database.Main
{
    public static class DbHelper
    {
        // Searches for database entities of type 'type' among tracked changes
        // that are not yet applied to the database.
        // Searches only newly added objects, modified and other states are skipped.
        // Used to fight concurrency/async issues when threads trying
        // to add same objects twice.
        public static Type[] GetTracked<Type>(MainDatabaseContext databaseContext)
        {
            return databaseContext.ChangeTracker.Entries()
                .Where(x => x.State == EntityState.Added &&
                x.Entity != null &&
                typeof(Type).IsAssignableFrom(x.Entity.GetType())).Select(x => (Type)x.Entity).ToArray();
        }

        public static void LogTracked<Type>(MainDatabaseContext databaseContext)
        {
            Log.Information($"LogTracked({typeof(Type).ToString()}):");
            foreach(var e in databaseContext.ChangeTracker.Entries()
                .Where(x => x.State == EntityState.Added &&
                x.Entity != null &&
                typeof(Type).IsAssignableFrom(x.Entity.GetType())).Select(x => x.Entity))
            {
                Log.Information($"Entry: type {e.GetType()}, {e.ToString()}");
            }
        }
    }
}