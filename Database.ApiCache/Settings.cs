using Microsoft.Extensions.Configuration;

namespace Database.ApiCache
{
    internal class Settings
    {
        public string ConnectionString;

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            var connectionSettings = section.GetSection("ApiCache").Get<DatabaseConnectionSettings>();
            ConnectionString = $"Host={connectionSettings.Host};Username={connectionSettings.Username};Password={connectionSettings.Password};Database={connectionSettings.Database};Include Error Detail=true";
        }

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
}
