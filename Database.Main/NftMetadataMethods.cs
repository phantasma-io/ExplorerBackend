using System.Linq;
using System.Text.Json;
using GhostDevs.Commons;

namespace Database.Main;

public static class NftMetadataMethods
{
    // Checks if "Nfts" table has entry with given NFT id,
    // and updates NFT's metadata.
    public static void Set(MainDbContext databaseContext, Nft nft, int nftId, string rom, string ram,
        string description, string name, string image, long mintDateUnixSeconds, int mintNumber,
        JsonDocument extendedProperties, bool saveChanges = true)
    {
        nft ??= databaseContext.Nfts.FirstOrDefault(x => x.ID == nftId);

        if ( nft == null ) return;

        nft.DM_UNIX_SECONDS = UnixSeconds.Now();

        if ( !string.IsNullOrEmpty(rom) ) nft.ROM = rom;

        if ( !string.IsNullOrEmpty(ram) ) nft.RAM = ram;

        if ( !string.IsNullOrEmpty(description) ) nft.DESCRIPTION = description;

        if ( !string.IsNullOrEmpty(name) ) nft.NAME = name;

        if ( !string.IsNullOrEmpty(image) ) nft.IMAGE = image;

        if ( mintDateUnixSeconds > 0 ) nft.MINT_DATE_UNIX_SECONDS = mintDateUnixSeconds;

        if ( mintNumber > 0 ) nft.MINT_NUMBER = mintNumber;

        if ( extendedProperties != null ) nft.OFFCHAIN_API_RESPONSE = extendedProperties;

        if ( saveChanges ) databaseContext.SaveChanges();
    }
}
