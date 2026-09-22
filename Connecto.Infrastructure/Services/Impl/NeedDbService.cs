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

        public async Task Update(Need need)
        {
            dbContext.Needs.Update(need);
            await dbContext.SaveChangesAsync();
        }

        public async Task Delete(Need need)
        {
            dbContext.Needs.Remove(need);
            await dbContext.SaveChangesAsync();
        }
    }
}
