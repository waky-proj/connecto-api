using Connecto.Api.Controllers;
using Connecto.Application.Internal.Calculators;
using Connecto.Application.Internal.Impl;
using Connecto.Application.Internal.Validators;
using Connecto.Domain.Entities;
using Connecto.Domain.Matching;

namespace Connecto.Tests.Unit.Application.Internal;

public sealed class DiscoveryPolicyTests
{
    [Fact]
    public void Hard_incompatibilities_are_excluded_before_ranking()
    {
        var need = new Need { UserId = Guid.NewGuid(), Category = "Technology", Currency = "NZD",
            PricingBasis = PricingBasis.Fixed, BudgetMin = 40, BudgetMax = 100, Location = "Auckland" };
        var offer = new Offering { UserId = Guid.NewGuid(), Category = "Technology", Currency = "NZD",
            PricingBasis = PricingBasis.Fixed, PriceMin = 50, Location = "Auckland" };
        Assert.True(MatchService.IsEligible(need, offer));
        Assert.False(MatchService.IsEligible(need, offer with { PriceMin = 101 }));
        Assert.False(MatchService.IsEligible(need, offer with { PriceMin = 20, PriceMax = 39 }));
        Assert.False(MatchService.IsEligible(need, offer with { Location = "Wellington" }));
        Assert.True(MatchService.IsEligible(need, offer with { Location = "Wellington", RemoteAvailable = true }));
        Assert.False(MatchService.IsEligible(need with { PricingBasis = PricingBasis.Hourly }, offer));
    }

    [Fact]
    public void Related_wording_has_inspectable_expansion()
    {
        Assert.Contains("laptop", DiscoveryVocabulary.Expand("computer"));
        Assert.Contains("weeding", DiscoveryVocabulary.Expand("gardening"));
    }

    [Fact]
    public void Configured_weights_change_factor_contribution_and_invalid_totals_fail()
    {
        var weights = new MatchingWeights { Skills = 25, Requirements = 30, Availability = 15,
            Budget = 10, Location = 10, Experience = 5, Reputation = 5 };
        MatchingWeightsValidator.Validate(weights);
        Assert.Throws<InvalidOperationException>(() => MatchingWeightsValidator.Validate(weights with { Skills = -1 }));
        var calculators = new ICalculator[] { new RequirementCalculator() };
        var need = new Need { Id = Guid.NewGuid(), Category = "Technology", Skills = [] };
        var offer = new Offering { Id = Guid.NewGuid(), Category = "Technology", Skills = [] };
        Assert.Equal(30, new MatchingEngine(calculators, weights).Calculate(need, offer).Score.Requirements);
    }

    [Fact]
    public void Missing_provider_factors_are_explained_without_awarding_fit()
    {
        var need = new Need { Id = Guid.NewGuid(), Category = "Technology", Skills = [],
            Location = "Auckland", PricingBasis = PricingBasis.Fixed };
        var offer = new Offering { Id = Guid.NewGuid(), Category = "Technology", Skills = [],
            Location = "", ExperienceYears = 0, ReputationScore = 0 };
        var result = new MatchingEngine([new LocationCalculator(), new ExperienceCalculator(),
            new ReputationCalculator()]).Calculate(need, offer);
        Assert.Equal(0, result.Score.Location);
        Assert.Equal(0, result.Score.Experience);
        Assert.Equal(0, result.Score.Reputation);
        Assert.Contains(result.Unknowns, x => x.Contains("location"));
        Assert.Contains(result.Unknowns, x => x.Contains("reputation"));
    }
}
