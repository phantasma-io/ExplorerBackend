using System.Threading;
using System.Threading.Tasks;
using GhostDevs.Service.Events;
using Microsoft.Extensions.Hosting;

namespace GhostDevs.Service.Hosting;

public class EventBusBackgroundService : BackgroundService
{
    private readonly IEventBus _bus;


    public EventBusBackgroundService(IEventBus bus)
    {
        _bus = bus;
    }


    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _bus.Run(stoppingToken);
    }
}
