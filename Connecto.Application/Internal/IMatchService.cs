using Connecto.Domain.Matching;

namespace Connecto.Application.Internal
{
    public interface IMatchService
    {
        Task<IReadOnlyCollection<MatchResult>> FindMatchesAsync(
            Guid needId,
            CancellationToken cancellationToken = default);
    }
}