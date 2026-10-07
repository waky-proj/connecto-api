using Connecto.Domain.Entities;
using Connecto.Infrastructure.Services.Data;
using Microsoft.EntityFrameworkCore;

namespace Connecto.Infrastructure.Services.Impl
{
    public class OfferingDbService(ConnectoDbContext dbContext) : IOfferingDbService
    {
        public async Task Add(Offering offering)
        {
            dbContext.Offerings.Add(offering);
            await dbContext.SaveChangesAsync();
        }

        public async Task<Offering?> GetById(Guid id)
        {
            return await dbContext.Offerings
                .Include(x => x.Skills)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<List<Offering>> GetAll()
        {
            return await dbContext.Offerings
                .Include(x => x.Skills)
                .ToListAsync();
        }

        public IQueryable<Offering> Query()
        {
            return dbContext.Offerings
                    .Include(x => x.Skills);
        }

        public async Task<bool> Update(Offering offering, int expectedRevision)
        {
            var changed = await dbContext.Offerings.Where(item => item.Id == offering.Id && item.Revision == expectedRevision)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Title, offering.Title).SetProperty(item => item.Description, offering.Description)
                    .SetProperty(item => item.Category, offering.Category).SetProperty(item => item.PriceMin, offering.PriceMin)
                    .SetProperty(item => item.PriceMax, offering.PriceMax).SetProperty(item => item.Currency, offering.Currency)
                    .SetProperty(item => item.PricingBasis, offering.PricingBasis).SetProperty(item => item.EstimatedHours, offering.EstimatedHours)
                    .SetProperty(item => item.Location, offering.Location).SetProperty(item => item.RemoteAvailable, offering.RemoteAvailable)
                    .SetProperty(item => item.AvailableFrom, offering.AvailableFrom).SetProperty(item => item.AvailableUntil, offering.AvailableUntil)
                    .SetProperty(item => item.ExperienceYears, offering.ExperienceYears).SetProperty(item => item.Status, offering.Status)
                    .SetProperty(item => item.Revision, offering.Revision));
            if (changed != 1) return false;
            dbContext.ChangeTracker.Clear();
            await dbContext.OfferingSkills.Where(item => item.OfferingId == offering.Id).ExecuteDeleteAsync();
            dbContext.OfferingSkills.AddRange(offering.Skills.Select(skill => new OfferingSkill
            {
                Id = skill.Id, OfferingId = offering.Id, Skill = skill.Skill
            }));
            await dbContext.SaveChangesAsync();
            return true;
        }

        public async Task Delete(Offering offering)
        {
            dbContext.Offerings.Remove(offering);
            await dbContext.SaveChangesAsync();
        }
    }
}
