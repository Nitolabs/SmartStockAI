using Microsoft.EntityFrameworkCore;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Services;
using SmartStockAI.Domain.Entities;
using SmartStockAI.Domain.Enums;
using SmartStockAI.Infrastructure.Data;
using SmartStockAI.Infrastructure.Repositories;
using Xunit;

namespace SmartStockAI.Tests.Integration;

/// <summary>
/// Flujo de negocio completo con los servicios reales sobre Entity Framework Core:
/// crear producto → comprar → vender → rechazar sobreventa → anular → aislamiento entre negocios.
/// </summary>
public class FlujoCompletoIntegrationTests : IDisposable
{
    private readonly SqliteTestDb _db = new();

    public void Dispose() => _db.Dispose();

    /// <summary>Servicios reales compartiendo un DbContext, igual que en un request con DI "Scoped".</summary>
    private sealed class Servicios
    {
        public Servicios(ApplicationDbContext ctx)
        {
            var uow = new EfUnitOfWork(ctx);
            var productos = new EfRepository<Producto>(ctx);
            var movimientos = new EfRepository<MovimientoInventario>(ctx);
            var ventas = new EfRepository<Venta>(ctx);
            var compras = new EfRepository<Compra>(ctx);
            var proveedores = new EfRepository<Proveedor>(ctx);
            var negocios = new EfRepository<Business>(ctx);

            Producto = new ProductoService(productos, movimientos, uow);
            Venta = new VentaService(ventas, productos, movimientos, negocios, uow);
            Compra = new CompraService(compras, productos, proveedores, movimientos, uow);
            Dashboard = new DashboardService(productos, ventas, compras);
        }

        public ProductoService Producto { get; }
        public VentaService Venta { get; }
        public CompraService Compra { get; }
        public DashboardService Dashboard { get; }
    }

    private async Task<(int Negocio1, int Negocio2, int CategoriaId, int ProveedorId)> PrepararAsync()
    {
        var (n1, n2) = await _db.SembrarNegociosAsync();
        await using var ctx = _db.CreateContext();
        var categoria = new Categoria { BusinessId = n1, Nombre = "Abarrotes" };
        var proveedor = new Proveedor { BusinessId = n1, Nombre = "La Constancia" };
        ctx.AddRange(categoria, proveedor);
        await ctx.SaveChangesAsync();
        return (n1, n2, categoria.Id, proveedor.Id);
    }

    [Fact]
    public async Task CicloCompletoDeInventario_QuedaConsistenteEnLaBaseDeDatos()
    {
        var (n1, _, categoriaId, proveedorId) = await PrepararAsync();
        int productoId, ventaId;

        await using (var ctx = _db.CreateContext())
        {
            var s = new Servicios(ctx);

            // 1) Crear producto (stock 0)
            var creado = await s.Producto.CreateAsync(n1, "u1", "Ana",
                new ProductoCreateDto("Arroz", null, "ABR-1", categoriaId, proveedorId, 0.6m, 5m, 0, 2));
            Assert.True(creado.Success);
            productoId = creado.Value!.Id;

            // 2) Comprar 10 unidades a $2 -> stock 10
            var compra = await s.Compra.RegistrarCompraAsync(n1, "u1", "Ana",
                new CompraCreateDto(proveedorId, new[] { new CompraDetalleCreateDto(productoId, 10, 2m) }));
            Assert.True(compra.Success);

            // 3) Vender 4 con $1 de descuento -> stock 6, total 19
            var venta = await s.Venta.RegistrarVentaAsync(n1, "u1", "Ana",
                new VentaCreateDto(MetodoPago.Efectivo, 0m, new[] { new VentaDetalleCreateDto(productoId, 4, 1m) }));
            Assert.True(venta.Success);
            ventaId = venta.Value!.Id;
            Assert.Equal(19m, venta.Value.Total);

            // 4) Intentar vender 7 (solo quedan 6) -> rechazada
            var sobreventa = await s.Venta.RegistrarVentaAsync(n1, "u1", "Ana",
                new VentaCreateDto(MetodoPago.Efectivo, 0m, new[] { new VentaDetalleCreateDto(productoId, 7, 0m) }));
            Assert.False(sobreventa.Success);
        }

        // Verificación con un contexto NUEVO: lo que quedó realmente persistido
        await using (var verificacion = _db.CreateContext())
        {
            var producto = await verificacion.Productos.SingleAsync();
            Assert.Equal(6, producto.StockActual);
            Assert.Equal(2m, producto.PrecioCompra); // la compra actualizó el costo

            // El repositorio carga la categoría (Include) al consultar el producto
            var dto = await new Servicios(verificacion).Producto.GetByIdAsync(n1, productoId);
            Assert.Equal("Abarrotes", dto!.CategoriaNombre);

            Assert.Equal(1, await verificacion.Ventas.CountAsync());
            Assert.Equal(1, await verificacion.Compras.CountAsync());

            var movimientos = await verificacion.MovimientosInventario.OrderBy(m => m.Id).ToListAsync();
            Assert.Equal(2, movimientos.Count); // entrada por compra + salida por venta (la sobreventa no dejó nada)
            Assert.Equal(MotivoMovimiento.Compra, movimientos[0].Motivo);
            Assert.Equal(MotivoMovimiento.Venta, movimientos[1].Motivo);
            Assert.Equal(6, movimientos[1].StockResultante);
        }

        // 5) Anular la venta -> el stock vuelve a 10 y queda registro de la devolución
        await using (var ctx = _db.CreateContext())
        {
            var s = new Servicios(ctx);
            var anulada = await s.Venta.AnularVentaAsync(n1, ventaId);
            Assert.True(anulada.Success);
        }

        await using (var verificacion = _db.CreateContext())
        {
            Assert.Equal(10, (await verificacion.Productos.SingleAsync()).StockActual);
            Assert.Equal(EstadoDocumento.Anulada, (await verificacion.Ventas.SingleAsync()).Estado);
            Assert.Equal(3, await verificacion.MovimientosInventario.CountAsync());
        }
    }

