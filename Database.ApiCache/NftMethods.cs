using System.Linq;
using System.Text.Json;
using GhostDevs.Commons;

namespace Database.ApiCache;

public static class NftMethods
{
    // Checks if "Nft" table has entry with given contract/token id,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry.
    public static Nft Upsert(ApiCacheDbContext databaseContext, int contractId, string tokenId, out bool newNftCreated)
    {
        newNftCreated = false;

        var nft = databaseContext.Nfts.FirstOrDefault(x => x.ContractId == contractId && x.TOKEN_ID == tokenId);
        if ( nft == null )
        {
            nft = new Nft {ContractId = contractId, TOKEN_ID = tokenId};
            databaseContext.Nfts.Add(nft);

            newNftCreated = true;
        }

        return nft;
    }


    public static JsonDocument GetOffchainApiResponse(ApiCacheDbContext databaseContext, string chainShortName,
        string contractHash, string tokenId)
    {
        var contractId = ContractMethods.GetId(databaseContext, chainShortName, contractHash);

        var nft = databaseContext.Nfts.Where(x => x.ContractId == contractId && x.TOKEN_ID == tokenId).FirstOrDefault();
        if ( nft != null ) return nft.OFFCHAIN_API_RESPONSE;

        return null;
    }


    public static JsonDocument GetChainApiResponse(ApiCacheDbContext databaseContext, string chainShortName,
        string contractHash, string tokenId)
    {
        var contractId = ContractMethods.GetId(databaseContext, chainShortName, contractHash);

        var nft = databaseContext.Nfts.Where(x => x.ContractId == contractId && x.TOKEN_ID == tokenId).FirstOrDefault();
        if ( nft != null ) return nft.CHAIN_API_RESPONSE;

        return null;
    }


    public static void SetApiResponses(ApiCacheDbContext databaseContext, string chainShortName, string contractHash,
        string tokenId, JsonDocument offchainApiResponse, JsonDocument chainApiResponse, bool saveChanges = false)
    {
        var chainId = ChainMethods.Upsert(databaseContext, chainShortName);
        var contractId = ContractMethods.Upsert(databaseContext, chainId, contractHash);

        var nft = Upsert(databaseContext, contractId, tokenId, out var newNftCreated);

        if ( offchainApiResponse != null )
        {
            nft.OFFCHAIN_API_RESPONSE = offchainApiResponse;
            nft.OFFCHAIN_API_RESPONSE_DM_UNIX_SECONDS = UnixSeconds.Now();
        }

        if ( chainApiResponse != null )
        {
            nft.CHAIN_API_RESPONSE = chainApiResponse;
            nft.CHAIN_API_RESPONSE_DM_UNIX_SECONDS = UnixSeconds.Now();
        }

        if ( saveChanges ) databaseContext.SaveChanges();
    }
}
