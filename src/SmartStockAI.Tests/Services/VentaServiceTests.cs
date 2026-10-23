using SmartStockAI.Application.DTOs;
using SmartStockAI.Domain.Enums;
using SmartStockAI.Tests.Support;
using Xunit;

namespace SmartStockAI.Tests.Services;

public class VentaServiceTests
{
    private readonly TestWorld _w = new();

    private static VentaCreateDto Venta(params (int ProductoId, int Cantidad, decimal Descuento)[] lineas) =>
        new(MetodoPago.Efectivo, 0m,
            lineas.Select(l => new VentaDetalleCreateDto(l.ProductoId, l.Cantidad, l.Descuento)).ToList());

    private Task<SmartStockAI.Application.Common.Result<VentaDto>> Registrar(VentaCreateDto dto, int negocio = TestWorld.Negocio1) =>
        _w.VentaService.RegistrarVentaAsync(negocio, "u1", "Ana", dto);

    // ---------- Registrar venta + reducir inventario ----------

    [Fact]
    public async Task Venta_ConStockSuficiente_ReduceInventarioYRegistraMovimientoDeSalida()
    {
        var p = _w.AddProducto(stock: 10, precioVenta: 10m);

        var r = await Registrar(Venta((p.Id, 3, 0m)));

        Assert.True(r.Success);
        Assert.Equal(7, p.StockActual);

        var venta = Assert.Single(_w.Ventas.Items);
        Assert.Equal(TestWorld.Negocio1, venta.BusinessId);
        Assert.Equal(EstadoDocumento.Completada, venta.Estado);
        Assert.StartsWith("V-", venta.NumeroDocumento);
        Assert.Equal("u1", venta.UsuarioId);

        var m = Assert.Single(_w.Movimientos.Items);
        Assert.Equal(TipoMovimientoInventario.Salida, m.Tipo);
        Assert.Equal(MotivoMovimiento.Venta, m.Motivo);
        Assert.Equal(3, m.Cantidad);
        Assert.Equal(7, m.StockResultante);
        Assert.Equal(venta.Id, m.VentaId);
        Assert.Equal(1, _w.UnitOfWork.Transactions);
    }

    [Fact]
    public async Task Venta_TomaElPrecioDelProductoNoDelCliente()
    {
        var p = _w.AddProducto(precioVenta: 12.5m);

        await Registrar(Venta((p.Id, 2, 0m)));

        var detalle = Assert.Single(Assert.Single(_w.Ventas.Items).Detalles);
        Assert.Equal(12.5m, detalle.PrecioUnitario);
    }

    [Fact]
    public async Task Venta_CalculaSubtotalBrutoDescuentoYTotal_SinRestarElDescuentoDosVeces()
    {
        var p = _w.AddProducto(stock: 10, precioVenta: 10m);

        var r = await Registrar(Venta((p.Id, 2, 3m)));

        Assert.True(r.Success);
        var venta = Assert.Single(_w.Ventas.Items);
        Assert.Equal(20m, venta.Subtotal);
        Assert.Equal(3m, venta.Descuento);
        Assert.Equal(17m, venta.Total);
        Assert.Equal(17m, r.Value!.Total);
    }

    [Fact]
    public async Task Venta_ConDescuentoMayorAlSubtotal_NoGeneraTotalNegativo()
    {
        var p = _w.AddProducto(stock: 10, precioVenta: 10m);

        await Registrar(Venta((p.Id, 1, 50m)));

        Assert.Equal(0m, Assert.Single(_w.Ventas.Items).Total);
    }

    [Fact]
    public async Task Venta_ConVariasLineas_SumaTotalesYReduceCadaProducto()
    {
        var a = _w.AddProducto(sku: "A", stock: 10, precioVenta: 10m);
        var b = _w.AddProducto(sku: "B", stock: 5, precioVenta: 4m);

        var r = await Registrar(Venta((a.Id, 2, 0m), (b.Id, 5, 0m)));

        Assert.True(r.Success);
        Assert.Equal(8, a.StockActual);
        Assert.Equal(0, b.StockActual);
        Assert.Equal(40m, Assert.Single(_w.Ventas.Items).Total);
        Assert.Equal(2, _w.Movimientos.Items.Count);
    }

    // ---------- Reglas: stock insuficiente y validaciones ----------

    [Fact]
    public async Task Venta_ConStockInsuficiente_SeRechazaYNoModificaNada()
    {
        var p = _w.AddProducto(stock: 2);

        var r = await Registrar(Venta((p.Id, 3, 0m)));

        Assert.False(r.Success);
        Assert.Contains("Stock insuficiente", r.Error);
        Assert.Equal(2, p.StockActual);
        Assert.Empty(_w.Ventas.Items);
        Assert.Empty(_w.Movimientos.Items);
        Assert.Equal(0, _w.UnitOfWork.Transactions);
    }

    [Fact]
    public async Task Venta_CantidadIgualAlStock_EsPermitidaYDejaCero()
    {
        var p = _w.AddProducto(stock: 4);

        var r = await Registrar(Venta((p.Id, 4, 0m)));

        Assert.True(r.Success);
        Assert.Equal(0, p.StockActual);
    }

