using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Backend.Service.Api;

internal class Settings
{
    private Settings(IConfiguration section)
    {
        var settings = section.Get<ApiServiceSettings>();

        PerformanceMetrics = section.GetSection("PerformanceMetrics").Get<PerformanceMetricsSettings>();
        RejectedTransactionCandidates = section.GetSection("RejectedTransactionCandidates")
            .Get<RejectedTransactionCandidateSettings>() ?? new RejectedTransactionCandidateSettings();
    }

    public PerformanceMetricsSettings PerformanceMetrics { get; }
    public RejectedTransactionCandidateSettings RejectedTransactionCandidates { get; }

    public static Settings Default { get; set; }


    public static void Load(IConfigurationSection section)
    {
        Default = new Settings(section);
    }


    public class ApiServiceSettings
    {
    }

    public class RejectedTransactionCandidateSettings
    {
        private int _restNodeIndex = -1;

        public bool CaptureEnabled { get; set; }
        public string Nexus { get; set; } = "";
        public string DefaultChain { get; set; } = "main";
        public int RpcTimeoutSeconds { get; set; } = 8;
        public List<string> RestNodes { get; set; } = new();

        public string GetRest()
        {
            if (RestNodes == null || RestNodes.Count == 0)
                return "";

            var nodes = RestNodes.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            if (nodes.Length == 0)
                return "";

            var index = Interlocked.Increment(ref _restNodeIndex);
            var position = index % nodes.Length;
            if (position < 0)
                position += nodes.Length;

            return nodes[position].TrimEnd('/');
        }
    }

    public class PerformanceMetricsSettings
    {
        public bool CountsEnabled { get; set; }
        public bool AveragesEnabled { get; set; }
        public int MaxRequestsPerAverage { get; set; } = 100;
        public int LongRunningRequestThreshold { get; set; } = 500;
        public int LongRunningSqlQueryThreshold { get; set; } = 200;
        public bool SqlQueryTimeLoggingEnabled { get; set; }
    }
}
