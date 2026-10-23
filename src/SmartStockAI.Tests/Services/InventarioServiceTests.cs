using SmartStockAI.Application.DTOs;
using SmartStockAI.Domain.Enums;
using SmartStockAI.Tests.Support;
using Xunit;

namespace SmartStockAI.Tests.Services;

public class InventarioServiceTests
{
    private readonly TestWorld _w = new();

    private Task<SmartStockAI.Application.Common.Result> Ajustar(int productoId, TipoMovimientoInventario tipo, int cantidad, int negocio = TestWorld.Negocio1) =>
        _w.InventarioService.RegistrarAjusteAsync(negocio, "u1", "Ana", new AjusteInventarioDto(productoId, tipo, cantidad, "motivo"));

    [Fact]
    public async Task Entrada_AumentaStockYRegistraMovimiento()
    {
        var p = _w.AddProducto(stock: 10);

        var r = await Ajustar(p.Id, TipoMovimientoInventario.Entrada, 5);

        Assert.True(r.Success);
        Assert.Equal(15, p.StockActual);
        var m = Assert.Single(_w.Movimientos.Items);
        Assert.Equal(TipoMovimientoInventario.Entrada, m.Tipo);
        Assert.Equal(MotivoMovimiento.AjusteManual, m.Motivo);
        Assert.Equal(5, m.Cantidad);
        Assert.Equal(15, m.StockResultante);
        Assert.Equal("u1", m.UsuarioId);
        Assert.Equal("Ana", m.UsuarioNombre);
        Assert.Equal("motivo", m.Observaciones);
    }

    [Fact]
    public async Task Salida_ReduceStockYRegistraMovimiento()
    {
        var p = _w.AddProducto(stock: 10);

        var r = await Ajustar(p.Id, TipoMovimientoInventario.Salida, 4);

        Assert.True(r.Success);
        Assert.Equal(6, p.StockActual);
        Assert.Equal(6, Assert.Single(_w.Movimientos.Items).StockResultante);
    }

    [Fact]
    public async Task Salida_QueDejaStockNegativo_FallaYNoModificaNada()
    {
        var p = _w.AddProducto(stock: 10);

        var r = await Ajustar(p.Id, TipoMovimientoInventario.Salida, 11);

        Assert.False(r.Success);
        Assert.Equal(10, p.StockActual);
        Assert.Empty(_w.Movimientos.Items);
    }

    [Fact]
    public async Task Salida_PorElStockExacto_DejaCero()
    {
        var p = _w.AddProducto(stock: 10);

        var r = await Ajustar(p.Id, TipoMovimientoInventario.Salida, 10);

        Assert.True(r.Success);
        Assert.Equal(0, p.StockActual);
    }

    [Fact]
    public async Task Ajuste_FijaElStockExactoYRegistraLaDiferencia()
    {
        var p = _w.AddProducto(stock: 10);

        var r = await Ajustar(p.Id, TipoMovimientoInventario.Ajuste, 3);

        Assert.True(r.Success);
        Assert.Equal(3, p.StockActual);
        var m = Assert.Single(_w.Movimientos.Items);
        Assert.Equal(TipoMovimientoInventario.Ajuste, m.Tipo);
        Assert.Equal(7, m.Cantidad);
        Assert.Equal(3, m.StockResultante);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task CantidadNoPositiva_Falla(int cantidad)
    {
        var p = _w.AddProducto(stock: 10);

        var r = await Ajustar(p.Id, TipoMovimientoInventario.Entrada, cantidad);

        Assert.False(r.Success);
        Assert.Equal(10, p.StockActual);
        Assert.Empty(_w.Movimientos.Items);
    }

    [Fact]
    public async Task ProductoInexistente_Falla()
    {
        var r = await Ajustar(999, TipoMovimientoInventario.Entrada, 1);

        Assert.False(r.Success);
    }

    [Fact]
    public async Task ProductoDeOtroNegocio_NoSePuedeAjustar()
    {
        var ajeno = _w.AddProducto(businessId: TestWorld.Negocio2, stock: 10);

        var r = await Ajustar(ajeno.Id, TipoMovimientoInventario.Entrada, 5, negocio: TestWorld.Negocio1);

        Assert.False(r.Success);
        Assert.Equal(10, ajeno.StockActual);
        Assert.Empty(_w.Movimientos.Items);
    }

    [Fact]
    public async Task Resumen_CuentaStockBajoYValorSoloDeProductosActivosDelNegocio()
    {
        _w.AddProducto(sku: "1", stock: 10, stockMinimo: 5, precioVenta: 2m);          // 20
        _w.AddProducto(sku: "2", stock: 3, stockMinimo: 5, precioVenta: 4m);           // 12, stock bajo
        _w.AddProducto(sku: "3", stock: 50, stockMinimo: 5, precioVenta: 9m, activo: false); // inactivo: no cuenta
        _w.AddProducto(businessId: TestWorld.Negocio2, sku: "4", stock: 99, precioVenta: 99m); // otro negocio

        var resumen = await _w.InventarioService.GetResumenAsync(TestWorld.Negocio1);

        Assert.Equal(2, resumen.TotalProductos);
        Assert.Equal(1, resumen.ProductosStockBajo);
        Assert.Equal(32m, resumen.ValorTotalInventario);
    }

    [Fact]
    public async Task Movimientos_FiltraPorProductoNegocioYOrdenaDelMasRecienteAlMasAntiguo()
    {
        var p1 = _w.AddProducto(sku: "1");
        var p2 = _w.AddProducto(sku: "2");
        _w.Movimientos.Add(new SmartStockAI.Domain.Entities.MovimientoInventario { BusinessId = 1, ProductoId = p1.Id, Fecha = DateTime.UtcNow.AddHours(-3), Cantidad = 1 });
        _w.Movimientos.Add(new SmartStockAI.Domain.Entities.MovimientoInventario { BusinessId = 1, ProductoId = p1.Id, Fecha = DateTime.UtcNow.AddHours(-1), Cantidad = 2 });
        _w.Movimientos.Add(new SmartStockAI.Domain.Entities.MovimientoInventario { BusinessId = 1, ProductoId = p2.Id, Fecha = DateTime.UtcNow, Cantidad = 3 });
        _w.Movimientos.Add(new SmartStockAI.Domain.Entities.MovimientoInventario { BusinessId = 2, ProductoId = p1.Id, Fecha = DateTime.UtcNow, Cantidad = 9 });

        var delProducto = await _w.InventarioService.GetMovimientosAsync(TestWorld.Negocio1, p1.Id);
        var limitado = await _w.InventarioService.GetMovimientosAsync(TestWorld.Negocio1, take: 2);

        Assert.Equal(new[] { 2, 1 }, delProducto.Select(m => m.Cantidad).ToArray());
        Assert.Equal(2, limitado.Count);
    }
}
