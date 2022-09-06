using System.Threading;
using System.Threading.Tasks;

namespace Backend.Service.Api.Events;

public interface IEventBus
{
    Task Run(CancellationToken cancellationToken);
}
