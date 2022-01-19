// NFT details types and samples:
// ------------------------------
// This stored in main NFT table:
// string   Id; // Main NFT id
// int      "item": "371"
// string   "item_info"->"name_english": "Akuna Front Spoiler (Carbon Fibre)"
// string   "item_info"->"description_english": "Make: Kaya<br/>Model: Akuna<br/>Part: Aluminium Front Spoiler<br/>Aerodynamic Adjustable<br/>Finish: Clear (High Gloss)<br/>Part No: KA-3301-AERO-SP-FR-Carbon-Fibre"
// string   "url": "https://www.22series.com/api/store/part_info?id=371"
// string   "img": "https://www.22series.com/api/store/part_img?id=371"
// int      "mint": 3
// DateTime "timestamp": 1581797657
// int      "item_info"->"mint_limit": 0
// 
// This stored in NFT details table:
// string   "type": "Item"
// int      "source": 0
// int      "source_data": 2
// string   "item_info"->"make": "Kaya"
// string   "item_info"->"model": "Akuna"
// string   "item_info"->"part": "Front Spoiler"
// string   "item_info"->"material": "Aluminium"
// string   [duplicate] "item_info"->"image_url": "https://www.22series.com/api/store/part_img?id=371"
// string   "item_info"->"display_type_english": "Part"
// int      [duplicate] "item_info"->"itemdefid": 371
// int      "item_info"->"season": 1
// int      "item_info"->"rarity": 3
// string   "item_info"->"body_part": "AeroSpoilerFront"
// string   "item_info"->"model_asset": "ka-3301-aero-sp-fr-carbon-fibre"
// string   "item_info"->"type": "kaya akuna"
// string   "item_info"->"parent_types": "kaya akuna"
// string   "item_info"->"series": ""
// string   "item_info"->"extra": "Aerodynamic Adjustable"
// string   "item_info"->"color": "Clear"
// string   "item_info"->"finish": "High Gloss"

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Database.ApiCache;
using Database.Main;
using GhostDevs.Api;
using Serilog;
using ChainMethods = Database.Main.ChainMethods;
using ContractMethods = Database.Main.ContractMethods;
using NftMethods = Database.Main.NftMethods;

namespace GhostDevs.Nft;

public class Fetch
{
    private static readonly int NftLoadPageSize = 100; // Max number of nfts in a single query.
    private static int ChainId;
    private static readonly string nftSymbol = "TTRS";
    private static int ContractId;
    private static int GameContractId;


    public static void Init()
    {
        using MainDbContext databaseContext = new();
        ChainId = ChainMethods.GetId(databaseContext, "Phantasma");
        ContractId = ContractMethods.GetId(databaseContext, ChainId, "TTRS");
        GameContractId = ContractMethods.GetId(databaseContext, ChainId, "GAME");

        databaseContext.SaveChanges();
    }


    private static void LoadStoreNftFromDataNode(List<string> ids, JsonNode storeNft)
    {
        if ( storeNft == null ) return;

        var startTime = DateTime.Now;
        var updatedNftsCount = 0;

        using ( MainDbContext databaseContext = new() )
        {
            databaseContext.ChangeTracker.AutoDetectChangesEnabled = false;

            using ( var transaction = databaseContext.Database.BeginTransaction() )
            {
                try
                {
                    foreach ( var id in ids )
                    {
                        var item = storeNft[id];
                        if ( item == null ) continue;
                        var itemInfo = item["item_info"];

                        var nft = NftMethods.Get(databaseContext, ChainId, ContractId, id);

                        if ( nft == null ) continue;
                        updatedNftsCount++;

                        if ( ( ( string ) item["type"] )!.Contains("System object") )
                        {
                            // We found "system" NFT, which is an internal non-tradable object.
                            // We should delete it.
                            EventMethods.DeleteByNftId(databaseContext, nft.ID, false);
                            NftMethods.Delete(databaseContext, nft.ID);
                            Log.Information(
                                "DB: Deleting {NftSymbol} system NFT with type '{Type}'", nftSymbol,
                                ( string ) item["type"]);
                        }
                        else
                        {
                            // For TTRS we only save API responses for backup purposes,
                            // we don't use them on database resync.

                            using ApiCacheDbContext databaseApiCacheContext = new();
                            Database.ApiCache.NftMethods.SetApiResponses(databaseApiCacheContext, "main",
                                "TTRS", nft.TOKEN_ID, JsonDocument.Parse(item.ToJsonString()), null, true);


                            if ( itemInfo != null )
                                NftMetadataMethods.Set(databaseContext,
                                    nft,
                                    0,
                                    null,
                                    null,
                                    ( string ) itemInfo["description_english"],
                                    ( string ) itemInfo["name_english"],
                                    ( string ) item["img"],
                                    ( int ) item["timestamp"],
                                    ( int ) item["mint"],
                                    JsonDocument.Parse(item.ToJsonString()),
                                    false);
                        }

                        // else - NFT is probably deleted by burn mechanism.
                    }

                    databaseContext.ChangeTracker.DetectChanges();
                    databaseContext.SaveChanges();
                    transaction.Commit();
                }
                catch ( Exception ex )
                {
                    transaction.Rollback();
                    ExceptionDispatchInfo.Capture(ex).Throw();
                }
            }
        }

        var responseTime = DateTime.Now - startTime;

        Log.Information(
            "DB: {nftSymbol} nfts update took {ResponseTime} sec, {UpdatedNftsCount} records added",
            nftSymbol, Math.Round(responseTime.TotalSeconds, 3), updatedNftsCount);
    }


