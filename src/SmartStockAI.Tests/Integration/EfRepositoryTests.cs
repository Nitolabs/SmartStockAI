using Microsoft.EntityFrameworkCore;
using SmartStockAI.Domain.Entities;
using SmartStockAI.Domain.Enums;
using SmartStockAI.Infrastructure.Repositories;
using Xunit;

namespace SmartStockAI.Tests.Integration;

/// <summary>Pruebas de integración de Entity Framework Core: mapeo, relaciones, índices y transacciones.</summary>
public class EfRepositoryTests : IDisposable
{
    private readonly SqliteTestDb _db = new();

    public void Dispose() => _db.Dispose();

    private async Task<(int Negocio, int CategoriaId, int ProveedorId)> SembrarCatalogoAsync(int negocio)
    {
        await using var ctx = _db.CreateContext();
        var categoria = new Categoria { BusinessId = negocio, Nombre = "Abarrotes" };
        var proveedor = new Proveedor { BusinessId = negocio, Nombre = "La Constancia" };
        ctx.AddRange(categoria, proveedor);
        await ctx.SaveChangesAsync();
        return (negocio, categoria.Id, proveedor.Id);
    }

    [Fact]
    public async Task Producto_SeGuardaConSusRelacionesYElRepositorioLasCarga()
    {
        var (n1, _) = await _db.SembrarNegociosAsync();
        var (_, categoriaId, proveedorId) = await SembrarCatalogoAsync(n1);

        await using (var ctx = _db.CreateContext())
        {
            ctx.Productos.Add(new Producto
            {
                BusinessId = n1, Nombre = "Arroz", Sku = "ABR-1", CategoriaId = categoriaId, ProveedorId = proveedorId,
                PrecioCompra = 0.6m, PrecioVenta = 0.95m, StockActual = 10, StockMinimo = 3
            });
            await ctx.SaveChangesAsync();
        }

        await using var lectura = _db.CreateContext();
        var repo = new EfRepository<Producto>(lectura);
        var producto = (await repo.FindAsync(p => p.BusinessId == n1)).Single();

        Assert.Equal("Abarrotes", producto.Categoria.Nombre);
        Assert.Equal("La Constancia", producto.Proveedor!.Nombre);
        Assert.Equal(0.95m, producto.PrecioVenta);
    }

    [Fact]
    public async Task Repositorio_DeVentas_CargaDetallesConSuProducto()
    {
        var (n1, _) = await _db.SembrarNegociosAsync();
        var (_, categoriaId, _) = await SembrarCatalogoAsync(n1);

        await using (var ctx = _db.CreateContext())
        {
            var producto = new Producto { BusinessId = n1, Nombre = "Arroz", Sku = "A", CategoriaId = categoriaId, PrecioVenta = 2m, StockActual = 5 };
            ctx.Productos.Add(producto);
            await ctx.SaveChangesAsync();

            var venta = new Venta { BusinessId = n1, NumeroDocumento = "V-1", Total = 4m, Estado = EstadoDocumento.Completada };
            venta.Detalles.Add(new VentaDetalle { ProductoId = producto.Id, Cantidad = 2, PrecioUnitario = 2m });
            ctx.Ventas.Add(venta);
            await ctx.SaveChangesAsync();
        }

        await using var lectura = _db.CreateContext();
        var ventas = await new EfRepository<Venta>(lectura).FindAsync(v => v.BusinessId == n1);

        var detalle = Assert.Single(Assert.Single(ventas).Detalles);
        Assert.Equal("Arroz", detalle.Producto.Nombre);
    }

    [Fact]
    public async Task Repositorio_DeCompras_CargaProveedorYDetalles()
    {
        var (n1, _) = await _db.SembrarNegociosAsync();
        var (_, categoriaId, proveedorId) = await SembrarCatalogoAsync(n1);

        await using (var ctx = _db.CreateContext())
        {
            var producto = new Producto { BusinessId = n1, Nombre = "Arroz", Sku = "A", CategoriaId = categoriaId };
            ctx.Productos.Add(producto);
            await ctx.SaveChangesAsync();

            var compra = new Compra { BusinessId = n1, NumeroDocumento = "C-1", ProveedorId = proveedorId, Total = 10m };
            compra.Detalles.Add(new CompraDetalle { ProductoId = producto.Id, Cantidad = 5, CostoUnitario = 2m });
            ctx.Compras.Add(compra);
            await ctx.SaveChangesAsync();
        }

        await using var lectura = _db.CreateContext();
        var compras = await new EfRepository<Compra>(lectura).FindAsync(c => c.BusinessId == n1);

        var c = Assert.Single(compras);
        Assert.Equal("La Constancia", c.Proveedor.Nombre);
        Assert.Equal("Arroz", Assert.Single(c.Detalles).Producto.Nombre);
    }

