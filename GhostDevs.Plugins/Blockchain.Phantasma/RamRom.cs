using GhostDevs.Api;
using GhostDevs.Commons;
using Database.Main;
using GhostDevs.PluginEngine;
using Phantasma.Cryptography;
using Phantasma.VM;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace GhostDevs.Blockchain
{
    public struct TokenProperty
    {
        public string Key;
        public string Value;
    }
    public partial class PhantasmaPlugin: Plugin, IBlockchainPlugin
    {
        // When we reach this number of loaded ROMs/RAMs, we save received/processed results.
        private static readonly int maxRomRamUpdatesForOneSession = 100;

        public interface IRom
        {
            string GetName();
            string GetDescription();
            Int64 GetDate();
            Phantasma.Numerics.BigInteger GetNftType();
            bool GetHasLocked();
        }
        public class CrownRom : IRom
        {
            public Phantasma.Cryptography.Address staker;
            public Int64 date;

            public CrownRom(byte[] rom)
            {
                using (var stream = new System.IO.MemoryStream(rom))
                {
                    using (var reader = new System.IO.BinaryReader(stream))
                    {
                        UnserializeData(reader);
                    }
                }
            }

            public string GetName() => "Crown NFT";
            public string GetDescription() => "";
            public Int64 GetDate() => date;
            public Phantasma.Numerics.BigInteger GetNftType() => 0;
            public bool GetHasLocked() => false;

            private void UnserializeData(System.IO.BinaryReader reader)
            {
                this.staker = reader.ReadAddress();
                this.date = reader.ReadUInt32();
            }
        }
        public class CustomRom : IRom
        {
            Dictionary<VMObject, VMObject> fields = new Dictionary<VMObject, VMObject>();

            public CustomRom(byte[] romBytes)
            {
                try
                {
                    var rom = VMObject.FromBytes(romBytes);
                    if (rom.Type == VMType.Struct)
                    {
                        fields = (Dictionary<VMObject, VMObject>)rom.Data;
                    }
                    else
                    {
                        Log.Error($"[PHA][CustomRom] Cannot parse ROM.");
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"[PHA][CustomRom] ROM parsing error: {e.ToString()}");
                }
            }

            public string GetName()
            {
                if (fields.TryGetValue(VMObject.FromObject("name"), out var value))
                {
                    return value.AsString();
                }
                return "";
            }
            public string GetDescription()
            {
                if (fields.TryGetValue(VMObject.FromObject("description"), out var value))
                {
                    return value.AsString();
                }
                return "";
            }
            public Int64 GetDate()
            {
                if (fields.TryGetValue(VMObject.FromObject("created"), out var value))
                {
                    return value.AsTimestamp().Value;
                }
                return 0;
            }
            public Phantasma.Numerics.BigInteger GetNftType()
            {
                if (fields.TryGetValue(VMObject.FromObject("typeNFT"), out var value))
                {
                    return value.AsNumber();
                }
                // TODO - remove, for old testnet NFTs
                if (fields.TryGetValue(VMObject.FromObject("type"), out value))
                {
                    return value.AsNumber();
                }
                // TODO - sounds better, no?
                if (fields.TryGetValue(VMObject.FromObject("nftType"), out value))
                {
                    return value.AsNumber();
                }
                return 0;
            }
            public bool GetHasLocked()
            {
                try
                {
                    if (fields.TryGetValue(VMObject.FromObject("hasLocked"), out var value))
                    {
                        return value.AsBool();
                    }
                }
                catch(Exception e)
                {
                    Log.Error($"[PHA][CustomRom] ROM parsing error in GetHasLocked(): {e.ToString()}");
                }
                return false;
            }
        }
        public class DummyRom : IRom
        {
            public DummyRom(byte[] rom)
            {
            }

            public string GetName() => null;
            public string GetDescription() => null;
            public Int64 GetDate() => 0;
            public Phantasma.Numerics.BigInteger GetNftType() => 0;
            public bool GetHasLocked() => false;
        }

        private static string GetPropertyValue(List<TokenProperty> properties, string key)
        {
            if (properties != null)
            {
                return properties.Where(x => x.Key.ToUpper() == key.ToUpper()).Select(x => x.Value).FirstOrDefault();
            }

            return null;
        }
        public void NewNftsSetRomRam()
        {
            int updatedNftCount;

            do
            {
                DateTime startTime = DateTime.Now;

                using (var databaseContext = new MainDbContext())
                {
                    // First we take GHOST NFTs
                    var nfts = databaseContext.Nfts.Where(x => x.ChainId == ChainId && x.ROM == null && x.BURNED != true && x.Contract.SYMBOL.ToUpper() == "GHOST").Take(maxRomRamUpdatesForOneSession).ToList();

                    // If we have available quota per iteration, adding other NFTs
                    // 0.7 to avoid doing 2nd query for just couple or so NFTs
                    if (nfts.Count() < 0.7 * maxRomRamUpdatesForOneSession)
                    {
                        var nftsOthers = databaseContext.Nfts.Where(x => x.ChainId == ChainId && x.ROM == null && x.BURNED != true && x.Contract.SYMBOL.ToUpper() != "GHOST").Take(maxRomRamUpdatesForOneSession - nfts.Count()).ToList();
                        nfts.AddRange(nftsOthers);
                    }

                    updatedNftCount = 0;
                    for (var i = 0; i < nfts.Count(); i++)
                    {
                        var nft = nfts[i];

                        var url = $"{Settings.Default.GetRest()}/api/getNFT?symbol=" + nft.Contract.SYMBOL.ToUpper() + "&IDtext=" + nft.TOKEN_ID + "&extended=true";

                        var response = Client.APIRequest<JsonDocument>(url, out var stringResponse, null, 10);
                        if (response == null)
                        {
                            Log.Warning($"[{Name}][RAM/ROM update] Response in null, raw response is {stringResponse} for {nft.Contract.SYMBOL} NFT with id {nft.TOKEN_ID}");

                            if (nft.Contract.SYMBOL.ToUpper() == "GAME")
                            {
                                // Hack for binary data inside JSON
                                var cutFrom = stringResponse.IndexOf(",{\"Key\" : \"OriginalMetadata\"");
                                stringResponse = stringResponse.Substring(0, cutFrom) + "]}";

                                if(stringResponse.Contains("\"creatorAddress\" : \"P2K9ih2iuscWbHT3eyzcjdX4UyEbyY44aJnukdQAS8C2WUY\""))
                                {
                                    // Hack for broken links inside NFT descriptions
                                    stringResponse = stringResponse.Replace("P2KJ5JxtdZcnh9EXuQ1unq3FmLZzNKgXYoTTJNVH9rSvqkL", "P2K9ih2iuscWbHT3eyzcjdX4UyEbyY44aJnukdQAS8C2WUY");
                                }

                                try
                                {
                                    response = JsonDocument.Parse(stringResponse);
                                }
                                catch (Exception e)
                                {
                                    Log.Error($"[{Name}] GAME hack parsing error:\n{e.Message}\nHacked response: {stringResponse}");
                                }
                            }

                            if (response == null)
                            {
                                continue;
                            }
                        }

                        // Following code should be called before leaving
                        // if NFT is burned,
                        // so that burned NFT has maximum info available.

                        System.Text.Json.JsonDocument responseSaved = null;
                        var error = "";
                        if (response.RootElement.TryGetProperty("error", out var errorProperty))
                        {
                            error = errorProperty.GetString();

                            using (var databaseApiCacheContext = new Database.ApiCache.ApiCacheDbContext())
                            {
                                responseSaved = Database.ApiCache.NftMethods.GetChainApiResponse(databaseApiCacheContext, Settings.Default.ChainName, nft.Contract.HASH, nft.TOKEN_ID);
                            }

                            if (responseSaved != null)
                            {
                                // Use saved api response if available
                                response = responseSaved;
                            }
                            else
                            {
                                if (error.Contains("nft does not exists"))
                                {
                                    // NFT was burned, marking it.
                                    nft.BURNED = true;
                                    nft.DM_UNIX_SECONDS = UnixSeconds.Now();
                                    Log.Warning($"[{Name}] NFT {nft.TOKEN_ID} is apparently burned.");
                                }
                                else
                                {
                                    Log.Error($"[{Name}] Request error: {error}");
                                }

                                continue;
                            }
                        }

                        nft.DM_UNIX_SECONDS = UnixSeconds.Now();

                        // Saving getNFT results for future use.
                        nft.CHAIN_API_RESPONSE = response;

                        if (responseSaved == null)
                        {
                            // We loaded it for the first time, we should save it.
                            using (var databaseUsersContext = new Database.ApiCache.ApiCacheDbContext())
                            {
                                Database.ApiCache.NftMethods.SetApiResponses(databaseUsersContext, Settings.Default.ChainName, nft.Contract.HASH, nft.TOKEN_ID, null, nft.CHAIN_API_RESPONSE, true);
                            }
                        }

                        // Reading properties
                        List<TokenProperty> properties = new List<TokenProperty>();
                        if (response.RootElement.TryGetProperty("properties", out var propertiesNode))
                        {
                            foreach (var entry in propertiesNode.EnumerateArray())
                            {
                                var property = new TokenProperty();
                                property.Key = entry.GetProperty("Key").GetString();
                                if(entry.TryGetProperty("Value", out var valueProperty))
                                    property.Value = valueProperty.GetString();
                                properties.Add(property);
                            }
                        }

                        nft.CreatorAddress = AddressMethods.Upsert(databaseContext, ChainId, response.RootElement.GetProperty("creatorAddress").GetString(), false);
                        var series = response.RootElement.GetProperty("series").GetString();
                        // TODO remove later after changing SERIES_ID type to string
                        // Hack for long series numbers (21 or 24 digits) inside GAME NFT descriptions
                        if (!String.IsNullOrEmpty(series) && series.Length < 21)
                            nft.Series = SeriesMethods.Upsert(databaseContext, nft.ContractId, series);
                        nft.MINT_NUMBER = Int32.Parse(response.RootElement.GetProperty("mint").GetString());
                        nft.ROM = response.RootElement.GetProperty("rom").GetString();
                        nft.RAM = response.RootElement.GetProperty("ram").GetString();

                        // Pasring ROM
                        IRom parsedRom = null;
                        var romBytes = Phantasma.Numerics.Base16.Decode(nft.ROM);
                        switch (nft.Contract.SYMBOL)
                        {
                            case "CROWN":
                                parsedRom = new CrownRom(romBytes);
                                break;
                            case "TTRS":
                                parsedRom = new DummyRom(romBytes);
                                break;
                            default:
                                parsedRom = new CustomRom(romBytes);
                                break;
                        }

                        // Putting all fields from ROM to - ? TODO.

                        // For cases when NFT was created not from mint event,
                        // try to get date from ROM.
                        if (nft.MINT_DATE_UNIX_SECONDS == 0)
                        {
                            var date = parsedRom.GetDate();
                            nft.MINT_DATE_UNIX_SECONDS = date;
                        }

                        nft.NAME = GetPropertyValue(properties, "name");
                        nft.DESCRIPTION = GetPropertyValue(properties, "description");
                        nft.IMAGE = GetPropertyValue(properties, "imageURL");
                        nft.INFO_URL = GetPropertyValue(properties, "infoURL");
                        
                        // Feeling Series with available information, if needed.
                        if (nft.Series != null && nft.Series.SERIES_ID != null)
                        {
                            nft.Series.CreatorAddressId = nft.CreatorAddressId;
                            nft.Series.NAME = nft.NAME;
                            nft.Series.DESCRIPTION = nft.DESCRIPTION;
                            nft.Series.IMAGE = nft.IMAGE;
                            if(Decimal.TryParse(GetPropertyValue(properties, "royalties"), out var royalties))
                                nft.Series.ROYALTIES = royalties;
                            else
                                nft.Series.ROYALTIES = 0;
                            nft.Series.TYPE = (int)parsedRom.GetNftType();

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

                        if (updatedNftCount == maxRomRamUpdatesForOneSession)
                            break;
                    }

                    try
                    {
                        if(updatedNftCount > 0)
                            databaseContext.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.Contains("Database operation expected to affect 1 row(s) but actually affected 0 row(s)."))
                        {
                            TimeSpan attemptTime = DateTime.Now - startTime;
                            Log.Warning($"[{Name}] RAM/ROM update failed because some NFTs were deleted in another thread, attempt took {Math.Round(attemptTime.TotalSeconds, 3)} sec");
                        }
                        else
                        {
                            // Unknown exception, throwing futher.
                            ExceptionDispatchInfo.Capture(ex).Throw();
                        }
                    }
                }

                TimeSpan updateTime = DateTime.Now - startTime;
                Log.Information($"[{Name}] RAM/ROM update took {Math.Round(updateTime.TotalSeconds, 3)} sec, {updatedNftCount} NFTs updated");
            }
            while (updatedNftCount > 0);
        }
    }
}
