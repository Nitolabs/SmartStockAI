using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SmartStockAI.Domain.Interfaces;
using SmartStockAI.Infrastructure.Data;

namespace SmartStockAI.Infrastructure.Repositories;

/// <summary>
/// Implementación EF Core genérica de IRepository&lt;T&gt;. Aplica
/// automáticamente los Include necesarios según el tipo de entidad para
/// mantener a los servicios de Application desacoplados de EF Core.
/// </summary>
public class EfRepository<T> : IRepository<T> where T : class
{
    protected readonly ApplicationDbContext Context;
    protected readonly DbSet<T> Set;

    public EfRepository(ApplicationDbContext context)
    {
        Context = context;
        Set = context.Set<T>();
    }

    protected virtual IQueryable<T> Query() => IncludeGraph(Set.AsQueryable());

    /// <summary>Centraliza los .Include() de navegación por tipo de entidad.</summary>
    private IQueryable<T> IncludeGraph(IQueryable<T> query)
    {
        if (typeof(T) == typeof(Domain.Entities.Producto))
        {
            var q = (IQueryable<Domain.Entities.Producto>)(object)query;
            q = q.Include(p => p.Categoria).Include(p => p.Proveedor);
            return (IQueryable<T>)(object)q;
        }
        if (typeof(T) == typeof(Domain.Entities.MovimientoInventario))
        {
            var q = (IQueryable<Domain.Entities.MovimientoInventario>)(object)query;
            q = q.Include(m => m.Producto);
            return (IQueryable<T>)(object)q;
        }
        if (typeof(T) == typeof(Domain.Entities.Venta))
        {
            var q = (IQueryable<Domain.Entities.Venta>)(object)query;
            q = q.Include(v => v.Detalles).ThenInclude(d => d.Producto);
            return (IQueryable<T>)(object)q;
        }
        if (typeof(T) == typeof(Domain.Entities.Compra))
        {
            var q = (IQueryable<Domain.Entities.Compra>)(object)query;
            q = q.Include(c => c.Detalles).ThenInclude(d => d.Producto).Include(c => c.Proveedor);
            return (IQueryable<T>)(object)q;
        }
        if (typeof(T) == typeof(Domain.Entities.Categoria))
        {
            var q = (IQueryable<Domain.Entities.Categoria>)(object)query;
            q = q.Include(c => c.Productos);
            return (IQueryable<T>)(object)q;
        }
        return query;
    }

    public async Task<T?> GetByIdAsync(int id) =>
        await Query().FirstOrDefaultAsync(BuildIdPredicate(id));

    public async Task<IReadOnlyList<T>> GetAllAsync() =>
        await Query().ToListAsync();

    public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate) =>
        await Query().Where(predicate).ToListAsync();

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate) =>
        await Query().FirstOrDefaultAsync(predicate);

    public async Task AddAsync(T entity) => await Set.AddAsync(entity);

    public void Update(T entity) => Set.Update(entity);

    public void Remove(T entity) => Set.Remove(entity);

    public async Task<int> SaveChangesAsync() => await Context.SaveChangesAsync();

    private static Expression<Func<T, bool>> BuildIdPredicate(int id)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, "Id");
        var constant = Expression.Constant(id);
        var equal = Expression.Equal(property, constant);
        return Expression.Lambda<Func<T, bool>>(equal, parameter);
    }
}
