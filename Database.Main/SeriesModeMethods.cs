using System.Linq;

namespace Database.Main;

public static class SeriesModeMethods
{
    public static SeriesMode Upsert(MainDbContext databaseContext, string name, bool saveChanges = true)
    {
        var seriesMode = databaseContext.SeriesModes.FirstOrDefault(x => x.MODE_NAME == name);
        if ( seriesMode != null ) return seriesMode;

        seriesMode = DbHelper.GetTracked<SeriesMode>(databaseContext)
            .FirstOrDefault(x => x.MODE_NAME == name);
        if ( seriesMode != null ) return seriesMode;


        seriesMode = new SeriesMode {MODE_NAME = name};

        databaseContext.SeriesModes.Add(seriesMode);
        if ( saveChanges ) databaseContext.SaveChanges();

        return seriesMode;
    }


    public static SeriesMode Get(MainDbContext databaseContext, string name)
    {
        return databaseContext.SeriesModes.FirstOrDefault(x => x.MODE_NAME == name);
    }
}
