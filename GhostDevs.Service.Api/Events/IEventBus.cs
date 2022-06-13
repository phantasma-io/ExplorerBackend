using System.Threading;
using System.Threading.Tasks;

namespace GhostDevs.Service.Api.Events;

public interface IEventBus
{
    Task Run(CancellationToken cancellationToken);
}
