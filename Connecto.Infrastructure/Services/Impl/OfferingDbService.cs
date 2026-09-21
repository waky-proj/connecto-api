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

        public async Task Update(Offering offering)
        {
            dbContext.Offerings.Update(offering);
            await dbContext.SaveChangesAsync();
        }

        public async Task Delete(Offering offering)
        {
            dbContext.Offerings.Remove(offering);
            await dbContext.SaveChangesAsync();
        }
    }
}
