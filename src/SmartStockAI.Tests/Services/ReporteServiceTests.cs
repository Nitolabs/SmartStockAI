using SmartStockAI.Application.DTOs;
using SmartStockAI.Domain.Enums;
using SmartStockAI.Tests.Support;
using Xunit;

namespace SmartStockAI.Tests.Services;

public class ReporteServiceTests
{
    private readonly TestWorld _w = new();

    private static ReporteFiltro Filtro(DateTime? desde = null, DateTime? hasta = null, int? producto = null, int? categoria = null, int? proveedor = null) =>
        new(desde, hasta, producto, categoria, proveedor);

    // ---------- Ventas ----------

    [Fact]
    public async Task Ventas_ExcluyeAnuladasYOtrosNegocios()
    {
        var p = _w.AddProducto();
        var ajeno = _w.AddProducto(businessId: TestWorld.Negocio2, sku: "X");
        var hoy = DateTime.UtcNow;
        _w.AddVenta(TestWorld.Negocio1, hoy, EstadoDocumento.Completada, (p, 2, 0m));
        _w.AddVenta(TestWorld.Negocio1, hoy, EstadoDocumento.Anulada, (p, 7, 0m));
        _w.AddVenta(TestWorld.Negocio2, hoy, EstadoDocumento.Completada, (ajeno, 7, 0m));

        var r = await _w.ReporteService.ReporteVentasAsync(TestWorld.Negocio1, Filtro());

        var fila = Assert.Single(r);
        Assert.Equal(2, fila.Cantidad);
        Assert.Equal(20m, fila.Total);
    }

    [Fact]
    public async Task Ventas_ElFiltroHastaIncluyeTodoElDiaFinal()
    {
        var p = _w.AddProducto();
        var tardeDeHoy = DateTime.UtcNow.Date.AddHours(15);
        _w.AddVenta(TestWorld.Negocio1, tardeDeHoy, EstadoDocumento.Completada, (p, 1, 0m));

        var r = await _w.ReporteService.ReporteVentasAsync(TestWorld.Negocio1,
            Filtro(desde: DateTime.UtcNow.Date, hasta: DateTime.UtcNow.Date));

        Assert.Single(r);
    }

    [Fact]
    public async Task Ventas_ElFiltroDesdeExcluyeLoAnterior()
    {
        var p = _w.AddProducto();
        var hoy = DateTime.UtcNow.Date.AddHours(10);
        _w.AddVenta(TestWorld.Negocio1, hoy.AddDays(-5), EstadoDocumento.Completada, (p, 1, 0m));
        _w.AddVenta(TestWorld.Negocio1, hoy, EstadoDocumento.Completada, (p, 1, 0m));

        var r = await _w.ReporteService.ReporteVentasAsync(TestWorld.Negocio1, Filtro(desde: hoy.Date.AddDays(-1)));

        Assert.Single(r);
    }

    [Fact]
    public async Task Ventas_FiltraPorProducto()
    {
        var a = _w.AddProducto(nombre: "A", sku: "A");
        var b = _w.AddProducto(nombre: "B", sku: "B");
        _w.AddVenta(TestWorld.Negocio1, DateTime.UtcNow, EstadoDocumento.Completada, (a, 1, 0m), (b, 1, 0m));

        var r = await _w.ReporteService.ReporteVentasAsync(TestWorld.Negocio1, Filtro(producto: b.Id));

        Assert.Equal("B", Assert.Single(r).ProductoNombre);
    }

    // ---------- Compras ----------

    [Fact]
    public async Task Compras_FiltraPorProveedorYCalculaTotalPorLinea()
    {
        var prov1 = _w.AddProveedor(nombre: "P1");
        var prov2 = _w.AddProveedor(nombre: "P2");
        var p = _w.AddProducto();
        _w.AddCompra(TestWorld.Negocio1, DateTime.UtcNow, EstadoDocumento.Completada, prov1, (p, 4, 2.5m));
        _w.AddCompra(TestWorld.Negocio1, DateTime.UtcNow, EstadoDocumento.Completada, prov2, (p, 1, 1m));
        _w.AddCompra(TestWorld.Negocio1, DateTime.UtcNow, EstadoDocumento.Anulada, prov1, (p, 9, 9m));

        var r = await _w.ReporteService.ReporteComprasAsync(TestWorld.Negocio1, Filtro(proveedor: prov1.Id));

        var fila = Assert.Single(r);
        Assert.Equal("P1", fila.ProveedorNombre);
        Assert.Equal(10m, fila.Total);
    }

