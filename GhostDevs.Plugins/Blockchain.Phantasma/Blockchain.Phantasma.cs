using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using Database.Main;
using GhostDevs.Api;
using GhostDevs.Commons;
using GhostDevs.PluginEngine;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Serilog;
using Address = Phantasma.Cryptography.Address;
using BigInteger = System.Numerics.BigInteger;

namespace GhostDevs.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    private static List<Chain> ChainList;
    private bool _running = true;
    public override string Name => "PHA";
    public string[] ChainNames { get; private set; }


    public void Fetch()
    {
    }


    public void Startup()
    {
        Log.Information("{Name} plugin: Startup...", Name);

        if ( !Settings.Default.Enabled )
        {
            Log.Information("{Name} plugin is disabled, stopping", Name);
            return;
        }

        Thread mainThread = new(() =>
        {
            Thread.Sleep(Settings.Default.StartDelay * 1000);

            //starting chain thread

            using ( MainDbContext databaseContext = new() )
            {
                InitChains();

                ChainList = ChainMethods.GetChains(databaseContext).ToList();
                ChainNames = ChainMethods.getChainNames(databaseContext).ToArray();
            }

            using ( MainDbContext databaseContext = new() )
            {
                SeriesMethods.SeriesModesInit(databaseContext);

                databaseContext.SaveChanges();
            }

            Log.Verbose("[{Name}] got {ChainCount} Chains, get to work", Name, ChainList.Count);
            foreach ( var chain in ChainList )
            {
                Log.Information("[{Name}] starting with Chain {ChainName} and Internal Id {Id}", Name, chain.NAME,
                    chain.ID);

                //TODO replace
                foreach ( var cInfo in Settings.Default.NFTs )
                {
                    using MainDbContext databaseContext = new();
                    var contractId =
                        ContractMethods.Upsert(databaseContext, cInfo.Symbol, chain.ID, cInfo.Symbol, cInfo.Symbol);
                    databaseContext.SaveChanges();
                }

                Thread tokensInitThread = new(() =>
                {
                    while ( _running )
                        try
                        {
                            InitNewTokens(chain.ID);

                            Thread.Sleep(Settings.Default.TokensProcessingInterval *
                                         1000); // We process tokens every TokensProcessingInterval seconds
                        }
                        catch ( Exception e )
                        {
                            LogEx.Exception("Token init", e);

                            Thread.Sleep(Settings.Default.TokensProcessingInterval * 1000);
                        }
                });
                tokensInitThread.Start();

                Thread blocksSyncThread = new(() =>
                {
                    while ( _running )
                        try
                        {
                            FetchBlocks(chain.ID, chain.NAME);

                            Thread.Sleep(Settings.Default.BlocksProcessingInterval *
                                         1000); // We sync blocks every BlocksProcessingInterval seconds
                        }
                        catch ( Exception e )
                        {
                            LogEx.Exception("Block fetch", e);

                            Thread.Sleep(Settings.Default.BlocksProcessingInterval * 1000);
                        }
                });
                blocksSyncThread.Start();

                Thread eventsProcessThread = new(() =>
                {
                    while ( _running )
                        try
                        {
                            MergeSendReceiveToTransfer(chain.ID);

                            Thread.Sleep(Settings.Default.EventsProcessingInterval *
                                         1000); // We process events every EventsProcessingInterval seconds
                        }
                        catch ( Exception e )
                        {
                            LogEx.Exception("Events processing", e);

                            Thread.Sleep(Settings.Default.EventsProcessingInterval * 1000);
                        }
                });
                eventsProcessThread.Start();

                Thread romRamSyncThread = new(() =>
                {
                    while ( _running )
                        try
                        {
                            NewNftsSetRomRam(chain.ID, chain.NAME);

                            Thread.Sleep(Settings.Default.RomRamProcessingInterval *
                                         1000); // We process ROM/RAM every RomRamProcessingInterval seconds
                        }
                        catch ( Exception e )
                        {
                            LogEx.Exception("ROM/RAM load", e);

                            Thread.Sleep(Settings.Default.RomRamProcessingInterval * 1000);
                        }
                });
                romRamSyncThread.Start();

                Thread seriesSyncThread = new(() =>
                {
                    while ( _running )
                        try
                        {
                            NewSeriesLoad(chain.ID);

                            Thread.Sleep(Settings.Default.SeriesProcessingInterval *
                                         1000); // We check for new series every SeriesProcessingInterval seconds
                        }
                        catch ( Exception e )
                        {
                            LogEx.Exception("Series load", e);

                            Thread.Sleep(Settings.Default.SeriesProcessingInterval * 1000);
                        }
                });
                seriesSyncThread.Start();

                Thread infusionsSyncThread = new(() =>
                {
                    while ( _running )
                        try
                        {
                            ProcessInfusionEvents(chain.ID);

                            Thread.Sleep(Settings.Default.InfusionsProcessingInterval *
                                         1000); // We process infusion events every InfusionsProcessingInterval seconds
                        }
                        catch ( Exception e )
                        {
                            LogEx.Exception("Infusions processing", e);

                            Thread.Sleep(Settings.Default.InfusionsProcessingInterval * 1000);
                        }
                });
                infusionsSyncThread.Start();


                Thread namesSyncThread = new(() =>
                {
                    while ( _running )
                        try
                        {
                            NameSync(chain.ID);

                            Thread.Sleep(Settings.Default.NamesSyncInterval *
                                         1000); // We sync names every NamesSyncInterval seconds
                        }
                        catch ( Exception e )
                        {
                            LogEx.Exception("Names sync", e);

                            Thread.Sleep(Settings.Default.NamesSyncInterval * 1000);
                        }
                });
                namesSyncThread.Start();
            }
        });
        mainThread.Start();

        Log.Information("{Name} plugin: Startup finished", Name);
    }


    public void Shutdown()
    {
        Log.Information("{Name} plugin: Shutdown command received", Name);
        _running = false;
    }


    public bool VerifySignatureAndOwnership(int chainId, string publicKey, string contractHash, string tokenId,
        string messageBase16, string messagePrefixBase16, string signatureBase16, out string error)
    {
        error = null;

        // Getting ownership first.
        var owner = GetCurrentOwnerAddress(contractHash, tokenId, out var getOwnerError);
        if ( !string.IsNullOrEmpty(getOwnerError) )
        {
            if ( getOwnerError.Contains("nft does not exists") )
            {
                error = "NFT is burned";
                return false;
            }

            error = "Owner retrieval error: " + getOwnerError;
            return false;
        }

        if ( string.IsNullOrEmpty(owner) )
        {
            error = "Unknown owner retrieval error";
            return false;
        }

        // publicAddress is not mandatory for phantasma,
        // but if passed - we compare it to owner that we've calculated.
        if ( !string.IsNullOrEmpty(publicKey) && publicKey != owner )
        {
            error = $"Passed owner '{publicKey}' differs from a real owner '{owner}'";
            return false;
        }

        // We use owner address that we calculated ourselves.
        publicKey = owner;

        var pubKey = Address.FromText(publicKey);

        byte[] msg;
        if ( !string.IsNullOrEmpty(messagePrefixBase16) )
            msg = ByteArrayUtils.ConcatBytes(messagePrefixBase16.Decode(), messageBase16.Decode());
        else
            msg = messageBase16.Decode();

        using MemoryStream stream = new(signatureBase16.Decode());
        using BinaryReader reader = new(stream);
        var signature = reader.ReadSignature();
        return signature.Verify(msg, pubKey);
    }


    public bool VerifySignature(string chainShortName, string publicKey, string messageBase16,
        string messagePrefixBase16,
        string signatureBase16, out string address, out string error)
    {
        var pubKey = Address.FromText(publicKey);

        byte[] msg;
        if ( !string.IsNullOrEmpty(messagePrefixBase16) )
            msg = ByteArrayUtils.ConcatBytes(messagePrefixBase16.Decode(), messageBase16.Decode());
        else
            msg = messageBase16.Decode();

        using MemoryStream stream = new(signatureBase16.Decode());
        using BinaryReader reader = new(stream);
        var signature = reader.ReadSignature();
        error = "";
        address = publicKey;
        return signature.Verify(msg, pubKey);
    }


    public BigInteger GetCurrentBlockHeight(string chain)
    {
        var url = $"{Settings.Default.GetRest()}/api/getBlockHeight?chainInput=main";

        HttpClient httpClient = new();
        var response = httpClient.GetAsync(url).Result;
        using var content = response.Content;
        var reply = content.ReadAsStringAsync().Result;
        return BigInteger.Parse(reply.Replace("\"", ""));
    }


    protected override void Configure()
    {
        Settings.Load(GetConfiguration());
    }


    private static string GetCurrentOwnerAddress(string contractHash, string tokenId, out string error)
    {
        error = null;

        var url = $"{Settings.Default.GetRest()}/api/getNFT?symbol=" + contractHash + "&IDtext=" + tokenId +
                  "&extended=true";

        var response = Client.APIRequest<JsonDocument>(url, out var stringResponse, null, 10);
        if ( response == null ) return null;

        if ( response.RootElement.TryGetProperty("error", out var errorProperty) ) error = errorProperty.GetString();

        return response.RootElement.TryGetProperty("ownerAddress", out var ownerAddressProperty)
            ? ownerAddressProperty.GetString()
            : null;
    }
}
