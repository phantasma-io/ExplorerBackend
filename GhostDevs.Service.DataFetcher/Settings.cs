using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace GhostDevs.Service.DataFetcher;

internal class Settings
{
    private Settings(IConfigurationSection section)
    {
        Chains = section.GetSection("Chains").Get<List<ChainData>>();
        Tokens = section.GetSection("Tokens").Get<List<TokenData>>();
        FetchInterval = section.GetValue<int>("FetchInterval");
    }


    public List<ChainData> Chains { get; }
    public List<TokenData> Tokens { get; }
    public int FetchInterval { get; }

    public static Settings Default { get; private set; }


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
