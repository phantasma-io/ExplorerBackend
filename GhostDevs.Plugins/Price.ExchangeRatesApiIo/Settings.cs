using System.Linq;
using Microsoft.Extensions.Configuration;

namespace GhostDevs;

internal class Settings
{
    private Settings(IConfigurationSection section)
    {
        Enabled = section.GetSection("enabled").Get<bool>();
        StartDelay = section.GetValue<int>("startDelay");
        RunInterval = section.GetSection("runInterval").Get<uint>();

        ApiKeys = section.GetSection("apiKeys").AsEnumerable()
            .Where(p => p.Value != null)
            .Select(p => p.Value)
            .ToArray();
    }


    public bool Enabled { get; }
    public int StartDelay { get; }
    public uint RunInterval { get; }
    public string[] ApiKeys { get; }

    public static Settings Default { get; private set; }


    public static void Load(IConfigurationSection section)
    {
        Default = new Settings(section);
    }
}
