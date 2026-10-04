using Connecto.Domain.Entities;

namespace Connecto.Infrastructure.Services
{
    public interface IOfferingDbService
    {
        Task Add(Offering offering);
        
        Task<Offering?> GetById(Guid id);
        
        Task<List<Offering>> GetAll();
        
        IQueryable<Offering> Query();
        
        Task<bool> Update(Offering offering, int expectedRevision);
        
        Task Delete(Offering offering);
    }
}
