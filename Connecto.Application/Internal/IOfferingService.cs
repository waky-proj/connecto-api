using Connecto.Domain.Entities;
using Connecto.Domain.Requests;

namespace Connecto.Application.Internal
{
    public interface IOfferingService
    {
        Task<Offering> CreateAsync(
            Guid userId,
            CreateOfferingRequest request,
            CancellationToken cancellationToken = default);
    }
}