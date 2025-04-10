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
using Backend.Api;
using Database.Main;
using Serilog;
using ChainMethods = Database.Main.ChainMethods;
using ContractMethods = Database.Main.ContractMethods;
using NftMethods = Database.Main.NftMethods;

// ReSharper disable PossibleNullReferenceException

namespace Backend.Nft;

public class Fetch
{
    private const int NftLoadPageSize = 100; // Max number of nfts in a single query.
    private const string NtfHash = "TTRS";
    private const string ChainName = "main";
    private const string GameHash = "GAME";
    private static int _chainId;
    private static int _contractId;
    private static int _gameContractId;


    public static void Init()
    {
        using MainDbContext databaseContext = new();
        _chainId = ChainMethods.GetId(databaseContext, ChainName);
        _contractId = ContractMethods.GetId(databaseContext, _chainId, NtfHash);
        _gameContractId = ContractMethods.GetId(databaseContext, _chainId, GameHash);

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

                        if ( item is not JsonObject ) continue;

                        var itemInfo = item["item_info"];

                        var nft = NftMethods.Get(databaseContext, _chainId, _contractId, id);

                        if ( nft == null ) continue;
                        updatedNftsCount++;

                        //TODO maybe we should not do that
                        if ( ( ( string ) item["type"] )!.Contains("System object") )
                        {
                            // We found "system" NFT, which is an internal non-tradable object.
                            // We should delete it.
                            EventMethods.DeleteByNftId(databaseContext, nft.ID);
                            NftMethods.Delete(databaseContext, nft.ID);
                            Log.Information(
                                "DB: Deleting {NftSymbol} system NFT with type '{Type}'", NtfHash,
                                ( string ) item["type"]);
                        }
                        else
                        {
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
            "DB: {NftSymbol} nfts update took {ResponseTime} sec, {UpdatedNftsCount} records added",
            NtfHash, Math.Round(responseTime.TotalSeconds, 3), updatedNftsCount);
    }


    public static void LoadNfts()
    {
        const string url = "https://www.22series.com/api/store/nft";

        List<string> ids;

        using ( MainDbContext databaseContext = new() )
        {
            // Select TOKEN_IDs which have no corresponding OFFCHAIN_API_RESPONSE -> they should be loaded.
            // Also check that series != null - to avoid troubles first we deal with chain api, then with offchain api.
            ids = databaseContext.Nfts
                .Where(x => x.BURNED != true && x.ChainId == _chainId && x.ContractId == _contractId &&
                            x.Series != null && x.OFFCHAIN_API_RESPONSE == null).Select(x => x.TOKEN_ID).ToList();
        }

        if ( ids.Count == 0 ) return;

        for ( var i = 0; i < ids.Count; i += NftLoadPageSize )
        {
            var idsPage = ids.GetRange(i, Math.Min(NftLoadPageSize, ids.Count - i));
            var request = "{\"ids\":[" + "\"" + string.Join("\", \"", idsPage) + "\"" + "]}";
            var response = Client.ApiRequest<JsonNode>(url, out var stringResponse, null, 0,
                request, Client.RequestType.Post);
            if ( response == null )
            {
                Log.Error("TTRS error: Parsed response is null, raw response: '{Response}', request: {Request}", stringResponse, request);
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
                    .Where(x => x.BURNED != true && x.ChainId == _chainId && x.ContractId == _gameContractId &&
                                x.Series != null && x.OFFCHAIN_API_RESPONSE == null).Select(x => x.TOKEN_ID)
                    .FirstOrDefault();
            }

            if ( string.IsNullOrEmpty(id) ) return;

            var response = Client.ApiRequest<JsonNode>(url + id, out var stringResponse);
            if ( response == null )
            {
                Log.Error("GAME meta: null response for {ID}, returning", id);
                return;
            }

            var meta = response["meta"];

            if ( meta is not JsonObject )
            {
                Log.Error("GAME meta: null meta for {ID}, returning", id);
                return;
                //Log.Warning("GAME meta: null meta for {ID}, continue with next", id);
                //continue;
            }


            var metadataKey = ( string ) response["nfts"][0]["parsed_rom"]["metadata"];

            var metaJsonDocument = JsonDocument.Parse(meta[metadataKey].ToJsonString());

            using ( MainDbContext databaseContext = new() )
            {
                var nft = NftMethods.Get(databaseContext, _chainId, _gameContractId, id);
                nft.OFFCHAIN_API_RESPONSE = metaJsonDocument;

                databaseContext.SaveChanges();
            }
        }
    }
}
