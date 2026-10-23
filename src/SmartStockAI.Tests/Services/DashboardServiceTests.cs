using SmartStockAI.Domain.Enums;
using SmartStockAI.Tests.Support;
using Xunit;

namespace SmartStockAI.Tests.Services;

public class DashboardServiceTests
{
    private readonly TestWorld _w = new();

    [Fact]
    public async Task Ventas_DeHoyYDelMes_SoloCuentanCompletadasDelNegocioActual()
    {
        var p = _w.AddProducto(precioVenta: 10m);
        var ajeno = _w.AddProducto(businessId: TestWorld.Negocio2, sku: "X", precioVenta: 10m);
        var hoy = DateTime.UtcNow;

        _w.AddVenta(TestWorld.Negocio1, hoy, EstadoDocumento.Completada, (p, 2, 0m));                   // 20 ✔
        _w.AddVenta(TestWorld.Negocio1, hoy, EstadoDocumento.Anulada, (p, 9, 0m));                      // anulada ✘
        _w.AddVenta(TestWorld.Negocio2, hoy, EstadoDocumento.Completada, (ajeno, 9, 0m));               // otro negocio ✘
        _w.AddVenta(TestWorld.Negocio1, hoy.AddMonths(-2), EstadoDocumento.Completada, (p, 5, 0m));     // mes pasado: no cuenta en el mes

        var d = await _w.DashboardService.GetDashboardAsync(TestWorld.Negocio1);

        Assert.Equal(20m, d.VentasHoy);
        Assert.Equal(20m, d.VentasMes);
        Assert.Equal(2, d.ProductosVendidosHoy);
    }

    [Fact]
    public async Task VentasDeAyer_NoCuentanComoVentasDeHoy()
    {
        var p = _w.AddProducto(precioVenta: 10m);
        _w.AddVenta(TestWorld.Negocio1, DateTime.UtcNow.AddDays(-1), EstadoDocumento.Completada, (p, 3, 0m));

        var d = await _w.DashboardService.GetDashboardAsync(TestWorld.Negocio1);

        Assert.Equal(0m, d.VentasHoy);
        Assert.Equal(0, d.ProductosVendidosHoy);
    }

    [Fact]
    public async Task ValorDelInventario_SumaStockPorPrecioDeVentaSoloDeActivosDelNegocio()
    {
        _w.AddProducto(sku: "1", stock: 10, precioVenta: 2m);                          // 20
        _w.AddProducto(sku: "2", stock: 5, precioVenta: 4m);                           // 20
        _w.AddProducto(sku: "3", stock: 100, precioVenta: 100m, activo: false);        // ✘
        _w.AddProducto(businessId: TestWorld.Negocio2, sku: "4", stock: 100, precioVenta: 100m); // ✘

        var d = await _w.DashboardService.GetDashboardAsync(TestWorld.Negocio1);

        Assert.Equal(40m, d.ValorInventario);
    }

    [Fact]
    public async Task Alertas_DeterministasParaStockBajoYAgotado_ConPrioridad()
    {
        _w.AddProducto(nombre: "Sano", sku: "1", stock: 50, stockMinimo: 5);
        _w.AddProducto(nombre: "Bajo", sku: "2", stock: 3, stockMinimo: 5);
        _w.AddProducto(nombre: "Agotado", sku: "3", stock: 0, stockMinimo: 5);
        _w.AddProducto(businessId: TestWorld.Negocio2, nombre: "Ajeno", sku: "4", stock: 0, stockMinimo: 5);

        var d = await _w.DashboardService.GetDashboardAsync(TestWorld.Negocio1);

        Assert.Equal(2, d.ProductosStockBajo);
        Assert.Equal(2, d.Alertas.Count);

        var primera = d.Alertas[0]; // ordenadas por stock ascendente: el agotado primero
        Assert.Equal("Agotado", primera.ProductoNombre);
        Assert.Equal("SinStock", primera.Tipo);
        Assert.Equal("ALTA", primera.Prioridad);

        var segunda = d.Alertas[1];
        Assert.Equal("Bajo", segunda.ProductoNombre);
        Assert.Equal("StockBajo", segunda.Tipo);
        Assert.Equal("MEDIA", segunda.Prioridad);
    }