    [Fact]
    public async Task Venta_SiUnaLineaNoTieneStock_NingunProductoDelCarritoSeModifica()
    {
        var conStock = _w.AddProducto(sku: "A", stock: 10);
        var sinStock = _w.AddProducto(sku: "B", stock: 1);

        var r = await Registrar(Venta((conStock.Id, 2, 0m), (sinStock.Id, 5, 0m)));

        Assert.False(r.Success);
        Assert.Equal(10, conStock.StockActual);
        Assert.Equal(1, sinStock.StockActual);
        Assert.Empty(_w.Ventas.Items);
        Assert.Empty(_w.Movimientos.Items);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Venta_ConCantidadNoPositiva_Falla(int cantidad)
    {
        var p = _w.AddProducto(stock: 10);

        var r = await Registrar(Venta((p.Id, cantidad, 0m)));

        Assert.False(r.Success);
        Assert.Equal(10, p.StockActual);
        Assert.Empty(_w.Ventas.Items);
    }

    [Fact]
    public async Task Venta_SinProductos_Falla()
    {
        var r = await Registrar(Venta());

        Assert.False(r.Success);
        Assert.Empty(_w.Ventas.Items);
    }

    [Fact]
    public async Task Venta_ProductoInactivo_Falla()
    {
        var p = _w.AddProducto(stock: 10, activo: false);

        var r = await Registrar(Venta((p.Id, 1, 0m)));

        Assert.False(r.Success);
        Assert.Equal(10, p.StockActual);
    }

    // ---------- Aislamiento entre negocios ----------

    [Fact]
    public async Task Venta_ConProductoDeOtroNegocio_SeRechazaYNoTocaSuInventario()
    {
        var ajeno = _w.AddProducto(businessId: TestWorld.Negocio2, stock: 10);

        var r = await Registrar(Venta((ajeno.Id, 1, 0m)), negocio: TestWorld.Negocio1);

        Assert.False(r.Success);
        Assert.Equal(10, ajeno.StockActual);
        Assert.Empty(_w.Ventas.Items);
    }

    [Fact]
    public async Task Buscar_NoMuestraVentasDeOtroNegocio()
    {
        var mio = _w.AddProducto(sku: "M");
        var ajeno = _w.AddProducto(businessId: TestWorld.Negocio2, sku: "A");
        _w.AddVenta(TestWorld.Negocio1, DateTime.UtcNow, EstadoDocumento.Completada, (mio, 1, 0m));
        var ventaAjena = _w.AddVenta(TestWorld.Negocio2, DateTime.UtcNow, EstadoDocumento.Completada, (ajeno, 1, 0m));

        var r = await _w.VentaService.BuscarAsync(TestWorld.Negocio1, new VentaFiltro(null, null, null));

        Assert.Equal(1, r.TotalCount);
        Assert.Null(await _w.VentaService.GetByIdAsync(TestWorld.Negocio1, ventaAjena.Id));
    }

    [Fact]
    public async Task Buscar_FiltraPorRangoDeFechasIncluyendoElDiaFinalCompleto()
    {
        var p = _w.AddProducto();
        var dia = new DateTime(2026, 10, 5, 15, 30, 0, DateTimeKind.Utc);
        _w.AddVenta(TestWorld.Negocio1, dia, EstadoDocumento.Completada, (p, 1, 0m));
        _w.AddVenta(TestWorld.Negocio1, dia.AddDays(-10), EstadoDocumento.Completada, (p, 1, 0m));

        var r = await _w.VentaService.BuscarAsync(TestWorld.Negocio1, new VentaFiltro(dia.Date, dia.Date, null));

        Assert.Equal(1, r.TotalCount);
    }

    // ---------- Anulación ----------

    [Fact]
    public async Task Anular_RestauraInventarioYRegistraMovimientoDeDevolucion()
    {
        var p = _w.AddProducto(stock: 10);
        var venta = (await Registrar(Venta((p.Id, 4, 0m)))).Value!;
        Assert.Equal(6, p.StockActual);

        var r = await _w.VentaService.AnularVentaAsync(TestWorld.Negocio1, venta.Id);

        Assert.True(r.Success);
        Assert.Equal(10, p.StockActual);
        Assert.Equal(EstadoDocumento.Anulada, _w.Ventas.Items.Single().Estado);

        var devolucion = _w.Movimientos.Items.Last();
        Assert.Equal(TipoMovimientoInventario.Entrada, devolucion.Tipo);
        Assert.Equal(MotivoMovimiento.DevolucionCliente, devolucion.Motivo);
        Assert.Equal(4, devolucion.Cantidad);
        Assert.Equal(10, devolucion.StockResultante);
        Assert.Equal(2, _w.Movimientos.Items.Count); // el movimiento original NO se borra: queda bitácora
    }

    [Fact]
    public async Task Anular_UnaVentaYaAnulada_Falla_YNoDuplicaElStock()
    {
        var p = _w.AddProducto(stock: 10);
        var venta = (await Registrar(Venta((p.Id, 4, 0m)))).Value!;
        await _w.VentaService.AnularVentaAsync(TestWorld.Negocio1, venta.Id);

        var r = await _w.VentaService.AnularVentaAsync(TestWorld.Negocio1, venta.Id);

        Assert.False(r.Success);
        Assert.Equal(10, p.StockActual);
    }

    [Fact]
    public async Task Anular_VentaDeOtroNegocio_Falla()
    {
        var p = _w.AddProducto(stock: 10);
        var venta = (await Registrar(Venta((p.Id, 4, 0m)))).Value!;

        var r = await _w.VentaService.AnularVentaAsync(TestWorld.Negocio2, venta.Id);

        Assert.False(r.Success);
        Assert.Equal(6, p.StockActual);
        Assert.Equal(EstadoDocumento.Completada, _w.Ventas.Items.Single().Estado);
    }
}
