using SmartStockAI.Application.DTOs;
using SmartStockAI.Domain.Entities;
using SmartStockAI.Domain.Enums;
using SmartStockAI.Tests.Support;
using Xunit;

namespace SmartStockAI.Tests.Services;

/// <summary>Reportes de productos más vendidos y movimientos, filtros por categoría/proveedor y compras recientes del dashboard.</summary>
public class ReporteNuevosTests
{
    private readonly TestWorld _w = new();

    private static ReporteFiltro Filtro(DateTime? desde = null, DateTime? hasta = null, int? producto = null, int? categoria = null, int? proveedor = null) =>
        new(desde, hasta, producto, categoria, proveedor);

    // ---------- Productos más vendidos ----------

    [Fact]
    public async Task MasVendidos_OrdenaPorCantidadYCalculaTotalYGanancia()
    {
        var arroz = _w.AddProducto(nombre: "Arroz", sku: "A", precioVenta: 10m, precioCompra: 6m);
        var frijol = _w.AddProducto(nombre: "Frijol", sku: "F", precioVenta: 5m, precioCompra: 2m);
        var hoy = DateTime.UtcNow;
        _w.AddVenta(TestWorld.Negocio1, hoy, EstadoDocumento.Completada, (arroz, 3, 0m), (frijol, 10, 0m));
        _w.AddVenta(TestWorld.Negocio1, hoy, EstadoDocumento.Completada, (arroz, 2, 0m));

        var r = await _w.ReporteService.ReporteProductosMasVendidosAsync(TestWorld.Negocio1, Filtro());

        Assert.Equal(new[] { "Frijol", "Arroz" }, r.Select(x => x.ProductoNombre).ToArray());
        Assert.Equal(10, r[0].CantidadVendida);
        Assert.Equal(50m, r[0].TotalVendido);
        Assert.Equal(30m, r[0].GananciaEstimada);   // 50 - 10*2
        Assert.Equal(5, r[1].CantidadVendida);
        Assert.Equal(20m, r[1].GananciaEstimada);   // 50 - 5*6 = 20
    }

    [Fact]
    public async Task MasVendidos_RespetaTopYExcluyeAnuladasYOtrosNegocios()
    {
        var hoy = DateTime.UtcNow;
        for (var i = 1; i <= 5; i++)
        {
            var p = _w.AddProducto(nombre: $"P{i}", sku: $"S{i}");
            _w.AddVenta(TestWorld.Negocio1, hoy, EstadoDocumento.Completada, (p, i, 0m));
        }
        var anulado = _w.AddProducto(nombre: "Anulado", sku: "X");
        _w.AddVenta(TestWorld.Negocio1, hoy, EstadoDocumento.Anulada, (anulado, 99, 0m));
        var ajeno = _w.AddProducto(businessId: TestWorld.Negocio2, nombre: "Ajeno", sku: "Y");
        _w.AddVenta(TestWorld.Negocio2, hoy, EstadoDocumento.Completada, (ajeno, 99, 0m));

        var r = await _w.ReporteService.ReporteProductosMasVendidosAsync(TestWorld.Negocio1, Filtro(), top: 3);

        Assert.Equal(new[] { "P5", "P4", "P3" }, r.Select(x => x.ProductoNombre).ToArray());
    }

    [Fact]
    public async Task MasVendidos_FiltraPorCategoriaProveedorYFechas()
    {
        var abarrotes = _w.AddCategoria(nombre: "Abarrotes");
        var limpieza = _w.AddCategoria(nombre: "Limpieza");
        var prov = _w.AddProveedor(nombre: "Prov");
        var arroz = _w.AddProducto(nombre: "Arroz", sku: "A", categoria: abarrotes, proveedor: prov);
        var cloro = _w.AddProducto(nombre: "Cloro", sku: "C", categoria: limpieza);
        var hoy = DateTime.UtcNow.Date.AddHours(10);
        _w.AddVenta(TestWorld.Negocio1, hoy, EstadoDocumento.Completada, (arroz, 1, 0m), (cloro, 1, 0m));
        _w.AddVenta(TestWorld.Negocio1, hoy.AddDays(-40), EstadoDocumento.Completada, (arroz, 7, 0m));

        var porCategoria = await _w.ReporteService.ReporteProductosMasVendidosAsync(TestWorld.Negocio1, Filtro(categoria: limpieza.Id));
        var porProveedor = await _w.ReporteService.ReporteProductosMasVendidosAsync(TestWorld.Negocio1, Filtro(proveedor: prov.Id, desde: hoy.AddDays(-5)));

        Assert.Equal("Cloro", Assert.Single(porCategoria).ProductoNombre);
        var fila = Assert.Single(porProveedor);
        Assert.Equal("Arroz", fila.ProductoNombre);
        Assert.Equal(1, fila.CantidadVendida); // la venta de hace 40 días queda fuera del rango
        Assert.Equal("Abarrotes", fila.CategoriaNombre);
    }

