using System.Threading;
using System.Threading.Tasks;
using Backend.PluginEngine;
using Foundatio.Extensions.Hosting.Startup;
using Microsoft.Extensions.Logging;

namespace Backend.Service.Api.StartupActions;

public class LoadPlugins : IStartupAction
{
    private readonly ILogger<LoadPlugins> _logger;

    public LoadPlugins(
        ILogger<LoadPlugins> logger
    )
    {
        _logger = logger;
    }

    public Task RunAsync(
        CancellationToken shutdownToken = new()
    )
    {
        _logger.LogInformation("Loading plugins...");
        Plugin.LoadPlugins(); // TODO use shutdownToken

        return Task.CompletedTask;
    }
}
