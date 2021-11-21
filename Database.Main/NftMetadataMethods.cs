using GhostDevs.Commons;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Database.Main
{
    public static class NftMetadataMethods
    {
        // Checks if "Nfts" table has entry with given NFT id,
        // and updates NFT's metadata.
        public static void Set(MainDbContext databaseContext, Nft nft, int nftId, string rom, string ram, string description, string name, string image, Int64 mintDateUnixSeconds, int mintNumber, System.Text.Json.JsonDocument extendedProperties, bool saveChanges = true)
        {
            if(nft == null)
                nft = databaseContext.Nfts.Where(x => x.ID == nftId).FirstOrDefault();

            if (nft == null)
                return;

            nft.DM_UNIX_SECONDS = UnixSeconds.Now();

            if (!String.IsNullOrEmpty(rom))
                nft.ROM = rom;
            if (!String.IsNullOrEmpty(ram))
                nft.RAM = ram;
            if (!String.IsNullOrEmpty(description))
                nft.DESCRIPTION = description;
            if (!String.IsNullOrEmpty(name))
                nft.NAME = name;
            if (!String.IsNullOrEmpty(image))
                nft.IMAGE = image;
            if (mintDateUnixSeconds > 0)
                nft.MINT_DATE_UNIX_SECONDS = mintDateUnixSeconds;
            if (mintNumber > 0)
                nft.MINT_NUMBER = mintNumber;
            if (extendedProperties != null)
                nft.OFFCHAIN_API_RESPONSE = extendedProperties;

            if (saveChanges)
                databaseContext.SaveChanges();
        }
    }
}
