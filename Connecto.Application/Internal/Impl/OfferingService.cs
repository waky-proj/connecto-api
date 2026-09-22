using Connecto.Domain.Entities;
using Connecto.Domain.Requests;
using Connecto.Infrastructure.Services;

namespace Connecto.Application.Internal.Impl
{
    /// <summary>
    /// Provides business logic for creating and managing offerings.
    /// </summary>
    public sealed class OfferingService(IOfferingDbService offeringDbService, 
        IUserDbService userDbService) : IOfferingService
    {
        public async Task<Offering> CreateAsync(
            Guid userId,
            CreateOfferingRequest request,
            CancellationToken cancellationToken = default)
        {
            Validate(request);

            var user = await userDbService.GetById(userId);

            if (user == null)
                throw new KeyNotFoundException(
                    $"User '{userId}' was not found.");

            var offering = new Offering
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = request.Title.Trim(),
                Description = request.Description.Trim(),
                Category = request.Category.Trim(),
                PriceMin = request.PriceMin,
                PriceMax = request.PriceMax,
                Location = request.Location?.Trim(),
                RemoteAvailable = request.RemoteAvailable,
                AvailableFrom = request.AvailableFrom,
                AvailableUntil = request.AvailableUntil,
                ExperienceYears = request.ExperienceYears,
                Status = OfferingStatus.Active
            };

            foreach (var skill in request.Skills
                         .Select(x => {
                             if (Enum.TryParse<Skill>(x, out var skill))
                                 return skill;
                             return Skill.None;
                         })
                         .Where(x => x != Skill.None))
            {
                offering.Skills.Add(new OfferingSkill
                {
                    Id = Guid.NewGuid(),
                    OfferingId = offering.Id,
                    Skill = skill
                });
            }

            await offeringDbService.Add(offering);
            return offering;
        }

        private static void Validate(CreateOfferingRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                throw new ArgumentException("Title is required.");

            if (string.IsNullOrWhiteSpace(request.Description))
                throw new ArgumentException("Description is required.");

            if (string.IsNullOrWhiteSpace(request.Category))
                throw new ArgumentException("Category is required.");

            if (request.PriceMin.HasValue &&
                request.PriceMax.HasValue &&
                request.PriceMin > request.PriceMax)
            {
                throw new ArgumentException(
                    "PriceMin cannot be greater than PriceMax.");
            }

            if (request.ExperienceYears < 0)
                throw new ArgumentException(
                    "ExperienceYears cannot be negative.");
        }
    }
}
