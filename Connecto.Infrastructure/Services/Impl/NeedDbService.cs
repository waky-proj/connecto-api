using Connecto.Domain.Entities;
using Connecto.Infrastructure.Services.Data;
using Microsoft.EntityFrameworkCore;

namespace Connecto.Infrastructure.Services.Impl
{
    public class NeedDbService(ConnectoDbContext dbContext) : INeedDbService
    {
        public async Task Add(Need need)
        {
            dbContext.Needs.Add(need);
            await dbContext.SaveChangesAsync();
        }

        public async Task<Need?> GetById(Guid id)
        {
            return await dbContext.Needs
                .Include(x => x.Skills)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<List<Need>> GetAll()
        {
            return await dbContext.Needs
                .Include(x => x.Skills)
                .ToListAsync();
        }

        public async Task<bool> Update(Need need, int expectedRevision)
        {
            var changed = await dbContext.Needs.Where(item => item.Id == need.Id && item.Revision == expectedRevision)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Title, need.Title).SetProperty(item => item.Description, need.Description)
                    .SetProperty(item => item.Category, need.Category).SetProperty(item => item.BudgetMin, need.BudgetMin)
                    .SetProperty(item => item.BudgetMax, need.BudgetMax).SetProperty(item => item.Currency, need.Currency)
                    .SetProperty(item => item.PricingBasis, need.PricingBasis).SetProperty(item => item.EstimatedHours, need.EstimatedHours)
                    .SetProperty(item => item.Location, need.Location).SetProperty(item => item.Deadline, need.Deadline)
                    .SetProperty(item => item.Status, need.Status).SetProperty(item => item.Revision, need.Revision));
            if (changed != 1) return false;
            dbContext.ChangeTracker.Clear();
            await dbContext.NeedSkills.Where(item => item.NeedId == need.Id).ExecuteDeleteAsync();
            dbContext.NeedSkills.AddRange(need.Skills.Select(skill => new NeedSkill
            {
                Id = skill.Id, NeedId = need.Id, Skill = skill.Skill
            }));
            await dbContext.SaveChangesAsync();
            return true;
        }

        public async Task Delete(Need need)
        {
            dbContext.Needs.Remove(need);
            await dbContext.SaveChangesAsync();
        }
    }
}