    public static void LoadNfts()
    {
        const string url = "https://www.22series.com/api/store/nft";

        List<string> ids = null;

        using ( MainDbContext databaseContext = new() )
        {
            // Select TOKEN_IDs which have no corresponding OFFCHAIN_API_RESPONSE -> they should be loaded.
            // Also check that series != null - to avoid troubles first we deal with chain api, then with offchain api.
            ids = databaseContext.Nfts
                .Where(x => x.BURNED != true && x.ChainId == ChainId && x.ContractId == ContractId &&
                            x.Series != null && x.OFFCHAIN_API_RESPONSE == null).Select(x => x.TOKEN_ID).ToList();
        }

        if ( ids.Count == 0 ) return;

        for ( var i = 0; i < ids.Count; i += NftLoadPageSize )
        {
            var idsPage = ids.GetRange(i, Math.Min(NftLoadPageSize, ids.Count - i));
            var response = Client.APIRequest<JsonNode>(url, out var stringResponse, null, 0,
                "{\"ids\":[" + "\"" + string.Join("\", \"", idsPage) + "\"" + "]}", Client.RequestType.POST);
            if ( response == null )
            {
                Log.Error("TTRS error: Parsed response is null, raw response: '{Response}'", stringResponse);
                return;
            }

            LoadStoreNftFromDataNode(idsPage, response);
        }
    }


    // TODO this is one big bs hacky workaround for pha bug.
    public static void LoadGAMENfts()
    {
        const string url = "https://pavillionhub.com/api/nft_data?phantasma_ids=1&token=GAME&meta=1&ids=";

        for ( var i = 0; i < 1000; i += 1 )
        {
            string id;
            using ( MainDbContext databaseContext = new() )
            {
                // Select TOKEN_IDs which have no corresponding OFFCHAIN_API_RESPONSE -> they should be loaded.
                // Also check that series != null - to avoid troubles first we deal with chain api, then with offchain api.
                id = databaseContext.Nfts
                    .Where(x => x.BURNED != true && x.ChainId == ChainId && x.ContractId == GameContractId &&
                                x.Series != null && x.OFFCHAIN_API_RESPONSE == null).Select(x => x.TOKEN_ID)
                    .FirstOrDefault();
            }

            if ( string.IsNullOrEmpty(id) ) return;

            var response = Client.APIRequest<JsonNode>(url + id, out var stringResponse);
            if ( response == null )
            {
                Log.Error("GAME meta: null response for {ID}, returning", id);
                return;
            }

            var meta = response["meta"];
            if ( meta == null )
            {
                Log.Error("GAME meta: null meta for {ID}, returning", id);
                return;
            }

            // ReSharper disable once PossibleNullReferenceException
            var metadataKey = ( string ) response["nfts"][0]["parsed_rom"]["metadata"];

            // ReSharper disable once PossibleNullReferenceException
            var metaJsonDocument = JsonDocument.Parse(meta[metadataKey].ToJsonString());

            using ( ApiCacheDbContext databaseApiCacheContext = new() )
            {
                Database.ApiCache.NftMethods.SetApiResponses(databaseApiCacheContext, "main", "GAME", id,
                    metaJsonDocument, null, true);
            }

            using ( MainDbContext databaseContext = new() )
            {
                var nft = NftMethods.Get(databaseContext, ChainId, GameContractId, id);
                nft.OFFCHAIN_API_RESPONSE = metaJsonDocument;

                databaseContext.SaveChanges();
            }
        }
    }
}
