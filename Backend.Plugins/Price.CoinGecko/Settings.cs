using System;
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Backend;

internal class Settings
{
    private Settings(IConfigurationSection section)
    {
        Enabled = section.GetSection("enabled").Get<bool>();
        StartDelay = section.GetValue<int>("startDelay");
        RunInterval = section.GetSection("runInterval").Get<uint>();
        EnableCoingeckoPaidFeatures = section.GetSection("enableCoingeckoPaidFeatures").Get<bool>();
        StartDate = DateTime.SpecifyKind(
            DateTime.ParseExact(section.GetSection("startDate").Get<string>(), "dd.MM.yyyy",
                CultureInfo.InvariantCulture), DateTimeKind.Utc);
    }


    public bool Enabled { get; }
    public int StartDelay { get; }
    public uint RunInterval { get; }
    public bool EnableCoingeckoPaidFeatures { get; } = false;
    public DateTime StartDate { get; }

    public static Settings Default { get; private set; }


    public static void Load(IConfigurationSection section)
    {
        Default = new Settings(section);
    }
}
