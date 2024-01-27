using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Database.Main;

internal class Settings
{
    public string ConnectionString;

    public int ConnectMaxRetries;
    public int ConnectRetryTimeout;


    private Settings(IConfiguration section)
    {
        var connectionSettings = section.GetSection("Main").Get<DatabaseConnectionSettings>();

        ConnectionString = new NpgsqlConnectionStringBuilder {
            Host = connectionSettings.Host,
            Port = connectionSettings.Port,
            Username = connectionSettings.Username,
            Password = connectionSettings.Password,
            Database = connectionSettings.Database,
            IncludeErrorDetail = true,
            MaxPoolSize = connectionSettings.MaximumPoolSize ?? 100
        }.ToString();

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
        public int Port { get; set; } = 5432;
        public string Database { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public int? MaximumPoolSize { get; set; }
    }
}
