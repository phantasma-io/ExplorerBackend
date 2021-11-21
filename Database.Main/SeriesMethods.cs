using System.Linq;

namespace Database.Main
{
    public static class SeriesMethods
    {
        public static void SeriesModesInit(MainDatabaseContext databaseContext)
        {
            var seriesModeUnique = databaseContext.SeriesModes.Where(x => x.MODE_NAME.ToUpper() == "UNIQUE").FirstOrDefault();
            if(seriesModeUnique == null)
            {
                seriesModeUnique = new SeriesMode { MODE_NAME = "UNIQUE" };
                databaseContext.SeriesModes.Add(seriesModeUnique);
                databaseContext.SaveChanges();
            }
            var seriesModeDuplicate = databaseContext.SeriesModes.Where(x => x.MODE_NAME.ToUpper() == "DUPLICATE").FirstOrDefault();
            if (seriesModeDuplicate == null)
            {
                seriesModeDuplicate = new SeriesMode { MODE_NAME = "DUPLICATED" };
                databaseContext.SeriesModes.Add(seriesModeDuplicate);
                databaseContext.SaveChanges();
            }
        }
        public static int SeriesModesGetId(MainDatabaseContext databaseContext, string name)
        {
            return databaseContext.SeriesModes.Where(x => x.MODE_NAME.ToUpper() == name.ToUpper()).First().ID;
        }

        // Checks if "Series" table has entry,
        // and adds new entry, if there's no entry available.
        // Returns new or existing entry's Id.
        public static Series Upsert(MainDatabaseContext databaseContext, int contractId, string seriesId,
            int? creatorAddressId = null,
            int? currentSupply = null, int? maxSupply = null, string seriesModeName = null, string name = null, string description = null, string image = null, decimal? royalties = null,
            string attrType1 = null, string attrValue1 = null, string attrType2 = null, string attrValue2 = null, string attrType3 = null, string attrValue3 = null)
        {
            var series = databaseContext.Serieses.Where(x => x.ContractId == contractId && x.SERIES_ID == seriesId).FirstOrDefault();

            if (series == null)
            {
                series = new Series{ ContractId = contractId, SERIES_ID = seriesId };

                databaseContext.Serieses.Add(series);
                databaseContext.SaveChanges();
            }

            if (creatorAddressId != null)
            {
                series.CreatorAddressId = (int)creatorAddressId;
            }
            if (currentSupply != null)
            {
                series.CURRENT_SUPPLY = (int)currentSupply;
            }
            if (maxSupply != null)
            {
                series.MAX_SUPPLY = (int)maxSupply;
            }
            if (!string.IsNullOrEmpty(seriesModeName))
            {
                series.SeriesModeId = SeriesModesGetId(databaseContext, seriesModeName);
            }
            if (!string.IsNullOrEmpty(name))
            {
                series.NAME = name;
            }
            if (!string.IsNullOrEmpty(description))
            {
                series.DESCRIPTION = description;
            }
            if (!string.IsNullOrEmpty(image))
            {
                series.IMAGE = image;
            }
            if (royalties != null)
            {
                series.ROYALTIES = (decimal)royalties;
            }
            if (!string.IsNullOrEmpty(attrType1))
            {
                series.ATTR_TYPE_1 = attrType1;
            }
            if (!string.IsNullOrEmpty(attrValue1))
            {
                series.ATTR_VALUE_1 = attrValue1;
            }
            if (!string.IsNullOrEmpty(attrType2))
            {
                series.ATTR_TYPE_2 = attrType2;
            }
            if (!string.IsNullOrEmpty(attrValue2))
            {
                series.ATTR_VALUE_2 = attrValue2;
            }
            if (!string.IsNullOrEmpty(attrType3))
            {
                series.ATTR_TYPE_3 = attrType3;
            }
            if (!string.IsNullOrEmpty(attrValue3))
            {
                series.ATTR_VALUE_3 = attrValue3;
            }

            return series;
        }

        public static Series Get(MainDatabaseContext databaseContext, int contractId, string seriesId)
        {
            return databaseContext.Serieses.Where(x => x.ContractId == contractId && x.SERIES_ID == seriesId).FirstOrDefault();
        }
    }
}