    [Fact]
    public async Task AislamientoEntreNegocios_UnNegocioNoVeNiModificaDatosDelOtro()
    {
        var (n1, n2, categoriaId, proveedorId) = await PrepararAsync();
        int productoId;

        await using (var ctx = _db.CreateContext())
        {
            var s = new Servicios(ctx);
            var creado = await s.Producto.CreateAsync(n1, "u1", "Ana",
                new ProductoCreateDto("Arroz", null, "ABR-1", categoriaId, proveedorId, 1m, 5m, 20, 2));
            productoId = creado.Value!.Id;
            await s.Venta.RegistrarVentaAsync(n1, "u1", "Ana",
                new VentaCreateDto(MetodoPago.Efectivo, 0m, new[] { new VentaDetalleCreateDto(productoId, 2, 0m) }));
        }

        await using (var ctx = _db.CreateContext())
        {
            var s = new Servicios(ctx);

            // El negocio 2 no ve el producto ni sus ventas...
            Assert.Null(await s.Producto.GetByIdAsync(n2, productoId));
            var busqueda = await s.Producto.BuscarAsync(n2, new ProductoBusquedaFiltro(null, null, null));
            Assert.Equal(0, busqueda.TotalCount);
            var dash = await s.Dashboard.GetDashboardAsync(n2);
            Assert.Equal(0m, dash.VentasMes);
            Assert.Equal(0m, dash.ValorInventario);

            // ...ni puede venderlo ni editarlo.
            var venta = await s.Venta.RegistrarVentaAsync(n2, "u2", "Luis",
                new VentaCreateDto(MetodoPago.Efectivo, 0m, new[] { new VentaDetalleCreateDto(productoId, 1, 0m) }));
            Assert.False(venta.Success);

            var edicion = await s.Producto.UpdateAsync(n2,
                new ProductoUpdateDto(productoId, "Hackeado", null, "ABR-1", categoriaId, null, 1m, 1m, 1, true));
            Assert.False(edicion.Success);
        }

        await using var verificacion = _db.CreateContext();
        var producto = await verificacion.Productos.SingleAsync();
        Assert.Equal("Arroz", producto.Nombre);
        Assert.Equal(18, producto.StockActual); // solo la venta legítima del negocio 1
    }

    [Fact]
    public async Task Dashboard_ConDatosReales_CalculaVentasEInventario()
    {
        var (n1, _, categoriaId, proveedorId) = await PrepararAsync();

        await using var ctx = _db.CreateContext();
        var s = new Servicios(ctx);
        var p = (await s.Producto.CreateAsync(n1, "u1", "Ana",
            new ProductoCreateDto("Arroz", null, "ABR-1", categoriaId, proveedorId, 1m, 5m, 10, 3))).Value!;
        await s.Venta.RegistrarVentaAsync(n1, "u1", "Ana",
            new VentaCreateDto(MetodoPago.Efectivo, 0m, new[] { new VentaDetalleCreateDto(p.Id, 8, 0m) }));

        var d = await s.Dashboard.GetDashboardAsync(n1);

        Assert.Equal(40m, d.VentasHoy);
        Assert.Equal(8, d.ProductosVendidosHoy);
        Assert.Equal(10m, d.ValorInventario);      // 2 unidades x $5
        Assert.Equal(1, d.ProductosStockBajo);     // 2 <= 3
        Assert.Equal("Arroz", Assert.Single(d.ProductosMasVendidos).ProductoNombre);
    }
}
