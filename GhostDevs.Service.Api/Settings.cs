using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace GhostDevs.Service
{
    internal class Settings
    {
        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            var settings = section.Get<ApiServiceSettings>();
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }

        public class ApiServiceSettings
        {
        }
    }
}
