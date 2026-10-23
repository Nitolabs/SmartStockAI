namespace SmartStockAI.Domain.Interfaces;

/// <summary>
/// Coordina múltiples repositorios dentro de una sola transacción lógica,
/// necesario para operaciones compuestas como "registrar venta + reducir
/// inventario" (sección 6.8 del prompt maestro).
/// </summary>
public interface IUnitOfWork
{
    Task<int> CompleteAsync();
    Task ExecuteInTransactionAsync(Func<Task> operation);
}
