using Connecto.Domain.Entities;
using Connecto.Domain.Requests;
using Connecto.Infrastructure.Services;

namespace Connecto.Application.Internal.Impl
{
    /// <summary>
    /// Provides business logic for creating and managing user needs.
    /// </summary>
    public sealed class NeedService(INeedDbService needDbService, IUserDbService userDbService) : INeedService
    {
        public async Task<Need> CreateAsync(
            Guid userId,
            CreateNeedRequest request,
            CancellationToken cancellationToken = default)
        {
            Validate(request);

            var userExists = await userDbService.GetById(userId);

            if (userExists == null)
                throw new KeyNotFoundException(
                    $"User '{userId}' was not found.");

            var need = new Need
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = request.Title.Trim(),
                Description = request.Description.Trim(),
                Category = request.Category.Trim(),
                BudgetMin = request.BudgetMin,
                BudgetMax = request.BudgetMax,
                Location = request.Location.Trim(),
                Deadline = request.Deadline,
                Status = NeedStatus.Open
            };

            foreach (var skill in request.Skills
                         .Select(x => {
                             if (Enum.TryParse<Skill>(x, out var skill))
                                 return skill;
                             return Skill.None;
                         })
                         .Where(x => x != Skill.None))
            {
                need.Skills.Add(new NeedSkill
                {
                    Id = Guid.NewGuid(),
                    NeedId = need.Id,
                    Skill = skill
                });
            }

            await needDbService.Add(need);
            return need;
        }

        private static void Validate(CreateNeedRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                throw new ArgumentException("Title is required.");

            if (string.IsNullOrWhiteSpace(request.Description))
                throw new ArgumentException("Description is required.");

            if (string.IsNullOrWhiteSpace(request.Category))
                throw new ArgumentException("Category is required.");

            if (request.BudgetMin.HasValue &&
                request.BudgetMax.HasValue &&
                request.BudgetMin > request.BudgetMax)
            {
                throw new ArgumentException(
                    "BudgetMin cannot be greater than BudgetMax.");
            }
        }
    }
}
