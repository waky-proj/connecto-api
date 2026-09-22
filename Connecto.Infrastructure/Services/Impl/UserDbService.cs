using Connecto.Domain.Entities;
using Connecto.Infrastructure.Services.Data;
using Microsoft.EntityFrameworkCore;

namespace Connecto.Infrastructure.Services.Impl
{
    public sealed class UserDbService(ConnectoDbContext dbContext) : IUserDbService
    {
        public async Task Add(User user)
        {
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
        }

        public async Task<User?> GetById(Guid id)
        {
            return await dbContext.Users.FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<List<User>> GetAll()
        {
            return await dbContext.Users.ToListAsync();
        }

        public async Task Update(User user)
        {
            dbContext.Users.Update(user);
            await dbContext.SaveChangesAsync();
        }

        public async Task Delete(User user)
        {
            dbContext.Users.Remove(user);
            await dbContext.SaveChangesAsync();
        }
    }
}
