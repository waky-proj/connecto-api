using Connecto.Domain.Entities;
using Connecto.Domain.Matching;

namespace Connecto.Application.Internal.Calculators
{
    public interface ICalculator
    {
        void Calculate(Need need, Offering offering, MatchScore score);
                
        bool IsMatch(MatchScore score);

        string GetReason { get; }
    }
}