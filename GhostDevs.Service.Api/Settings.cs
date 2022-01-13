using Microsoft.Extensions.Configuration;

namespace GhostDevs.Service;

internal class Settings
{
    private Settings(IConfigurationSection section)
    {
        var settings = section.Get<ApiServiceSettings>();
    }


    public static Settings Default { get; private set; }


    public static void Load(IConfigurationSection section)
    {
        Default = new Settings(section);
    }


    public class ApiServiceSettings
    {
    }
}
