using Microsoft.Extensions.Configuration;

namespace Backend;

internal class Settings
{
    private Settings(IConfigurationSection section)
    {
        Enabled = section.GetSection("enabled").Get<bool>();
        RunInterval = section.GetSection("runInterval").Get<uint>();
    }


    public bool Enabled { get; }
    public uint RunInterval { get; }

    public static Settings Default { get; private set; }


    public static void Load(IConfigurationSection section)
    {
        Default = new Settings(section);
    }
}
