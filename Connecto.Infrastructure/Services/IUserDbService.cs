using Connecto.Domain.Entities;

namespace Connecto.Infrastructure.Services
{
    public interface IUserDbService
    {
        Task Add(User user);
        Task<User?> GetById(Guid id);
        Task<List<User>> GetAll();
        Task Update(User user);
        Task Delete(User user);
    }
}