    // ---------- Filtros por categoría / proveedor en los reportes existentes ----------

    [Fact]
    public async Task Ventas_FiltraPorCategoriaYPorProveedorHabitualDelProducto()
    {
        var abarrotes = _w.AddCategoria(nombre: "Abarrotes");
        var limpieza = _w.AddCategoria(nombre: "Limpieza");
        var prov = _w.AddProveedor();
        var arroz = _w.AddProducto(nombre: "Arroz", sku: "A", categoria: abarrotes, proveedor: prov);
        var cloro = _w.AddProducto(nombre: "Cloro", sku: "C", categoria: limpieza);
        _w.AddVenta(TestWorld.Negocio1, DateTime.UtcNow, EstadoDocumento.Completada, (arroz, 1, 0m), (cloro, 1, 0m));

        var porCategoria = await _w.ReporteService.ReporteVentasAsync(TestWorld.Negocio1, Filtro(categoria: limpieza.Id));
        var porProveedor = await _w.ReporteService.ReporteVentasAsync(TestWorld.Negocio1, Filtro(proveedor: prov.Id));

        Assert.Equal("Cloro", Assert.Single(porCategoria).ProductoNombre);
        Assert.Equal("Arroz", Assert.Single(porProveedor).ProductoNombre);
    }

    [Fact]
    public async Task Compras_FiltraPorCategoriaDelProducto_YElProveedorEsElDeLaCompra()
    {
        var abarrotes = _w.AddCategoria(nombre: "Abarrotes");
        var limpieza = _w.AddCategoria(nombre: "Limpieza");
        var provA = _w.AddProveedor(nombre: "A");
        var provB = _w.AddProveedor(nombre: "B");
        var arroz = _w.AddProducto(nombre: "Arroz", sku: "A", categoria: abarrotes);
        var cloro = _w.AddProducto(nombre: "Cloro", sku: "C", categoria: limpieza);
        _w.AddCompra(TestWorld.Negocio1, DateTime.UtcNow, EstadoDocumento.Completada, provA, (arroz, 1, 1m), (cloro, 1, 1m));
        _w.AddCompra(TestWorld.Negocio1, DateTime.UtcNow, EstadoDocumento.Completada, provB, (cloro, 1, 1m));

        var porCategoria = await _w.ReporteService.ReporteComprasAsync(TestWorld.Negocio1, Filtro(categoria: limpieza.Id));
        var porProveedor = await _w.ReporteService.ReporteComprasAsync(TestWorld.Negocio1, Filtro(proveedor: provB.Id));

        Assert.Equal(2, porCategoria.Count);
        Assert.All(porCategoria, f => Assert.Equal("Cloro", f.ProductoNombre));
        Assert.Equal("B", Assert.Single(porProveedor).ProveedorNombre);
    }

    [Fact]
    public async Task Inventario_FiltraPorProveedorHabitual()
    {
        var prov = _w.AddProveedor();
        _w.AddProducto(nombre: "Con proveedor", sku: "1", proveedor: prov);
        _w.AddProducto(nombre: "Sin proveedor", sku: "2");

        var r = await _w.ReporteService.ReporteInventarioAsync(TestWorld.Negocio1, Filtro(proveedor: prov.Id));

        Assert.Equal("Con proveedor", Assert.Single(r).ProductoNombre);
    }

    [Fact]
    public async Task Ganancias_ConFiltroDeCategoria_SoloCuentaLasLineasDeEsaCategoria()
    {
        var abarrotes = _w.AddCategoria(nombre: "Abarrotes");
        var limpieza = _w.AddCategoria(nombre: "Limpieza");
        var arroz = _w.AddProducto(nombre: "Arroz", sku: "A", precioVenta: 10m, precioCompra: 6m, categoria: abarrotes);
        var cloro = _w.AddProducto(nombre: "Cloro", sku: "C", precioVenta: 4m, precioCompra: 1m, categoria: limpieza);
        _w.AddVenta(TestWorld.Negocio1, DateTime.UtcNow, EstadoDocumento.Completada, (arroz, 2, 0m), (cloro, 3, 0m));

        var r = await _w.ReporteService.ReporteGananciasAsync(TestWorld.Negocio1, Filtro(categoria: limpieza.Id));

        var fila = Assert.Single(r);
        Assert.Equal(12m, fila.TotalVentas);       // 3 x 4
        Assert.Equal(3m, fila.CostoEstimado);      // 3 x 1
        Assert.Equal(9m, fila.GananciaEstimada);
    }

    // ---------- Movimientos de inventario ----------

