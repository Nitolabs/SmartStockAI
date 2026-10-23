using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStockAI.Domain.Entities;
using SmartStockAI.Infrastructure.Data;

namespace SmartStockAI.Tests.Integration;

/// <summary>
/// Base de datos SQLite en memoria con el modelo REAL de EF Core (ApplicationDbContext y sus configuraciones).
/// Una conexión compartida mantiene la base viva mientras dura la prueba; cada CreateContext() simula un request nuevo.
/// </summary>
public sealed class SqliteTestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteTestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new ApplicationDbContext(options);
    }

    /// <summary>Crea dos negocios (tenants) y devuelve sus ids.</summary>
    public async Task<(int Negocio1, int Negocio2)> SembrarNegociosAsync()
    {
        await using var ctx = CreateContext();
        var a = new Business { Nombre = "Negocio A" };
        var b = new Business { Nombre = "Negocio B" };
        ctx.Businesses.AddRange(a, b);
        await ctx.SaveChangesAsync();
        return (a.Id, b.Id);
    }

    public void Dispose() => _connection.Dispose();
}
