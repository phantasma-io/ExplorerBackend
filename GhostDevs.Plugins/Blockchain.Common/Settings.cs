using Microsoft.Extensions.Configuration;

namespace GhostDevs.Blockchain
{
    internal class Settings
    {
        public bool Enabled { get; }
        public int StartDelay { get; }
        public int EventsProcessingInterval { get; }
        public int EventsPriceRefreshStartDelay { get; }
        public int EventsPriceRefreshInterval { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            Enabled = section.GetSection("enabled").Get<bool>();

            StartDelay = section.GetValue<int>("startDelay");

            EventsProcessingInterval = section.GetValue<int>("eventsProcessingInterval");

            EventsPriceRefreshStartDelay = section.GetValue<int>("eventsPriceRefreshStartDelay");

            EventsPriceRefreshInterval = section.GetValue<int>("eventsPriceRefreshInterval");
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
