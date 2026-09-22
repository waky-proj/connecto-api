using Connecto.Domain.Results;

namespace Connecto.Application.Internal
{
    public interface IDiscoveryService
    {
        Task<DiscoveryResult> DiscoverNeedsAsync(
            string title,
            string description,
            CancellationToken cancellationToken = default);
    }
}
