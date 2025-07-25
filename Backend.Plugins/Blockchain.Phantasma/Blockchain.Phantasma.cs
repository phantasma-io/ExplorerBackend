using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using Backend.Api;
using Backend.Commons;
using Backend.PluginEngine;
using Database.Main;
using PhantasmaPhoenix.Core;
using PhantasmaPhoenix.Cryptography;
using PhantasmaPhoenix.Cryptography.Extensions;
using Serilog;
using Address = PhantasmaPhoenix.Cryptography.Address;

namespace Backend.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    private static List<Chain> _chainList;

    private readonly Queue<Tuple<string, string, long>> _methodQueue = new();
    private bool _running = true;
    public override string Name => "PHA";
    public string[] ChainNames { get; private set; }
    private BigInteger height = 0;
    private Dictionary<EventKindMethods.ChainEventKindKey, int> _eventKinds;

    public void Fetch()
    {
    }


    public void Startup()
    {
        Log.Information("{Name} plugin: Startup ...", Name);

        if ( !Settings.Default.Enabled )
        {
            Log.Information("{Name} plugin is disabled, stopping", Name);
            return;
        }

        Thread.Sleep(Settings.Default.StartDelay * 1000);

        try
        {
            using ( MainDbContext databaseContext = new() )
            {
                InitChains();

                _chainList = ChainMethods.GetChains(databaseContext).ToList();
                ChainNames = ChainMethods.GetChainNames(databaseContext).ToArray();

                //init tokens once too, cause we might need them, to keep them update, thread them later

                foreach ( var chain in _chainList )
                {
                    InitNexusData(chain.ID);
                    EventKindMethods.UpsertAllAsync(databaseContext, chain).Wait();
                    databaseContext.SaveChanges();
                }

                _eventKinds = EventKindMethods.GetAllAsync(databaseContext).Result;
            }

            Log.Verbose("[{Name}] got {ChainCount} Chains, get to work", Name, _chainList.Count);
            foreach ( var chain in _chainList )
            {
                Log.Information("[{Name}] starting with Chain {ChainName} and Internal Id {Id}", Name,
                    chain.NAME,
                    chain.ID);

                StartupNexusSync(chain);
                StartupBlockSync(chain.NAME);
                StartupRomRamSync(chain);
                StartupSeriesSync(chain);
                StartupInfusionSync(chain);
                StartupContractSync(chain);
                StartupContractMethodsSync(chain);
            }
            
            // Initialization was successful
        }
        catch ( Exception e )
        {
            LogEx.Exception("Chains processing", e);

            Thread.Sleep(Settings.Default.TokensProcessingInterval * 1000);
        }

        Log.Information("{Name} plugin: Startup finished", Name);
    }

    /// <summary>
    /// 
    /// </summary>
    private void StartupNexusSync(Chain chain)
    {
        Thread nexusDataInitThread = new(async () =>
        {
            while ( _running )
                try
                {
                    InitNexusData(chain.ID);
                    await UpdateTokens(chain.ID);

                    Thread.Sleep(Settings.Default.TokensProcessingInterval *
                                 1000); // We process tokens every TokensProcessingInterval seconds
                }
                catch ( Exception e )
                {
                    LogEx.Exception("NexusData init", e);

                    Thread.Sleep(Settings.Default.TokensProcessingInterval * 1000);
                }
        });
        nexusDataInitThread.Start();
    }
    
    /// <summary>
    /// 
    /// </summary>
    private void StartupBlockSync(string chainName)
    {
        Thread blocksSyncThread = new(async () =>
        {
            while ( _running )
                try
                {
                    BigInteger currentHeight;
                    using (MainDbContext databaseContext = new())
                    {
                        currentHeight = ChainMethods.GetLastProcessedBlock(databaseContext, chainName);
                    }

                    height = GetCurrentBlockHeight(chainName);
                    Log.Information("[Blocks] Chain height: {Height}, explorer's height {explorerHeight}", height, currentHeight);

                    if (currentHeight > height)
                    {
                        Log.Warning("[Blocks] RPC is out of sync, RPC: {Height}, explorer: {explorerHeight}", height, currentHeight);
                    }
                    else if (Settings.Default.HeightLimit != 0 && currentHeight >= Settings.Default.HeightLimit)
                    {
                        Log.Warning("[Blocks] Height limit is reached {Height} >= {HeightLimit}", currentHeight, Settings.Default.HeightLimit);
                    }
                    else
                    {
                        if (Settings.Default.HeightLimit != 0 && height >= Settings.Default.HeightLimit)
                        {
                            height = Settings.Default.HeightLimit;
                        }
                        FetchBlocksRange(chainName, currentHeight, height).Wait();
                    }

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
    }


    private void StartupRomRamSync(Chain chain)
    {
        if(!Settings.Default.RomRamProcessingEnabled)
        {
            Log.Warning("[{Name}][RAM/ROM update] Disabled", Name);
            return;
        }

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
    }


    private void StartupSeriesSync(Chain chain)
    {
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
    }


    private void StartupInfusionSync(Chain chain)
    {
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
    }

    private void StartupContractSync(Chain chain)
    {
        Thread contractSyncThread = new(() =>
        {
            while ( _running )
                try
                {
                    ContractDataSync(chain.ID);

                    Thread.Sleep(Settings.Default.NamesSyncInterval *
                                 1000); // We sync names every NamesSyncInterval seconds
                }
                catch ( Exception e )
                {
                    LogEx.Exception("Contract sync", e);

                    Thread.Sleep(Settings.Default.NamesSyncInterval * 1000);
                }
        });
        contractSyncThread.Start();
    }


    private void StartupContractMethodsSync(Chain chain)
    {
        Thread contractMethodSyncThread = new(() =>
        {
            while ( _running )
                try
                {
                    ContractMethodSync();

                    Thread.Sleep(Settings.Default.NamesSyncInterval *
                                 1000); // We sync names every NamesSyncInterval seconds
                }
                catch ( Exception e )
                {
                    LogEx.Exception("ContractMethod sync", e);

                    Thread.Sleep(Settings.Default.NamesSyncInterval * 1000);
                }
        });
        contractMethodSyncThread.Start();
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

        var pubKey = Address.Parse(publicKey);

        var msg = !string.IsNullOrEmpty(messagePrefixBase16)
            ? ByteArrayUtils.ConcatBytes(messagePrefixBase16.Decode(), messageBase16.Decode())
            : messageBase16.Decode();

        using MemoryStream stream = new(signatureBase16.Decode());
        using BinaryReader reader = new(stream);
        var signature = reader.ReadSignature();
        return signature.Verify(msg, pubKey);
    }


    public bool VerifySignature(string chainShortName, string publicKey, string messageBase16,
        string messagePrefixBase16,
        string signatureBase16, out string address, out string error)
    {
        var pubKey = Address.Parse(publicKey);

        var msg = !string.IsNullOrEmpty(messagePrefixBase16)
            ? ByteArrayUtils.ConcatBytes(messagePrefixBase16.Decode(), messageBase16.Decode())
            : messageBase16.Decode();

        using MemoryStream stream = new(signatureBase16.Decode());
        using BinaryReader reader = new(stream);
        var signature = reader.ReadSignature();
        error = "";
        address = publicKey;
        return signature.Verify(msg, pubKey);
    }


    public BigInteger GetCurrentBlockHeight(string chain)
    {
        var url = $"{Settings.Default.GetRest()}/api/v1/getBlockHeight?chainInput=main";

        HttpClient httpClient = new();
        var response = httpClient.GetAsync(url).Result;
        using var content = response.Content;
        var reply = content.ReadAsStringAsync().Result;
        Log.Information("[Blocks] Get heigt result: {Result}, url: {Url}", reply, url);
        return BigInteger.Parse(reply.Replace("\"", ""));
    }


    protected override void Configure()
    {
        Settings.Load(GetConfiguration());
    }


    private static string GetCurrentOwnerAddress(string contractHash, string tokenId, out string error)
    {
        error = null;

        var url = $"{Settings.Default.GetRest()}/api/v1/getNFT?symbol=" + contractHash + "&IDtext=" + tokenId +
                  "&extended=true";

        var response = Client.ApiRequest<JsonDocument>(url, out var stringResponse, null, 10);
        if ( response == null ) return null;

        if ( response.RootElement.TryGetProperty("error", out var errorProperty) ) error = errorProperty.GetString();

        return response.RootElement.TryGetProperty("ownerAddress", out var ownerAddressProperty)
            ? ownerAddressProperty.GetString()
            : null;
    }
}
