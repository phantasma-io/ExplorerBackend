using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace GhostDevs.Blockchain;

internal class Settings
{
    private Settings(IConfigurationSection section)
    {
        Enabled = section.GetSection("enabled").Get<bool>();

        StartDelay = section.GetValue<int>("startDelay");

        FirstBlock = section.GetValue<int>("first.block");

        PhaNexus = section.GetValue<string>("phantasma.nexus");

        PhaRestNodes = section.GetSection("phantasma.rest.nodes").AsEnumerable()
            .Where(p => p.Value != null)
            .Select(p => p.Value)
            .ToList();

        PhaRpcNodes = section.GetSection("phantasma.rpc.nodes").AsEnumerable()
            .Where(p => p.Value != null)
            .Select(p => p.Value)
            .ToList();

        TokensProcessingInterval = section.GetValue<int>("tokensProcessingInterval");
        BlocksProcessingInterval = section.GetValue<int>("blocksProcessingInterval");
        EventsProcessingInterval = section.GetValue<int>("eventsProcessingInterval");
        RomRamProcessingInterval = section.GetValue<int>("romRamProcessingInterval");
        SeriesProcessingInterval = section.GetValue<int>("seriesProcessingInterval");
        InfusionsProcessingInterval = section.GetValue<int>("infusionsProcessingInterval");
        NamesSyncInterval = section.GetValue<int>("namesSyncInterval");

        NFTs = section.GetSection("phantasma.nfts").Get<List<NFTData>>();
    }


    public bool Enabled { get; }
    public int StartDelay { get; }
    public int FirstBlock { get; }
    public string PhaNexus { get; }
    public List<string> PhaRestNodes { get; }
    public List<string> PhaRpcNodes { get; }
    public int TokensProcessingInterval { get; }
    public int BlocksProcessingInterval { get; }
    public int EventsProcessingInterval { get; }
    public int RomRamProcessingInterval { get; }
    public int SeriesProcessingInterval { get; }
    public int InfusionsProcessingInterval { get; }
    public int NamesSyncInterval { get; }
    public List<NFTData> NFTs { get; } = new();

    public static Settings Default { get; private set; }


    public static void Load(IConfigurationSection section)
    {
        Default = new Settings(section);
    }


    public string GetRest()
    {
        // TODO: Add proper logic later.
        return PhaRestNodes[0];
    }


    public string GetRpc()
    {
        // TODO: Add proper logic later.
        return PhaRpcNodes[0];
    }
}

public class NFTData
{
    public string Symbol { get; set; }
}
