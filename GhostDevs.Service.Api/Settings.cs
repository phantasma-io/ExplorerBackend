using Microsoft.Extensions.Configuration;

namespace GhostDevs.Service.Api;

internal class Settings
{
    private Settings(IConfiguration section)
    {
        var settings = section.Get<ApiServiceSettings>();
    }


    private static Settings Default { get; set; }


    public static void Load(IConfigurationSection section)
    {
        Default = new Settings(section);
    }


    public class ApiServiceSettings
    {
    }
}