    [Fact]
    public async Task Alerta_SeGeneraTambienCuandoElStockIgualaElMinimo()
    {
        _w.AddProducto(nombre: "EnElLimite", stock: 5, stockMinimo: 5);

        var d = await _w.DashboardService.GetDashboardAsync(TestWorld.Negocio1);

        Assert.Equal("EnElLimite", Assert.Single(d.Alertas).ProductoNombre);
    }

    [Fact]
    public async Task MasVendidos_EsTop5OrdenadoPorCantidad()
    {
        var hoy = DateTime.UtcNow;
        for (var i = 1; i <= 6; i++)
        {
            var p = _w.AddProducto(nombre: $"Prod{i}", sku: $"S{i}", stock: 100, precioVenta: 1m);
            _w.AddVenta(TestWorld.Negocio1, hoy, EstadoDocumento.Completada, (p, i * 10, 0m));
        }

        var d = await _w.DashboardService.GetDashboardAsync(TestWorld.Negocio1);

        Assert.Equal(5, d.ProductosMasVendidos.Count);
        Assert.Equal("Prod6", d.ProductosMasVendidos[0].ProductoNombre);
        Assert.Equal(60, d.ProductosMasVendidos[0].CantidadVendida);
        Assert.DoesNotContain(d.ProductosMasVendidos, x => x.ProductoNombre == "Prod1");
    }

    [Fact]
    public async Task Tendencia_TieneSieteDiasTerminandoHoyConElTotalDeCadaDia()
    {
        var p = _w.AddProducto(precioVenta: 10m);
        _w.AddVenta(TestWorld.Negocio1, DateTime.UtcNow, EstadoDocumento.Completada, (p, 2, 0m));                // hoy 20
        _w.AddVenta(TestWorld.Negocio1, DateTime.UtcNow.AddDays(-3), EstadoDocumento.Completada, (p, 1, 0m));    // hace 3 días 10

        var d = await _w.DashboardService.GetDashboardAsync(TestWorld.Negocio1);

        Assert.Equal(7, d.TendenciaVentas.Count);
        Assert.Equal(DateTime.UtcNow.Date, d.TendenciaVentas[^1].Fecha);
        Assert.Equal(20m, d.TendenciaVentas[^1].Total);
        Assert.Equal(10m, d.TendenciaVentas[3].Total); // índice 3 = hoy - 3 días
        Assert.Equal(0m, d.TendenciaVentas[0].Total);
    }

    [Fact]
    public async Task ComprasRecientes_CuentaUltimos7DiasCompletadasDelNegocio()
    {
        var prov = _w.AddProveedor();
        var provAjeno = _w.AddProveedor(businessId: TestWorld.Negocio2);
        var p = _w.AddProducto();
        var ajeno = _w.AddProducto(businessId: TestWorld.Negocio2, sku: "X");
        var ahora = DateTime.UtcNow;

        _w.AddCompra(TestWorld.Negocio1, ahora.AddDays(-3), EstadoDocumento.Completada, prov, (p, 1, 1m));   // ✔
        _w.AddCompra(TestWorld.Negocio1, ahora.AddDays(-20), EstadoDocumento.Completada, prov, (p, 1, 1m));  // vieja ✘
        _w.AddCompra(TestWorld.Negocio1, ahora.AddDays(-1), EstadoDocumento.Anulada, prov, (p, 1, 1m));      // anulada ✘
        _w.AddCompra(TestWorld.Negocio2, ahora, EstadoDocumento.Completada, provAjeno, (ajeno, 1, 1m));      // otro negocio ✘

        var d = await _w.DashboardService.GetDashboardAsync(TestWorld.Negocio1);

        Assert.Equal(1, d.ComprasRecientesCount);
    }

    [Fact]
    public async Task NegocioSinDatos_DevuelveDashboardEnCeros()
    {
        var d = await _w.DashboardService.GetDashboardAsync(TestWorld.Negocio1);

        Assert.Equal(0m, d.VentasHoy);
        Assert.Equal(0m, d.VentasMes);
        Assert.Equal(0m, d.ValorInventario);
        Assert.Empty(d.Alertas);
        Assert.Empty(d.ProductosMasVendidos);
        Assert.Equal(7, d.TendenciaVentas.Count);
    }
}