    [Fact]
    public async Task Movimientos_ListaDelNegocioConFiltrosYOrdenados()
    {
        var abarrotes = _w.AddCategoria(nombre: "Abarrotes");
        var limpieza = _w.AddCategoria(nombre: "Limpieza");
        var arroz = _w.AddProducto(nombre: "Arroz", sku: "A", categoria: abarrotes);
        var cloro = _w.AddProducto(nombre: "Cloro", sku: "C", categoria: limpieza);
        var ajeno = _w.AddProducto(businessId: TestWorld.Negocio2, nombre: "Ajeno", sku: "X");
        var ahora = DateTime.UtcNow;

        _w.Movimientos.Add(new MovimientoInventario { BusinessId = 1, ProductoId = arroz.Id, Producto = arroz, Tipo = TipoMovimientoInventario.Entrada, Motivo = MotivoMovimiento.Compra, Cantidad = 10, StockResultante = 10, Fecha = ahora.AddDays(-2), UsuarioNombre = "Ana" });
        _w.Movimientos.Add(new MovimientoInventario { BusinessId = 1, ProductoId = arroz.Id, Producto = arroz, Tipo = TipoMovimientoInventario.Salida, Motivo = MotivoMovimiento.Venta, Cantidad = 3, StockResultante = 7, Fecha = ahora.AddHours(-1) });
        _w.Movimientos.Add(new MovimientoInventario { BusinessId = 1, ProductoId = cloro.Id, Producto = cloro, Tipo = TipoMovimientoInventario.Entrada, Motivo = MotivoMovimiento.AjusteManual, Cantidad = 5, StockResultante = 5, Fecha = ahora });
        _w.Movimientos.Add(new MovimientoInventario { BusinessId = 2, ProductoId = ajeno.Id, Producto = ajeno, Tipo = TipoMovimientoInventario.Entrada, Motivo = MotivoMovimiento.Compra, Cantidad = 99, StockResultante = 99, Fecha = ahora });

        var todos = await _w.ReporteService.ReporteMovimientosAsync(TestWorld.Negocio1, Filtro());
        var deArroz = await _w.ReporteService.ReporteMovimientosAsync(TestWorld.Negocio1, Filtro(producto: arroz.Id));
        var deLimpieza = await _w.ReporteService.ReporteMovimientosAsync(TestWorld.Negocio1, Filtro(categoria: limpieza.Id));
        var recientes = await _w.ReporteService.ReporteMovimientosAsync(TestWorld.Negocio1, Filtro(desde: ahora.Date));

        Assert.Equal(3, todos.Count);
        Assert.Equal("Cloro", todos[0].ProductoNombre);                 // el más reciente primero
        Assert.Equal(2, deArroz.Count);
        Assert.Equal("Salida", deArroz[0].Tipo);
        Assert.Equal("Venta", deArroz[0].Motivo);
        Assert.Equal(7, deArroz[0].StockResultante);
        Assert.Equal("Cloro", Assert.Single(deLimpieza).ProductoNombre);
        Assert.DoesNotContain(todos, m => m.ProductoNombre == "Ajeno");
        Assert.True(recientes.Count >= 1 && recientes.All(m => m.Fecha.Date >= ahora.Date));
    }

    // ---------- Dashboard: compras recientes ----------

    [Fact]
    public async Task Dashboard_ListaLasUltimasComprasConProveedorYTotal_MasRecienteAlPrincipio()
    {
        var prov = _w.AddProveedor(nombre: "La Constancia");
        var p = _w.AddProducto();
        var ahora = DateTime.UtcNow;
        _w.AddCompra(TestWorld.Negocio1, ahora.AddDays(-4), EstadoDocumento.Completada, prov, (p, 1, 3m));
        _w.AddCompra(TestWorld.Negocio1, ahora.AddDays(-1), EstadoDocumento.Completada, prov, (p, 2, 5m));
        _w.AddCompra(TestWorld.Negocio1, ahora.AddDays(-30), EstadoDocumento.Completada, prov, (p, 1, 1m)); // fuera de los últimos 7 días
        _w.AddCompra(TestWorld.Negocio1, ahora.AddDays(-2), EstadoDocumento.Anulada, prov, (p, 1, 1m));

        var d = await _w.DashboardService.GetDashboardAsync(TestWorld.Negocio1);

        Assert.Equal(2, d.ComprasRecientes.Count);
        Assert.Equal(10m, d.ComprasRecientes[0].Total);
        Assert.Equal("La Constancia", d.ComprasRecientes[0].ProveedorNombre);
        Assert.Equal(3m, d.ComprasRecientes[1].Total);
        Assert.Equal(d.ComprasRecientesCount, d.ComprasRecientes.Count);
    }

    [Fact]
    public async Task Dashboard_ComprasRecientes_MuestraComoMaximo5()
    {
        var prov = _w.AddProveedor();
        var p = _w.AddProducto();
        for (var i = 0; i < 8; i++)
            _w.AddCompra(TestWorld.Negocio1, DateTime.UtcNow.AddHours(-i), EstadoDocumento.Completada, prov, (p, 1, 1m));

        var d = await _w.DashboardService.GetDashboardAsync(TestWorld.Negocio1);

        Assert.Equal(5, d.ComprasRecientes.Count);
        Assert.Equal(8, d.ComprasRecientesCount);
    }
}
