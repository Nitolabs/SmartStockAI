using Microsoft.EntityFrameworkCore.Storage;
using SmartStockAI.Domain.Interfaces;
using SmartStockAI.Infrastructure.Data;

namespace SmartStockAI.Infrastructure.Repositories;

public class EfUnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;

    public EfUnitOfWork(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<int> CompleteAsync() => _context.SaveChangesAsync();

    public async Task ExecuteInTransactionAsync(Func<Task> operation)
    {
        // Si ya existe una transacción activa (por ejemplo, en pruebas), no anidamos otra.
        if (_context.Database.CurrentTransaction is not null)
        {
            await operation();
            return;
        }

        IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await operation();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