    [Fact]
    public async Task Compras_NoIncluyeLasDeOtroNegocio()
    {
        var provAjeno = _w.AddProveedor(businessId: TestWorld.Negocio2);
        var ajeno = _w.AddProducto(businessId: TestWorld.Negocio2, sku: "X");
        _w.AddCompra(TestWorld.Negocio2, DateTime.UtcNow, EstadoDocumento.Completada, provAjeno, (ajeno, 1, 1m));

        var r = await _w.ReporteService.ReporteComprasAsync(TestWorld.Negocio1, Filtro());

        Assert.Empty(r);
    }

    // ---------- Inventario ----------

    [Fact]
    public async Task Inventario_FiltraPorCategoriaYCalculaValorEnStock()
    {
        var abarrotes = _w.AddCategoria(nombre: "Abarrotes");
        var limpieza = _w.AddCategoria(nombre: "Limpieza");
        _w.AddProducto(nombre: "Arroz", sku: "1", stock: 10, precioVenta: 2m, categoria: abarrotes);
        _w.AddProducto(nombre: "Cloro", sku: "2", stock: 5, precioVenta: 3m, categoria: limpieza);
        _w.AddProducto(nombre: "Viejo", sku: "3", stock: 5, precioVenta: 3m, categoria: limpieza, activo: false);

        var r = await _w.ReporteService.ReporteInventarioAsync(TestWorld.Negocio1, Filtro(categoria: limpieza.Id));

        var fila = Assert.Single(r);
        Assert.Equal("Cloro", fila.ProductoNombre);
        Assert.Equal("Limpieza", fila.CategoriaNombre);
        Assert.Equal(15m, fila.ValorStock);
    }

    // ---------- Baja rotación ----------

    [Fact]
    public async Task BajaRotacion_ListaProductosSinVentasEnLosUltimos30Dias()
    {
        var conVentaReciente = _w.AddProducto(nombre: "Reciente", sku: "1");
        var conVentaAntigua = _w.AddProducto(nombre: "Antigua", sku: "2");
        var sinVentas = _w.AddProducto(nombre: "Nunca", sku: "3");
        _w.AddProducto(businessId: TestWorld.Negocio2, nombre: "Ajeno", sku: "4");

        _w.AddVenta(TestWorld.Negocio1, DateTime.UtcNow.AddDays(-5), EstadoDocumento.Completada, (conVentaReciente, 1, 0m));
        _w.AddVenta(TestWorld.Negocio1, DateTime.UtcNow.AddDays(-45), EstadoDocumento.Completada, (conVentaAntigua, 1, 0m));

        var r = await _w.ReporteService.ReporteBajaRotacionAsync(TestWorld.Negocio1);

        Assert.Equal(new[] { "Antigua", "Nunca" }, r.Select(x => x.ProductoNombre).ToArray());
        Assert.NotNull(r.Single(x => x.ProductoNombre == "Antigua").UltimaVenta);
        Assert.Null(r.Single(x => x.ProductoNombre == "Nunca").UltimaVenta);
    }

    [Fact]
    public async Task BajaRotacion_UnaVentaAnuladaNoCuentaComoRotacion()
    {
        var p = _w.AddProducto(nombre: "Anulado");
        _w.AddVenta(TestWorld.Negocio1, DateTime.UtcNow.AddDays(-2), EstadoDocumento.Anulada, (p, 5, 0m));

        var r = await _w.ReporteService.ReporteBajaRotacionAsync(TestWorld.Negocio1);

        Assert.Equal("Anulado", Assert.Single(r).ProductoNombre);
    }

    // ---------- Ganancias ----------

    [Fact]
    public async Task Ganancias_RestaElCostoEstimadoDelTotalVendidoAgrupadoPorDia()
    {
        var p = _w.AddProducto(precioVenta: 10m, precioCompra: 6m);
        var dia = DateTime.UtcNow.Date.AddHours(9);
        _w.AddVenta(TestWorld.Negocio1, dia, EstadoDocumento.Completada, (p, 2, 0m));            // 20, costo 12
        _w.AddVenta(TestWorld.Negocio1, dia.AddHours(3), EstadoDocumento.Completada, (p, 1, 0m)); // 10, costo 6
        _w.AddVenta(TestWorld.Negocio1, dia, EstadoDocumento.Anulada, (p, 50, 0m));              // ✘

        var r = await _w.ReporteService.ReporteGananciasAsync(TestWorld.Negocio1, Filtro());

        var fila = Assert.Single(r);
        Assert.Equal(30m, fila.TotalVentas);
        Assert.Equal(18m, fila.CostoEstimado);
        Assert.Equal(12m, fila.GananciaEstimada);
    }
}
