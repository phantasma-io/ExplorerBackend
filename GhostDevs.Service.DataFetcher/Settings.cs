using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace GhostDevs.Service
{
    internal class Settings
    {
        public List<ChainData> Chains { get; }
        public List<TokenData> Tokens { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            Chains = section.GetSection("Chains").Get<List<ChainData>>();
            Tokens = section.GetSection("Tokens").Get<List<TokenData>>();
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }

        public class ChainData
        {
            public string Name { get; set; }
        }

        public class TokenData
        {
            public string Chain { get; set; }
            public string Contract { get; set; }
            public string Symbol { get; set; }
        }
    }
}
