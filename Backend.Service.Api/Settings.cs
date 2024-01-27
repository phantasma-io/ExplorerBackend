using Microsoft.Extensions.Configuration;

namespace Backend.Service.Api;

internal class Settings
{
    private Settings(IConfiguration section)
    {
        var settings = section.Get<ApiServiceSettings>();

        PerformanceMetrics = section.GetSection("PerformanceMetrics").Get<PerformanceMetricsSettings>();
    }

    public PerformanceMetricsSettings PerformanceMetrics { get; }

    public static Settings Default { get; set; }


    public static void Load(IConfigurationSection section)
    {
        Default = new Settings(section);
    }


    public class ApiServiceSettings
    {
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
