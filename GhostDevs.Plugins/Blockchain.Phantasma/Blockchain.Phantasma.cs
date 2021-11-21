using System;
using GhostDevs.PluginEngine;
using Database.Main;
using Phantasma.Cryptography;
using System.Threading;
using GhostDevs.Api;
using System.Numerics;
using Serilog;
using GhostDevs.Commons;
using System.Text.Json;
using System.Net.Http;

namespace GhostDevs.Blockchain
{
    public partial class PhantasmaPlugin: Plugin, IBlockchainPlugin
    {
        private static int ChainId = 0;
        public override string Name => "PHA";
        public string[] ChainNames { get; set; }
        private bool _running = true;
        public PhantasmaPlugin()
        {
        }
        protected override void Configure()
        {
            Settings.Load(GetConfiguration());

            ChainNames = new string[] { Settings.Default.ChainName };
        }
        public void Fetch()
        {

        }
        public void Startup()
        {
            Log.Information($"{Name} plugin: Startup...");

            if (!Settings.Default.Enabled)
            {
                Log.Information($"{Name} plugin is disabled, stopping.");
                return;
            }

            Thread mainThread = new Thread(() =>
            {
                Thread.Sleep(Settings.Default.StartDelay * 1000);

                using (var databaseContext = new MainDatabaseContext())
                {
                    ChainId = ChainMethods.GetId(databaseContext, Settings.Default.ChainName);
                }

                foreach (var cInfo in Settings.Default.NFTs)
                {
                    using (var databaseContext = new MainDatabaseContext())
                    {
                        var contractId = ContractMethods.Upsert(databaseContext, cInfo.Symbol, ChainId, cInfo.Symbol, cInfo.Symbol);
                        databaseContext.SaveChanges();
                    }
                }

                // Initializing SeriesModes
                using (var databaseContext = new MainDatabaseContext())
                {
                    SeriesMethods.SeriesModesInit(databaseContext);

                    databaseContext.SaveChanges();
                }

                // Starting threads

                Thread tokensInitThread = new Thread(() =>
                {
                    while (_running)
                    {
                        try
                        {
                            InitNewTokens();

                            Thread.Sleep(Settings.Default.TokensProcessingInterval * 1000); // We process tokens every TokensProcessingInterval seconds
                        }
                        catch (Exception e)
                        {
                            LogEx.Exception("Token init", e);

                            Thread.Sleep(Settings.Default.TokensProcessingInterval * 1000);
                        }
                    }
                });
                tokensInitThread.Start();

                Thread blocksSyncThread = new Thread(() =>
                {
                    while (_running)
                    {
                        try
                        {
                            FetchBlocks();

                            Thread.Sleep(Settings.Default.BlocksProcessingInterval * 1000); // We sync blocks every BlocksProcessingInterval seconds
                        }
                        catch (Exception e)
                        {
                            LogEx.Exception("Block fetch", e);

                            Thread.Sleep(Settings.Default.BlocksProcessingInterval * 1000);
                        }
                    }
                });
                blocksSyncThread.Start();

                Thread eventsProcessThread = new Thread(() =>
                {
                    while (_running)
                    {
                        try
                        {
                            MergeSendReceiveToTransfer();

                            Thread.Sleep(Settings.Default.EventsProcessingInterval * 1000); // We process events every EventsProcessingInterval seconds
                        }
                        catch (Exception e)
                        {
                            LogEx.Exception("Events processing", e);

                            Thread.Sleep(Settings.Default.EventsProcessingInterval * 1000);
                        }
                    }
                });
                eventsProcessThread.Start();

                Thread romRamSyncThread = new Thread(() =>
                {
                    while (_running)
                    {
                        try
                        {
                            NewNftsSetRomRam();

                            Thread.Sleep(Settings.Default.RomRamProcessingInterval * 1000); // We process ROM/RAM every RomRamProcessingInterval seconds
                        }
                        catch (Exception e)
                        {
                            LogEx.Exception("ROM/RAM load", e);

                            Thread.Sleep(Settings.Default.RomRamProcessingInterval * 1000);
                        }
                    }
                });
                romRamSyncThread.Start();

                Thread seriesSyncThread = new Thread(() =>
                {
                    while (_running)
                    {
                        try
                        {
                            NewSeriesLoad();

                            Thread.Sleep(Settings.Default.SeriesProcessingInterval * 1000); // We check for new series every SeriesProcessingInterval seconds
                        }
                        catch (Exception e)
                        {
                            LogEx.Exception("Series load", e);

                            Thread.Sleep(Settings.Default.SeriesProcessingInterval * 1000);
                        }
                    }
                });
                seriesSyncThread.Start();

                Thread infusionsSyncThread = new Thread(() =>
                {
                    while (_running)
                    {
                        try
                        {
                            ProcessInfusionEvents();

                            Thread.Sleep(Settings.Default.InfusionsProcessingInterval * 1000); // We process infusion events every InfusionsProcessingInterval seconds
                        }
                        catch (Exception e)
                        {
                            LogEx.Exception("Infusions processing", e);

                            Thread.Sleep(Settings.Default.InfusionsProcessingInterval * 1000);
                        }
                    }
                });
                infusionsSyncThread.Start();

                Thread namesSyncThread = new Thread(() =>
                {
                    while (_running)
                    {
                        try
                        {
                            NameSync();

                            Thread.Sleep(Settings.Default.NamesSyncInterval * 1000); // We sync names every NamesSyncInterval seconds
                        }
                        catch (Exception e)
                        {
                            LogEx.Exception("Names sync", e);

                            Thread.Sleep(Settings.Default.NamesSyncInterval * 1000);
                        }
                    }
                });
                namesSyncThread.Start();
            });
            mainThread.Start();

            Log.Information($"{Name} plugin: Startup finished");
        }
        public void Shutdown()
        {
            Log.Information($"{Name} plugin: Shutdown command received.");
            _running = false;
        }
        public string GetCurrentOwnerAddress(string contractHash, string tokenId, out string error)
        {
            error = null;

            var url = $"{Settings.Default.GetRest()}/api/getNFT?symbol=" + contractHash + "&IDtext=" + tokenId + "&extended=true";

            var response = Client.APIRequest<JsonDocument>(url, out var stringResponse, null, 10);
            if (response == null)
            {
                return null;
            }

            if (response.RootElement.TryGetProperty("error", out var errorProperty))
                error = errorProperty.GetString();

            if (response.RootElement.TryGetProperty("ownerAddress", out var ownerAddressProperty))
                return ownerAddressProperty.GetString();

            return null;
        }

