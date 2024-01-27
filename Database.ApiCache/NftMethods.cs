using System.Linq;
using System.Text.Json;
using Backend.Commons;

namespace Database.ApiCache;

public static class NftMethods
{
    // Checks if "Nft" table has entry with given contract/token id,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry.
    private static Nft Upsert(ApiCacheDbContext databaseContext, Contract contract, string tokenId,
        out bool newNftCreated)
    {
        newNftCreated = false;

        var nft = databaseContext.Nfts.FirstOrDefault(x => x.Contract == contract && x.TOKEN_ID == tokenId);
        if ( nft != null ) return nft;
        nft = new Nft {Contract = contract, TOKEN_ID = tokenId};
        databaseContext.Nfts.Add(nft);

        newNftCreated = true;

        return nft;
    }


    public static JsonDocument GetOffchainApiResponse(ApiCacheDbContext databaseContext, string chainShortName,
        string contractHash, string tokenId)
    {
        var contract = ContractMethods.Get(databaseContext, chainShortName, contractHash);

        var nft = databaseContext.Nfts.FirstOrDefault(x => x.Contract == contract && x.TOKEN_ID == tokenId);
        return nft?.OFFCHAIN_API_RESPONSE;
    }


    public static JsonDocument GetChainApiResponse(ApiCacheDbContext databaseContext, string chainShortName,
        string contractHash, string tokenId)
    {
        var contract = ContractMethods.Get(databaseContext, chainShortName, contractHash);

        var nft = databaseContext.Nfts.FirstOrDefault(x => x.Contract == contract && x.TOKEN_ID == tokenId);
        return nft?.CHAIN_API_RESPONSE;
    }


    public static void SetApiResponses(ApiCacheDbContext databaseContext, string chainShortName, string contractHash,
        string tokenId, JsonDocument offchainApiResponse, JsonDocument chainApiResponse)
    {
        var chain = ChainMethods.Upsert(databaseContext, chainShortName);
        var contract = ContractMethods.Upsert(databaseContext, chain, contractHash);

        var nft = Upsert(databaseContext, contract, tokenId, out var newNftCreated);

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
    }
}
