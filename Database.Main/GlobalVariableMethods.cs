using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Database.Main
{
    public static class GlobalVariableMethods
    {
        // Checks if "GlobalVariables" table has entry,
        // and adds new entry, if there's no entry available.
        public static async Task UpsertAsync(MainDbContext databaseContext, string name, long value, bool saveChanges = true)
        {
            var entry = await databaseContext.GlobalVariables.FirstOrDefaultAsync(x => x.NAME == name.ToUpperInvariant());

            if (entry == null)
            {
                entry = new GlobalVariable { NAME = name.ToUpperInvariant(), LONG_VALUE = value };

                await databaseContext.GlobalVariables.AddAsync(entry);
            }
            else
            {
                entry.LONG_VALUE = value;
            }

            if (saveChanges)
            {
                await databaseContext.SaveChangesAsync();
            }
        }

        public static async Task UpsertAsync(MainDbContext databaseContext, string name, string value, bool saveChanges = true)
        {
            var entry = await databaseContext.GlobalVariables.FirstOrDefaultAsync(x => x.NAME == name.ToUpperInvariant());

            if (entry == null)
            {
                entry = new GlobalVariable { NAME = name.ToUpperInvariant(), STRING_VALUE = value };

                await databaseContext.GlobalVariables.AddAsync(entry);
            }
            else
            {
                entry.STRING_VALUE = value;
            }

            if (saveChanges)
            {
                await databaseContext.SaveChangesAsync();
            }
        }

        public static Task<bool> AnyAsync(MainDbContext databaseContext, string name)
        {
            return databaseContext.GlobalVariables.AnyAsync(x => x.NAME == name.ToUpperInvariant());
        }

        public static Task<long> GetLongAsync(MainDbContext databaseContext, string name)
        {
            return databaseContext.GlobalVariables.Where(x => x.NAME == name.ToUpperInvariant()).Select(x => x.LONG_VALUE).FirstOrDefaultAsync();
        }
        public static Task<string> GetStringAsync(MainDbContext databaseContext, string name)
        {
            return databaseContext.GlobalVariables.Where(x => x.NAME == name.ToUpperInvariant()).Select(x => x.STRING_VALUE).FirstOrDefaultAsync();
        }
    }
}
