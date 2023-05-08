using Microsoft.Extensions.Configuration;

namespace Database.Main;

internal class Settings
{
    public string ConnectionString;

    public int ConnectMaxRetries;
    public int ConnectRetryTimeout;


    private Settings(IConfiguration section)
    {
        var connectionSettings = section.GetSection("Main").Get<DatabaseConnectionSettings>();
        ConnectionString =
            $"Host={connectionSettings.Host};Username={connectionSettings.Username};Password={connectionSettings.Password};Database={connectionSettings.Database};MaxPoolSize=500;Timeout=60;Include Error Detail=true";

        ConnectMaxRetries = section.GetValue<int>("ConnectMaxRetries");
        ConnectRetryTimeout = section.GetValue<int>("ConnectRetryTimeout");
    }


    public static Settings Default { get; private set; }


    public static void Load(IConfigurationSection section)
    {
        Default = new Settings(section);
    }


    public class DatabaseConnectionSettings
    {
        public string Host { get; set; }
        public string Database { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
