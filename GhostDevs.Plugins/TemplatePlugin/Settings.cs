using Microsoft.Extensions.Configuration;

namespace GhostDevs
{
    internal class Settings
    {
        public bool Enabled { get; }
        public uint RunInterval { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            Enabled = section.GetSection("enabled").Get<bool>();
            RunInterval =  section.GetSection("runInterval").Get<uint>();
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