        public bool VerifySignatureAndOwnership(int chainId, string publicKey, string contractHash, string tokenId, string messageBase16, string messagePrefixBase16, string signatureBase16, out string error)
        {
            error = null;

            // Getting ownership first.
            var owner = GetCurrentOwnerAddress(contractHash, tokenId, out var getOwnerError);
            if (!String.IsNullOrEmpty(getOwnerError))
            {
                if (getOwnerError.Contains("nft does not exists"))
                {
                    error = "NFT is burned";
                    return false;
                }
                else
                {
                    error = "Owner retrieval error: " + getOwnerError;
                    return false;
                }
            }

            if (String.IsNullOrEmpty(owner))
            {
                error = "Unknown owner retrieval error";
                return false;
            }

            // publicAddress is not mandatory for phantasma,
            // but if passed - we compare it to owner that we've calculated.
            if(!string.IsNullOrEmpty(publicKey) && publicKey != owner)
            {
                error = $"Passed owner '{publicKey}' differs from a real owner '{owner}'";
                return false;
            }

            // We use owner address that we calculated ourselves.
            publicKey = owner;

            var pubKey = Phantasma.Cryptography.Address.FromText(publicKey);

            byte[] msg;
            if (!string.IsNullOrEmpty(messagePrefixBase16))
            {
                msg = Phantasma.Core.Utils.ByteArrayUtils.ConcatBytes(Phantasma.Numerics.Base16.Decode(messagePrefixBase16), Phantasma.Numerics.Base16.Decode(messageBase16));
            }
            else
            {
                msg = Phantasma.Numerics.Base16.Decode(messageBase16);
            }

            using (var stream = new System.IO.MemoryStream(Phantasma.Numerics.Base16.Decode(signatureBase16)))
            {
                using (var reader = new System.IO.BinaryReader(stream))
                {
                    var signature = reader.ReadSignature();
                    return signature.Verify(msg, pubKey);
                }
            }
        }

        public bool VerifySignature(string chainShortName, string publicKey, string messageBase16, string messagePrefixBase16,
            string signatureBase16, out string address, out string error)
        {
            var pubKey = Phantasma.Cryptography.Address.FromText(publicKey);

            byte[] msg;
            if (!string.IsNullOrEmpty(messagePrefixBase16))
            {
                msg = Phantasma.Core.Utils.ByteArrayUtils.ConcatBytes(Phantasma.Numerics.Base16.Decode(messagePrefixBase16), Phantasma.Numerics.Base16.Decode(messageBase16));
            }
            else
            {
                msg = Phantasma.Numerics.Base16.Decode(messageBase16);
            }

            using (var stream = new System.IO.MemoryStream(Phantasma.Numerics.Base16.Decode(signatureBase16)))
            {
                using (var reader = new System.IO.BinaryReader(stream))
                {
                    var signature = reader.ReadSignature();
                    error = "";
                    address = publicKey;
                    return signature.Verify(msg, pubKey);
                }
            }
        }
        
        public BigInteger GetCurrentBlockHeight(string chain)
        {
            var url = $"{Settings.Default.GetRest()}/api/getBlockHeight?chainInput=main";

            var httpClient = new HttpClient();
            var response = httpClient.GetAsync(url).Result;
            using (var content = response.Content)
            {
                var reply = content.ReadAsStringAsync().Result;
                return BigInteger.Parse(reply.Replace("\"", ""));
            }
        }
    }
}
