using Microsoft.Extensions.Configuration;
using System;

namespace GhostDevs
{
    internal class Settings
    {
        public bool Enabled { get; }
        public int StartDelay { get; }
        public uint RunInterval { get; }
        public DateTime StartDate { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            Enabled = section.GetSection("enabled").Get<bool>();
            StartDelay = section.GetValue<int>("startDelay");
            RunInterval =  section.GetSection("runInterval").Get<uint>();
            StartDate = DateTime.SpecifyKind(DateTime.ParseExact(section.GetSection("startDate").Get<string>(), "dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
