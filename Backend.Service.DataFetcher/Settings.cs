using Microsoft.Extensions.Configuration;

namespace Backend.Service.DataFetcher;

internal class Settings
{
    private Settings(IConfiguration section)
    {
        FetchInterval = section.GetValue<int>("FetchInterval");
    }


    public int FetchInterval { get; }

    public static Settings Default { get; private set; }


    public static void Load(IConfigurationSection section)
    {
        Default = new Settings(section);
    }
}
