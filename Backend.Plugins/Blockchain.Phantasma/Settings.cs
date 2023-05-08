using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Backend.Blockchain;

internal class Settings
{
    private Settings(IConfiguration section)
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
        ChangeNodesInterval = 30; //30 mins
        LastNodeChange = DateTime.Now;
    }


    public DateTime LastNodeChange { get; private set; }
    public bool Enabled { get; }
    public int StartDelay { get; }
    public int FirstBlock { get; }
    public string PhaNexus { get; }
    public List<string> PhaRestNodes { get; }
    public string SelectedPhaRestNodes { get; private set; }
    public int ChangeNodesInterval { get; private set; }
    public List<string> PhaRpcNodes { get; }
    public string SelectedPhaRpcNodes { get; private set; }
    public int TokensProcessingInterval { get; }
    public int BlocksProcessingInterval { get; }
    public int EventsProcessingInterval { get; }
    public int RomRamProcessingInterval { get; }
    public int SeriesProcessingInterval { get; }
    public int InfusionsProcessingInterval { get; }
    public int NamesSyncInterval { get; }

    public static Settings Default { get; private set; }


    public static void Load(IConfigurationSection section)
    {
        Default = new Settings(section);
    }


    /// <summary>
    /// Get a random Phantasma node from the list
    /// </summary>
    /// <returns></returns>
    public string GetRest()
    {
        if ( string.IsNullOrEmpty(SelectedPhaRestNodes))
            SelectedPhaRestNodes = PhaRestNodes[0];
        
        if (Utils.HasElapsed(LastNodeChange, TimeSpan.FromMinutes(ChangeNodesInterval)))
        {
            LastNodeChange = DateTime.Now;
            var index = PhaRestNodes.IndexOf(SelectedPhaRestNodes);
            if ( index == PhaRestNodes.Count - 1 )
                index = 0;
            else
                index++;
            SelectedPhaRestNodes = PhaRestNodes[index];
        }
        
        return SelectedPhaRestNodes;
    }


    /// <summary>
    /// Get a random Phantasma node from the list
    /// </summary>
    /// <returns></returns>
    public string GetRpc()
    {
        if ( string.IsNullOrEmpty(SelectedPhaRpcNodes))
            SelectedPhaRpcNodes = PhaRpcNodes[0];
        
        if (Utils.HasElapsed(LastNodeChange, TimeSpan.FromMinutes(ChangeNodesInterval)))
        {
            LastNodeChange = DateTime.Now;
            var index = PhaRpcNodes.IndexOf(SelectedPhaRpcNodes);
            if ( index == PhaRpcNodes.Count - 1 )
                index = 0;
            else
                index++;
            SelectedPhaRpcNodes = PhaRpcNodes[index];
        }
        
        return SelectedPhaRpcNodes;
    }
}
