using Connecto.Domain.Entities;

namespace Connecto.Infrastructure.Services
{
    public interface IOfferingDbService
    {
        Task Add(Offering offering);
        
        Task<Offering?> GetById(Guid id);
        
        Task<List<Offering>> GetAll();
        
        IQueryable<Offering> Query();
        
        Task Update(Offering offering);
        
        Task Delete(Offering offering);
    }
}