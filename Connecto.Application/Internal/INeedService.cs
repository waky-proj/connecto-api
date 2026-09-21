using Connecto.Domain.Entities;
using Connecto.Domain.Requests;

namespace Connecto.Application.Internal
{
    public interface INeedService
    {
        Task<Need> CreateAsync(
        Guid userId,
        CreateNeedRequest request,
        CancellationToken cancellationToken = default);
    }
}