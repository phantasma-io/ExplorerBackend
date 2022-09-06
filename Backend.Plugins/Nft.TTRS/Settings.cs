using Microsoft.Extensions.Configuration;

namespace Backend.Nft;

internal class Settings
{
    private Settings(IConfiguration section)
    {
        Enabled = section.GetSection("enabled").Get<bool>();
        StartDelay = section.GetValue<int>("startDelay");
        RunInterval = section.GetSection("runInterval").Get<uint>();
    }


    public bool Enabled { get; }
    public int StartDelay { get; }
    public uint RunInterval { get; }

    public static Settings Default { get; private set; }


    public static void Load(IConfigurationSection section)
    {
        Default = new Settings(section);
    }
}
