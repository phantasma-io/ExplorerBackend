using System;
using Microsoft.Extensions.Configuration;

namespace Backend.Price;

internal class Settings
{
    // Whether the plugin is enabled
    public bool Enabled { get; set; }

    // Delay before the plugin starts (in seconds)
    public int StartDelay { get; set; }

    // Interval between plugin runs (in seconds)
    public uint RunInterval { get; set; }

    // Plugin start date (parsed automatically from config)
    public DateTime StartDate { get; set; }

    // Whether to enable paid Coingecko features
    public bool EnableCoingeckoPaidFeatures { get; set; } = false;

    // A list of coin IDs that should be treated as inactive
    public string[] InactiveCoins { get; set; } = Array.Empty<string>();

    // Loaded instance of the settings
    public static Settings Default { get; private set; }

    // Loads the settings from the "PluginConfiguration" section
    public static void Load(IConfiguration configuration)
    {
        Default = configuration.GetSection("PluginConfiguration").Get<Settings>();
    }
}
