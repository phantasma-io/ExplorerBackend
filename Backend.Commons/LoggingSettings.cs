using Microsoft.Extensions.Configuration;

namespace Backend.Commons;

public class LoggingSettings
{
    private LoggingSettings(IConfiguration section)
    {
        Level = section.GetValue<string>("Level");
        LogOverwrite = section.GetValue<bool>("LogOverwrite");
        LogDirectoryPath = section.GetValue<string>("LogDirectoryPath");
    }


    public string Level { get; }
    public bool LogOverwrite { get; }
    public string LogDirectoryPath { get; }

    public static LoggingSettings Default { get; private set; }


    public static void Load(IConfigurationSection section)
    {
        Default = new LoggingSettings(section);
    }
}
