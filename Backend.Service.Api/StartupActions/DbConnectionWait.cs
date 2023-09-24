using System;
using System.Threading;
using System.Threading.Tasks;
using Database.Main;
using Foundatio.Extensions.Hosting.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.Service.Api.StartupActions;

public class DbConnectionWait : IStartupAction
{
    private readonly ILogger<DbConnectionWait> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public DbConnectionWait(
        ILogger<DbConnectionWait> logger,
        IServiceScopeFactory serviceScopeFactory
    )
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task RunAsync(
        CancellationToken shutdownToken = new()
    )
    {
        using (IServiceScope scope = _serviceScopeFactory.CreateScope())
        {
            MainDbContext database = scope.ServiceProvider.GetRequiredService<MainDbContext>();

            int max = 6;
            for (int i = 1; i <= max; i++)
            {
                try
                {
                    // TODO Don't know how to query version using ef core, want to show it in log again later.
                    // For now it's just a valid query to trigger connection error if there are issues.
                    if (!await database.Database.CanConnectAsync(shutdownToken))
                    {
                        _logger.LogWarning("Database connection error");
                        if (i < max)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(5000 * i), shutdownToken);
                            _logger.LogWarning("Database connection: Trying again...");
                        }
                        else
                        {
                            throw new Exception("Cannot connect to database");
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogWarning("Database connection error: {Warning}", e.Message);
                    if (i < max)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(5000 * i), shutdownToken);
                        _logger.LogWarning("Database connection: Trying again...");
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }
    }
}
