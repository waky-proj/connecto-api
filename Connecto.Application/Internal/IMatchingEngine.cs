using Connecto.Domain.Entities;
using Connecto.Domain.Matching;

namespace Connecto.Application.Internal
{
    public interface IMatchingEngine
    {
        MatchResult Calculate(Need need, Offering offering);
    }
}