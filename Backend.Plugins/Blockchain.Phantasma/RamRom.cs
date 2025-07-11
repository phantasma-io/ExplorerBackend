using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Backend.Api;
using Backend.Commons;
using Backend.PluginEngine;
using Database.Main;
using PhantasmaPhoenix.Cryptography;
using PhantasmaPhoenix.Cryptography.Extensions;
using PhantasmaPhoenix.VM;
using Serilog;
using Address = PhantasmaPhoenix.Cryptography.Address;
using Nft = Database.Main.Nft;

namespace Backend.Blockchain;

public struct TokenProperty
{
    public string Key;
    public string Value;
}

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    // When we reach this number of loaded ROMs/RAMs, we save received/processed results.
    private const int MaxRomRamUpdatesForOneSession = 100;
    private const int ChunkPerRequest = 20;


    private static string GetPropertyValue(IEnumerable<TokenProperty> properties, string key)
    {
        return properties?.Where(x => string.Equals(x.Key.ToUpper(), key.ToUpper()))
            .Select(x => x.Value).FirstOrDefault();
    }


    private void NewNftsSetRomRam(int chainId, string chainName)
    {
        int updatedNftCount;

        do
        {
            var startTime = DateTime.Now;

            using MainDbContext databaseContext = new();
            updatedNftCount = HandleSingleNft(chainId, chainName, databaseContext, startTime);
        }while ( updatedNftCount > 0 );
    }


    private int HandleSingleNft(int chainId, string chainName, MainDbContext databaseContext, DateTime startTime)
    {
        int updatedNftCount = 0;

        // First we take GHOST NFTs
        var nfts = databaseContext.Nfts
            .Where(x => x.ChainId == chainId && x.ROM == null && x.BURNED != true)
            .Take(MaxRomRamUpdatesForOneSession).ToList();
        
        foreach ( var nft in nfts )
        {
            Log.Verbose("[{Name}] checking NFT, Symbol {Symbol}, Token Id {Token}", Name, nft.Contract.SYMBOL,
                nft.TOKEN_ID);
            var url = $"{Settings.Default.GetRest()}/api/v1/getNFT?symbol=" + nft.Contract.SYMBOL.ToUpper() +
                      "&IDtext=" + nft.TOKEN_ID + "&extended=true";

            var response = Client.ApiRequest<JsonDocument>(url, out var stringResponse, null, 10);
            if ( response == null )
            {
                Log.Warning(
                    "[{Name}][RAM/ROM update] Response in null, raw response is {StringResponse} for {Symbol} NFT with id {ID}",
                    Name, stringResponse, nft.Contract.SYMBOL, nft.TOKEN_ID);

                if ( nft.Contract.SYMBOL.ToUpper() == "GAME" )
                {
                    // Hack for binary data inside JSON
                    var cutFrom = stringResponse.IndexOf(",{\"Key\" : \"OriginalMetadata\"",
                        StringComparison.InvariantCulture);
                    stringResponse = stringResponse[..cutFrom] + "]}";

                    if ( stringResponse.Contains(
                            "\"creatorAddress\" : \"P2K9ih2iuscWbHT3eyzcjdX4UyEbyY44aJnukdQAS8C2WUY\"") )
                        // Hack for broken links inside NFT descriptions
                        stringResponse = stringResponse.Replace(
                            "P2KJ5JxtdZcnh9EXuQ1unq3FmLZzNKgXYoTTJNVH9rSvqkL",
                            "P2K9ih2iuscWbHT3eyzcjdX4UyEbyY44aJnukdQAS8C2WUY");

                    try
                    {
                        response = JsonDocument.Parse(stringResponse);
                    }
                    catch ( Exception e )
                    {
                        Log.Error(
                            "[{Name}] GAME hack parsing error:\n{Message}\nHacked response: {StringResponse}",
                            Name, e.Message, stringResponse);
                    }
                }


                //would make json invalid, should be fixed on the node 
                Utils.ReplaceCharacter(ref stringResponse, ref response, @"\u000X", Name);

                if ( response == null ) continue;
            }

            // Following code should be called before leaving
            // if NFT is burned,
            // so that burned NFT has maximum info available.

            if ( response.RootElement.TryGetProperty("error", out var errorProperty) )
            {
                var error = errorProperty.GetString();

                {
                    if ( error.ToLower().Contains("invalid cast: expected") )
                    {
                        // NFT is broken, getNFT chain call is failing.
                        // TODO introduce blacklisting instead of burning.
                        nft.BURNED = true;
                        nft.DM_UNIX_SECONDS = UnixSeconds.Now();
                        Log.Error("[{Name}] NFT {ID} is broken, chain error: {Error}", Name, nft.TOKEN_ID, error);
                    }
                    else if ( error.Contains("nft does not exists") ||
                               ( error.Contains("nft") && error.Contains("does not exist") ) )
                    {
                        // NFT was burned, marking it.
                        nft.BURNED = true;
                        nft.DM_UNIX_SECONDS = UnixSeconds.Now();
                        // Log.Verbose("[{Name}] NFT {ID} is apparently burned", Name, nft.TOKEN_ID);
                    }
                    else
                        Log.Error("[{Name}] Request error: {Error}", Name, error);

                    continue;
                }
            }

            nft.DM_UNIX_SECONDS = UnixSeconds.Now();

            // Saving getNFT results for future use.
            nft.CHAIN_API_RESPONSE = response;

            // Reading properties
            List<TokenProperty> properties = new();
            if ( response.RootElement.TryGetProperty("properties", out var propertiesNode) )
                foreach ( var entry in propertiesNode.EnumerateArray() )
                {
                    TokenProperty property = new()
                    {
                        Key = entry.GetProperty("key").GetString()
                    };
                    if ( entry.TryGetProperty("value", out var valueProperty) )
                        property.Value = valueProperty.GetString();

                    properties.Add(property);
                }

            nft.CreatorAddress = AddressMethods.Upsert(databaseContext, chainId, response.RootElement.GetProperty("creatorAddress").GetString());
            var series = response.RootElement.GetProperty("series").GetString();
            // TODO remove later after changing SERIES_ID type to string
            // Hack for long series numbers (21 or 24 digits) inside GAME NFT descriptions
            if ( !string.IsNullOrEmpty(series) && series.Length < 21 )
                nft.Series = SeriesMethods.Upsert(databaseContext, nft.ContractId, series);

            nft.MINT_NUMBER = int.Parse(response.RootElement.GetProperty("mint").GetString());
            nft.ROM = response.RootElement.GetProperty("rom").GetString();
            nft.RAM = response.RootElement.GetProperty("ram").GetString();

            // Pasring ROM
            var romBytes = nft.ROM.Decode();
            IRom parsedRom = nft.Contract.SYMBOL switch
            {
                "CROWN" => new CrownRom(romBytes),
                "TTRS" => new DummyRom(romBytes),
                _ => new CustomRom(romBytes)
            };

            // Putting all fields from ROM to - ? TODO.

            // For cases when NFT was created not from mint event,
            // try to get date from ROM.
            if ( nft.MINT_DATE_UNIX_SECONDS == 0 )
            {
                var date = parsedRom.GetDate();
                nft.MINT_DATE_UNIX_SECONDS = date;
            }

            nft.NAME = GetPropertyValue(properties, "name");
            nft.DESCRIPTION = GetPropertyValue(properties, "description");
            nft.IMAGE = GetPropertyValue(properties, "imageURL");
            nft.INFO_URL = GetPropertyValue(properties, "infoURL");

            // Feeling Series with available information, if needed.
            if ( nft.Series != null && nft.Series.SERIES_ID != null )
            {
                nft.Series.CreatorAddressId = nft.CreatorAddressId;
                nft.Series.NAME = nft.NAME;
                nft.Series.DESCRIPTION = nft.DESCRIPTION;
                nft.Series.IMAGE = nft.IMAGE;
                nft.Series.ROYALTIES =
                    decimal.TryParse(GetPropertyValue(properties, "royalties"), out var royalties)
                        ? royalties
                        : 0;

                nft.Series.TYPE = ( int ) parsedRom.GetNftType();

                nft.Series.ATTR_TYPE_1 = GetPropertyValue(properties, "attrT1");
                nft.Series.ATTR_VALUE_1 = GetPropertyValue(properties, "attrV1");
                nft.Series.ATTR_TYPE_2 = GetPropertyValue(properties, "attrT2");
                nft.Series.ATTR_VALUE_2 = GetPropertyValue(properties, "attrV2");
                nft.Series.ATTR_TYPE_3 = GetPropertyValue(properties, "attrT3");
                nft.Series.ATTR_VALUE_3 = GetPropertyValue(properties, "attrV3");
                nft.Series.HAS_LOCKED = parsedRom.GetHasLocked();

                nft.Series.DM_UNIX_SECONDS = UnixSeconds.Now();
            }

            updatedNftCount++;

            if ( updatedNftCount == MaxRomRamUpdatesForOneSession ) break;
        }

        try
        {
            databaseContext.SaveChanges();
        }
        catch ( Exception ex )
        {
            if ( ex.Message.Contains(
                    "Database operation expected to affect 1 row(s) but actually affected 0 row(s).") )
            {
                var attemptTime = DateTime.Now - startTime;
                Log.Warning(
                    "[{Name}] RAM/ROM update failed because some NFTs were deleted in another thread, attempt took {AttemptTime} sec",
                    Name, Math.Round(attemptTime.TotalSeconds, 3));
            }
            else
                // Unknown exception, throwing futher.
                ExceptionDispatchInfo.Capture(ex).Throw();
        }
        
        var updateTime = DateTime.Now - startTime;
        if(updateTime.TotalSeconds > 1 || updatedNftCount > 0)
        {
            Log.Information(
                "[{Name}] RAM/ROM update took {UpdateTime} sec, {UpdatedNftCount} NFTs updated",
                Name, Math.Round(updateTime.TotalSeconds, 3), updatedNftCount);
        }
        
        return updatedNftCount;
    }


    private List<Nft> GetNFTs(int chainId, string chainName, MainDbContext databaseContext)
    {
        var nfts = databaseContext.Nfts
            .Where(x => x.ChainId == chainId && x.ROM == null && x.BURNED != true
                /*x.Contract.SYMBOL.ToUpper() == "GHOST"*/).Take(MaxRomRamUpdatesForOneSession).ToList();

        // If we have available quota per iteration, adding other NFTs
        // 0.7 to avoid doing 2nd query for just couple or so NFTs
        if ( nfts.Count < 0.7 * MaxRomRamUpdatesForOneSession )
        {
            var nftsOthers = databaseContext.Nfts
                .Where(x => x.ChainId == chainId && x.ROM == null && x.BURNED != true
                    /*x.Contract.SYMBOL.ToUpper() != "GHOST"*/)
                .Take(MaxRomRamUpdatesForOneSession - nfts.Count).ToList();
            nfts.AddRange(nftsOthers);
        }

        return nfts;
    }


    private void HandleNullResponse(JsonDocument response, string stringResponse, string symbol, string nftIDs)
    {
        if ( response == null )
        {
            Log.Warning(
                "[{Name}][RAM/ROM update] Response in null, raw response is {StringResponse} for {Symbol} NFT with id {ID}",
                Name, stringResponse, symbol, nftIDs);

            if ( symbol.ToUpper() == "GAME" )
            {
                // Hack for binary data inside JSON
                var cutFrom = stringResponse.IndexOf(",{\"Key\" : \"OriginalMetadata\"",
                    StringComparison.InvariantCulture);
                stringResponse = stringResponse[..cutFrom] + "]}";

                if ( stringResponse.Contains(
                        "\"creatorAddress\" : \"P2K9ih2iuscWbHT3eyzcjdX4UyEbyY44aJnukdQAS8C2WUY\"") )
                    // Hack for broken links inside NFT descriptions
                    stringResponse = stringResponse.Replace(
                        "P2KJ5JxtdZcnh9EXuQ1unq3FmLZzNKgXYoTTJNVH9rSvqkL",
                        "P2K9ih2iuscWbHT3eyzcjdX4UyEbyY44aJnukdQAS8C2WUY");

                try
                {
                    response = JsonDocument.Parse(stringResponse);
                }
                catch ( Exception e )
                {
                    Log.Error(
                        "[{Name}] GAME hack parsing error:\n{Message}\nHacked response: {StringResponse}",
                        Name, e.Message, stringResponse);
                }
            }


            //would make json invalid, should be fixed on the node 
            Utils.ReplaceCharacter(ref stringResponse, ref response, @"\u000X", Name);
        }
    }
    
    private void HandleMultipleNFTs(int chainId, string chainName, MainDbContext databaseContext, DateTime startTime, ref int updatedNftCount)
    {
        var nfts = GetNFTs(chainId, chainName, databaseContext);
        

        updatedNftCount = 0;
        List<string> contractSymbols =  nfts.Select(nft => nft.Contract.SYMBOL).Distinct().ToList();
        foreach ( var symbol in contractSymbols )
        {
            var numNFTs = nfts.Where(nft => nft.Contract.SYMBOL.ToUpper().Equals(symbol.ToUpper())).Count();
            for ( int i = 0; i < numNFTs / ChunkPerRequest; i++ )
            {
                var chunkArray = nfts.Where(nft => nft.Contract.SYMBOL.ToUpper().Equals(symbol.ToUpper())).Chunk(ChunkPerRequest);
                string requestIDs = string.Join(",", chunkArray);
                Log.Verbose("[{Name}] checking NFTs, Symbol {Symbol}, Token Id {Token}", Name, symbol, requestIDs);
                var url = $"{Settings.Default.GetRest()}/api/v1/getNFTs?symbol=" + symbol.ToUpper() +
                          "&IDtext=" + requestIDs + "&extended=true";
            
                var response = Client.ApiRequest<JsonDocument>(url, out var stringResponse, null, 10);
                HandleNullResponse(response, stringResponse, symbol, requestIDs);
                
                
            }
        }
        
    }


    public interface IRom
    {
        string GetName();
        string GetDescription();
        long GetDate();
        BigInteger GetNftType();
        bool GetHasLocked();
    }

    public class CrownRom : IRom
    {
        private long _date;
        private Address _staker;


        public CrownRom(byte[] rom)
        {
            using MemoryStream stream = new(rom);
            using BinaryReader reader = new(stream);
            UnserializeData(reader);
        }


        public string GetName()
        {
            return "Crown NFT";
        }


        public string GetDescription()
        {
            return "";
        }


        public long GetDate()
        {
            return _date;
        }


        public BigInteger GetNftType()
        {
            return 0;
        }


        public bool GetHasLocked()
        {
            return false;
        }


        private void UnserializeData(BinaryReader reader)
        {
            _staker = reader.ReadAddress();
            _date = reader.ReadUInt32();
        }
    }

    public class CustomRom : IRom
    {
        private readonly Dictionary<VMObject, VMObject> _fields = new();


        public CustomRom(byte[] romBytes)
        {
            try
            {
                var rom = VMObject.FromBytes(romBytes);
                if ( rom.Type == VMType.Struct )
                    _fields = ( Dictionary<VMObject, VMObject> ) rom.Data;
                else
                    Log.Error("[PHA][CustomRom] Cannot parse ROM");
            }
            catch ( Exception e )
            {
                Log.Error("[PHA][CustomRom] ROM parsing error: {Exception}", e.Message);
            }
        }


        public string GetName()
        {
            return _fields.TryGetValue(VMObject.FromObject("name"), out var value) ? value.AsString() : "";
        }


        public string GetDescription()
        {
            return _fields.TryGetValue(VMObject.FromObject("description"), out var value) ? value.AsString() : "";
        }


        public long GetDate()
        {
            return _fields.TryGetValue(VMObject.FromObject("created"), out var value)
                ? value.AsTimestamp().Value
                : 0;
        }


        public BigInteger GetNftType()
        {
            if ( _fields.TryGetValue(VMObject.FromObject("typeNFT"), out var value) ) return value.AsNumber();

            // TODO - remove, for old testnet NFTs
            if ( _fields.TryGetValue(VMObject.FromObject("type"), out value) ) return value.AsNumber();

            // TODO - sounds better, no?
            return _fields.TryGetValue(VMObject.FromObject("nftType"), out value) ? value.AsNumber() : 0;
        }


        public bool GetHasLocked()
        {
            try
            {
                if ( _fields.TryGetValue(VMObject.FromObject("hasLocked"), out var value) )
                {
                    Log.Verbose("[PHA][CustomRom] ROM hasLocked {Value}", value);
                    //TODO maybe fix
                    //return value.GetType() == typeof(String) ? value.ToString().Equals("1") : value.AsBool();
                    return value.ToString().Equals("1");
                }
            }
            catch ( Exception e )
            {
                Log.Error("[PHA][CustomRom] ROM parsing error in GetHasLocked(): {Exception}", e.Message);
            }

            return false;
        }
    }

    public class DummyRom : IRom
    {
        public DummyRom(byte[] rom)
        {
        }


        public string GetName()
        {
            return null;
        }


        public string GetDescription()
        {
            return null;
        }


        public long GetDate()
        {
            return 0;
        }


        public BigInteger GetNftType()
        {
            return 0;
        }


        public bool GetHasLocked()
        {
            return false;
        }
    }
}
