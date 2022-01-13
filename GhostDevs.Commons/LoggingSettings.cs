using Microsoft.Extensions.Configuration;

namespace GhostDevs.Commons;

public class LoggingSettings
{
    private LoggingSettings(IConfiguration section)
    {
        Level = section.GetValue<string>("Level");
        LogOverwrite = section.GetValue<bool>("LogOverwrite");
    }


    public string Level { get; }
    public bool LogOverwrite { get; }

    public static LoggingSettings Default { get; private set; }


    public static void Load(IConfigurationSection section)
    {
        Default = new LoggingSettings(section);
    }
}
