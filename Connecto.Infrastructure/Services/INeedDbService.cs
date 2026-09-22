using Connecto.Domain.Entities;

namespace Connecto.Infrastructure.Services
{
    public interface INeedDbService
    {

        Task Add(Need need);
        Task<Need?> GetById(Guid id);
        Task<List<Need>> GetAll();
        Task Update(Need need);
        Task Delete(Need need);
    }
}