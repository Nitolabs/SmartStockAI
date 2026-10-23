using System.Linq.Expressions;
using SmartStockAI.Domain.Entities;
using SmartStockAI.Domain.Interfaces;

namespace SmartStockAI.Tests.Support;

/// <summary>
/// Repositorio en memoria para pruebas unitarias. Evalúa los predicados reales que usan los servicios,
/// de modo que el filtrado por BusinessId (aislamiento entre negocios) se prueba de verdad.
/// </summary>
public class FakeRepository<T> : IRepository<T> where T : BaseEntity
{
    private int _nextId = 1;

    public List<T> Items { get; } = new();
    public int SaveCount { get; private set; }

    public T Add(T entity)
    {
        if (entity.Id == 0) entity.Id = _nextId++;
        else _nextId = Math.Max(_nextId, entity.Id + 1);
        Items.Add(entity);
        return entity;
    }

    public Task<T?> GetByIdAsync(int id) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

    public Task<IReadOnlyList<T>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<T>>(Items.ToList());

    public Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        var filter = predicate.Compile();
        return Task.FromResult<IReadOnlyList<T>>(Items.Where(filter).ToList());
    }

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        var filter = predicate.Compile();
        return Task.FromResult(Items.FirstOrDefault(filter));
    }

    public Task AddAsync(T entity)
    {
        Add(entity);
        return Task.CompletedTask;
    }

    public void Update(T entity)
    {
        if (!Items.Contains(entity))
            throw new InvalidOperationException("Se intentó actualizar una entidad que no está en el repositorio.");
    }

    public void Remove(T entity) => Items.Remove(entity);

    public Task<int> SaveChangesAsync()
    {
        SaveCount++;
        return Task.FromResult(0);
    }
}

public class FakeUnitOfWork : IUnitOfWork
{
    public int Transactions { get; private set; }

    public Task<int> CompleteAsync() => Task.FromResult(0);

    public async Task ExecuteInTransactionAsync(Func<Task> operation)
    {
        Transactions++;
        await operation();
    }
}