    [Fact]
    public async Task Repositorio_FiltraPorNegocio_NoMezclaDatosDeOtroTenant()
    {
        var (n1, n2) = await _db.SembrarNegociosAsync();

        await using (var ctx = _db.CreateContext())
        {
            ctx.Categorias.AddRange(
                new Categoria { BusinessId = n1, Nombre = "De A" },
                new Categoria { BusinessId = n2, Nombre = "De B" });
            await ctx.SaveChangesAsync();
        }

        await using var lectura = _db.CreateContext();
        var repo = new EfRepository<Categoria>(lectura);

        var deA = await repo.FindAsync(c => c.BusinessId == n1);

        Assert.Equal("De A", Assert.Single(deA).Nombre);
    }

    [Fact]
    public async Task Sku_EsUnicoPorNegocio_PeroPuedeRepetirseEnOtroNegocio()
    {
        var (n1, n2) = await _db.SembrarNegociosAsync();
        var (_, cat1, _) = await SembrarCatalogoAsync(n1);
        var (_, cat2, _) = await SembrarCatalogoAsync(n2);

        await using (var ctx = _db.CreateContext())
        {
            ctx.Productos.AddRange(
                new Producto { BusinessId = n1, Nombre = "Arroz A", Sku = "ABR-1", CategoriaId = cat1 },
                new Producto { BusinessId = n2, Nombre = "Arroz B", Sku = "ABR-1", CategoriaId = cat2 });
            await ctx.SaveChangesAsync(); // mismo SKU en negocios distintos: permitido
        }

        await using var duplicado = _db.CreateContext();
        duplicado.Productos.Add(new Producto { BusinessId = n1, Nombre = "Otro", Sku = "ABR-1", CategoriaId = cat1 });

        await Assert.ThrowsAsync<DbUpdateException>(() => duplicado.SaveChangesAsync());
    }

    [Fact]
    public async Task Montos_DecimalesSeGuardanSinPerderPrecision()
    {
        var (n1, _) = await _db.SembrarNegociosAsync();
        var (_, categoriaId, _) = await SembrarCatalogoAsync(n1);

        await using (var ctx = _db.CreateContext())
        {
            ctx.Productos.Add(new Producto { BusinessId = n1, Nombre = "P", Sku = "P", CategoriaId = categoriaId, PrecioCompra = 1.25m, PrecioVenta = 3.99m });
            await ctx.SaveChangesAsync();
        }

        await using var lectura = _db.CreateContext();
        var p = await lectura.Productos.SingleAsync();

        Assert.Equal(1.25m, p.PrecioCompra);
        Assert.Equal(3.99m, p.PrecioVenta);
    }

    [Fact]
    public async Task UnitOfWork_SiLaOperacionFalla_HaceRollbackDeLoYaGuardado()
    {
        var (n1, _) = await _db.SembrarNegociosAsync();

        await using (var ctx = _db.CreateContext())
        {
            var uow = new EfUnitOfWork(ctx);
            var repo = new EfRepository<Categoria>(ctx);

            await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteInTransactionAsync(async () =>
            {
                await repo.AddAsync(new Categoria { BusinessId = n1, Nombre = "No debe quedar" });
                await repo.SaveChangesAsync();
                throw new InvalidOperationException("fallo a mitad de la operación");
            }));
        }

        await using var verificacion = _db.CreateContext();
        Assert.Empty(await verificacion.Categorias.ToListAsync());
    }

    [Fact]
    public async Task UnitOfWork_SiTodoSale_Bien_HaceCommit()
    {
        var (n1, _) = await _db.SembrarNegociosAsync();

        await using (var ctx = _db.CreateContext())
        {
            var uow = new EfUnitOfWork(ctx);
            var repo = new EfRepository<Categoria>(ctx);

            await uow.ExecuteInTransactionAsync(async () =>
            {
                await repo.AddAsync(new Categoria { BusinessId = n1, Nombre = "Sí queda" });
                await repo.SaveChangesAsync();
            });
        }

        await using var verificacion = _db.CreateContext();
        Assert.Equal("Sí queda", (await verificacion.Categorias.SingleAsync()).Nombre);
    }
}
