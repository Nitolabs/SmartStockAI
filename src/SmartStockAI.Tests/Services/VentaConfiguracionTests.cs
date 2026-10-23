using SmartStockAI.Application.DTOs;
using SmartStockAI.Domain.Enums;
using SmartStockAI.Tests.Support;
using Xunit;

namespace SmartStockAI.Tests.Services;

/// <summary>Venta sin stock configurable (sección 6.8) y validación de stock por producto agregado.</summary>
public class VentaConfiguracionTests
{
    private readonly TestWorld _w = new();

    private static VentaCreateDto Venta(params (int ProductoId, int Cantidad)[] lineas) =>
        new(MetodoPago.Efectivo, 0m, lineas.Select(l => new VentaDetalleCreateDto(l.ProductoId, l.Cantidad, 0m)).ToList());

    [Fact]
    public async Task PorDefecto_NoSePermiteVenderSinStock()
    {
        _w.AddNegocio(TestWorld.Negocio1, permitirVentaSinStock: false);
        var p = _w.AddProducto(stock: 2);

        var r = await _w.VentaService.RegistrarVentaAsync(TestWorld.Negocio1, "u1", "Ana", Venta((p.Id, 5)));

        Assert.False(r.Success);
        Assert.Equal(2, p.StockActual);
    }

    [Fact]
    public async Task SinRegistroDeNegocio_SeAplicaLaReglaEstricta()
    {
        var p = _w.AddProducto(stock: 2);

        var r = await _w.VentaService.RegistrarVentaAsync(TestWorld.Negocio1, "u1", "Ana", Venta((p.Id, 5)));

        Assert.False(r.Success);
    }

    [Fact]
    public async Task ConLaConfiguracionActivada_SePermiteVender_YElStockQuedaNegativoConSuMovimiento()
    {
        _w.AddNegocio(TestWorld.Negocio1, permitirVentaSinStock: true);
        var p = _w.AddProducto(stock: 2);

        var r = await _w.VentaService.RegistrarVentaAsync(TestWorld.Negocio1, "u1", "Ana", Venta((p.Id, 5)));

        Assert.True(r.Success);
        Assert.Equal(-3, p.StockActual);
        var m = Assert.Single(_w.Movimientos.Items);
        Assert.Equal(-3, m.StockResultante); // queda trazado el faltante
        Assert.Equal(5, m.Cantidad);
        Assert.True(p.TieneStockBajo);       // y se dispara la alerta de reposición
    }

    [Fact]
    public async Task LaConfiguracionDeUnNegocio_NoAplicaAOtroNegocio()
    {
        _w.AddNegocio(TestWorld.Negocio1, permitirVentaSinStock: false);
        _w.AddNegocio(TestWorld.Negocio2, permitirVentaSinStock: true);
        var p = _w.AddProducto(businessId: TestWorld.Negocio1, stock: 1);

        var r = await _w.VentaService.RegistrarVentaAsync(TestWorld.Negocio1, "u1", "Ana", Venta((p.Id, 3)));

        Assert.False(r.Success);
    }

    [Fact]
    public async Task ConLaConfiguracionActivada_IgualSeRechazaProductoInactivoOCantidadInvalida()
    {
        _w.AddNegocio(TestWorld.Negocio1, permitirVentaSinStock: true);
        var inactivo = _w.AddProducto(sku: "I", stock: 10, activo: false);
        var activo = _w.AddProducto(sku: "A", stock: 10);

        Assert.False((await _w.VentaService.RegistrarVentaAsync(TestWorld.Negocio1, "u1", "Ana", Venta((inactivo.Id, 1)))).Success);
        Assert.False((await _w.VentaService.RegistrarVentaAsync(TestWorld.Negocio1, "u1", "Ana", Venta((activo.Id, 0)))).Success);
    }

    // ---------- Líneas repetidas del mismo producto ----------

    [Fact]
    public async Task DosLineasDelMismoProducto_SeValidanSumadas()
    {
        var p = _w.AddProducto(stock: 10);

        var r = await _w.VentaService.RegistrarVentaAsync(TestWorld.Negocio1, "u1", "Ana", Venta((p.Id, 6), (p.Id, 6)));

        Assert.False(r.Success);
        Assert.Contains("Stock insuficiente", r.Error);
        Assert.Equal(10, p.StockActual);
        Assert.Empty(_w.Ventas.Items);
        Assert.Empty(_w.Movimientos.Items);
    }

    [Fact]
    public async Task DosLineasDelMismoProducto_ConStockSuficiente_DescuentanAmbas()
    {
        var p = _w.AddProducto(stock: 10);

        var r = await _w.VentaService.RegistrarVentaAsync(TestWorld.Negocio1, "u1", "Ana", Venta((p.Id, 4), (p.Id, 3)));

        Assert.True(r.Success);
        Assert.Equal(3, p.StockActual);
        Assert.Equal(new[] { 6, 3 }, _w.Movimientos.Items.Select(m => m.StockResultante).ToArray());
    }
}
