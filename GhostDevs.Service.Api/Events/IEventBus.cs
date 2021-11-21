using System.Threading;
using System.Threading.Tasks;

namespace GhostDevs.Service.Events;

public interface IEventBus
{
    Task Run(CancellationToken cancellationToken);
}
