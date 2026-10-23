using System.Linq.Expressions;

namespace SmartStockAI.Domain.Interfaces;

/// <summary>
/// Contrato genérico de acceso a datos. La implementación concreta vive en
/// SmartStockAI.Infrastructure.Repositories y usa Entity Framework Core;
/// ni Domain ni Application conocen ese detalle (Dependency Inversion).
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IReadOnlyList<T>> GetAllAsync();
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
    Task AddAsync(T entity);
    void Update(T entity);
    void Remove(T entity);
    Task<int> SaveChangesAsync();
}
