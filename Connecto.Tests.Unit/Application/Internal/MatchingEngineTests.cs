using Connecto.Application.Internal;
using Connecto.Application.Internal.Calculators;
using Connecto.Application.Internal.Impl;
using Connecto.Domain.Entities;

namespace Connecto.Tests.Unit.Application.Internal
{
    public class MatchingEngineTests
    {
        private static IEnumerable<ICalculator> calculators = [
                new AvailabilityCalculator(),
                new BudgetCalculator(),
                new ExperienceCalculator(),
                new LocationCalculator(),
                new ReputationCalculator(),
                new RequirementCalculator(),
                new SkillCalculator()
            ];

        private readonly IMatchingEngine _engine =
            new MatchingEngine(calculators);

        private static Guid UserId => Guid.NewGuid();

        private static Need CreateNeed(string category, List<string> skills, decimal budgetMin = 5000m, 
            decimal budgetMax = 10000m, string location = "location", DateTime? deadline = null)
        {
            var needSkills = skills.Select(x =>
            {
                if (Enum.TryParse<Skill>(x, out var skill))
                    return skill;
                return Skill.None;
            })
            .Where(x => x != Skill.None).Select(x => new NeedSkill { Skill = x }).ToList();
            return new Need(UserId, "title", "description", category, needSkills, 
                budgetMin, budgetMax, location, deadline ?? DateTime.UtcNow.AddMonths(10), true);
        }

        private static Offering CreateOffering(string category, List<string> skills, string location = "location",
            bool remote = false, decimal priceMin = 5000m, decimal priceMax = 10000m, DateTime? availableFrom = null, 
            int experience = 5, decimal reputation = 100)
        {
            var offeringSkills = skills.Select(x =>
            {
                if (Enum.TryParse<Skill>(x, out var skill))
                    return skill;
                return Skill.None;
            })
            .Where(x => x != Skill.None).Select(x => new OfferingSkill { Skill = x }).ToList();
            return new Offering(UserId, "title", "description", category, offeringSkills, 
                priceMin, priceMax, location, experience, availableFrom ?? DateTime.UtcNow.AddMonths(10), remote);
        }

        [Fact]
        public async Task Calculate_WhenExactMatch_ReturnsHighScore()
        {
            var need = CreateNeed(
                category: "Web Development",
                skills: ["React", "TypeScript"],
                location: "Auckland",
                budgetMin: 600m,
                budgetMax: 600m,
                deadline: DateTime.UtcNow);

            var offering = CreateOffering(
                category: "Web Development",
                skills: ["React", "TypeScript"],
                location: "Auckland",
                remote: true,
                priceMin: 500,
                priceMax: 700,
                availableFrom: DateTime.UtcNow.AddDays(-1),
                experience: 5,
                reputation: 100);

            var result = _engine.Calculate(need, offering);

            Assert.True(result.Score.Total >= 90);
        }

        [Fact]
        public async Task Calculate_WhenPartialMatch_ReturnsMediumScore()
        {
            var need = CreateNeed(
               category: "Web Development",
               skills: ["React", "TypeScript"]);

            var offering = CreateOffering(
                category: "Web Development",
                skills: ["React"]);

            var result = _engine.Calculate(need, offering);

            Assert.True(result.Score.Total >= 35 && result.Score.Total < 90);
        }

        [Fact]
        public async Task Calculate_WhenNoMatch_ReturnsLowScore()
        {
            var need = CreateNeed(
               category: "Web Development",
               skills: ["React", "TypeScript"],
               location: "Auckland",
               budgetMin: 50000m,
               budgetMax: 100000m,
               deadline: DateTime.UtcNow);

            var offering = CreateOffering(
                category: "Home Renovation",
                skills: ["Building"]);

            var result = _engine.Calculate(need, offering);

            Assert.True(result.Score.Total < 35);
        }

        [Fact]
        public void Calculate_WhenWrongCategory_ShouldNotReceiveRequirementScore()
        {
            var need = CreateNeed(
                category: "Web Development",
                skills: ["React"]);

            var offering = CreateOffering(
                category: "Photography",
                skills: ["React"]);

            var result = _engine.Calculate(need, offering);

            Assert.Equal(0, result.Score.Requirements);
        }

        [Fact]
        public void Calculate_WhenIncompatibleBudget_ShouldReceiveZeroBudgetScore()
        {
            var need = CreateNeed(
                category: "Web Development",
                skills: ["React"],
                budgetMin: 500,
                budgetMax: 800);

            var offering = CreateOffering(
                category: "Web Development",
                skills: ["React"],
                priceMin: 1500,
                priceMax: 2000);

            var result = _engine.Calculate(need, offering);

            Assert.Equal(0, result.Score.Budget);
        }

        [Fact]
        public void Calculate_WhenRemoteOffering_ShouldMatchAnyLocation()
        {
            var need = CreateNeed(
                category: "Web Development",
                skills: ["React"],
                location: "Auckland");

            var offering = CreateOffering(
                category: "Web Development",
                skills: ["React"],
                location: "Wellington",
                remote: true);

            var result = _engine.Calculate(need, offering);

            Assert.Equal(10, result.Score.Location);
        }

        [Fact]
        public void Calculate_OfferingAfterDeadline_ShouldLoseAvailabilityScore()
        {
            var need = CreateNeed(
                category: "Web Development",
                skills: ["React"],
                deadline: new DateTime(2026, 10, 1));

            var offering = CreateOffering(
                category: "Web Development",
                skills: ["React"],
                availableFrom: new DateTime(2026, 10, 10));

            var result = _engine.Calculate(need, offering);

            Assert.Equal(0, result.Score.Availability);
        }

    }
}
