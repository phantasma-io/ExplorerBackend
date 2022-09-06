using Microsoft.Extensions.Configuration;

namespace Blockchain.Img;

internal class Settings
{
    private Settings(IConfiguration section)
    {
        Enabled = section.GetSection("enabled").Get<bool>();
        RunInterval = section.GetSection("runInterval").Get<uint>();
        StartDelay = section.GetValue<int>("startDelay");
        Folder = section.GetValue<string>("folder");
        FileEnding = section.GetValue<string>("fileEnding");
        HostName = section.GetValue<string>("hostName");
        DefaultImage = section.GetValue<string>("defaultImage");
    }


    public bool Enabled { get; }
    public uint RunInterval { get; }
    public int StartDelay { get; }
    public string Folder { get; }
    public string FileEnding { get; }
    public string HostName { get; }
    public string DefaultImage { get; }


    public static Settings Default { get; private set; }


    public static void Load(IConfigurationSection section)
    {
        Default = new Settings(section);
    }
}
