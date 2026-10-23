using SmartStockAI.Application.DTOs;
using SmartStockAI.Domain.Enums;
using SmartStockAI.Tests.Support;
using Xunit;

namespace SmartStockAI.Tests.Services;

public class CompraServiceTests
{
    private readonly TestWorld _w = new();

    private static CompraCreateDto Compra(int proveedorId, params (int ProductoId, int Cantidad, decimal Costo)[] lineas) =>
        new(proveedorId, lineas.Select(l => new CompraDetalleCreateDto(l.ProductoId, l.Cantidad, l.Costo)).ToList());

    private Task<SmartStockAI.Application.Common.Result<CompraDto>> Registrar(CompraCreateDto dto, int negocio = TestWorld.Negocio1) =>
        _w.CompraService.RegistrarCompraAsync(negocio, "u1", "Ana", dto);

    // ---------- Registrar compra + actualizar inventario ----------

    [Fact]
    public async Task Compra_AumentaInventarioYRegistraMovimientoDeEntrada()
    {
        var prov = _w.AddProveedor();
        var p = _w.AddProducto(stock: 10);

        var r = await Registrar(Compra(prov.Id, (p.Id, 20, 1.5m)));

        Assert.True(r.Success);
        Assert.Equal(30, p.StockActual);

        var compra = Assert.Single(_w.Compras.Items);
        Assert.Equal(TestWorld.Negocio1, compra.BusinessId);
        Assert.Equal(prov.Id, compra.ProveedorId);
        Assert.Equal(30m, compra.Total);
        Assert.Equal(EstadoDocumento.Completada, compra.Estado);
        Assert.StartsWith("C-", compra.NumeroDocumento);

        var m = Assert.Single(_w.Movimientos.Items);
        Assert.Equal(TipoMovimientoInventario.Entrada, m.Tipo);
        Assert.Equal(MotivoMovimiento.Compra, m.Motivo);
        Assert.Equal(20, m.Cantidad);
        Assert.Equal(30, m.StockResultante);
        Assert.Equal(compra.Id, m.CompraId);
        Assert.Equal(1, _w.UnitOfWork.Transactions);
    }

    [Fact]
    public async Task Compra_ActualizaElCostoDeCompraDelProducto()
    {
        var prov = _w.AddProveedor();
        var p = _w.AddProducto(precioCompra: 1m);

        await Registrar(Compra(prov.Id, (p.Id, 5, 2.25m)));

        Assert.Equal(2.25m, p.PrecioCompra);
    }

    [Fact]
    public async Task Compra_ConVariosProductos_SumaElTotal()
    {
        var prov = _w.AddProveedor();
        var a = _w.AddProducto(sku: "A", stock: 0);
        var b = _w.AddProducto(sku: "B", stock: 0);

        var r = await Registrar(Compra(prov.Id, (a.Id, 10, 2m), (b.Id, 4, 5m)));

        Assert.True(r.Success);
        Assert.Equal(40m, Assert.Single(_w.Compras.Items).Total);
        Assert.Equal(10, a.StockActual);
        Assert.Equal(4, b.StockActual);
        Assert.Equal(2, _w.Movimientos.Items.Count);
    }

    // ---------- Validaciones ----------

    [Fact]
    public async Task Compra_SinProductos_Falla()
    {
        var prov = _w.AddProveedor();

        var r = await Registrar(Compra(prov.Id));

        Assert.False(r.Success);
        Assert.Empty(_w.Compras.Items);
    }

    [Fact]
    public async Task Compra_ConProveedorInexistente_Falla()
    {
        var p = _w.AddProducto(stock: 10);

        var r = await Registrar(Compra(999, (p.Id, 5, 1m)));

        Assert.False(r.Success);
        Assert.Equal(10, p.StockActual);
    }

    [Theory]
    [InlineData(0, 1)]   // cantidad cero
    [InlineData(-2, 1)]  // cantidad negativa
    [InlineData(5, -1)]  // costo negativo
    public async Task Compra_ConCantidadOCostoInvalido_FallaSinTocarInventario(int cantidad, int costo)
    {
        var prov = _w.AddProveedor();
        var p = _w.AddProducto(stock: 10);

        var r = await Registrar(Compra(prov.Id, (p.Id, cantidad, costo)));

        Assert.False(r.Success);
        Assert.Equal(10, p.StockActual);
        Assert.Empty(_w.Compras.Items);
        Assert.Empty(_w.Movimientos.Items);
    }

    // ---------- Aislamiento entre negocios ----------

    [Fact]
    public async Task Compra_ConProveedorDeOtroNegocio_Falla()
    {
        var provAjeno = _w.AddProveedor(businessId: TestWorld.Negocio2);
        var p = _w.AddProducto(stock: 10);

        var r = await Registrar(Compra(provAjeno.Id, (p.Id, 5, 1m)));

        Assert.False(r.Success);
        Assert.Equal(10, p.StockActual);
    }

    [Fact]
    public async Task Compra_ConProductoDeOtroNegocio_FallaYNoTocaSuInventario()
    {
        var prov = _w.AddProveedor();
        var ajeno = _w.AddProducto(businessId: TestWorld.Negocio2, stock: 10);

        var r = await Registrar(Compra(prov.Id, (ajeno.Id, 5, 1m)));

        Assert.False(r.Success);
        Assert.Equal(10, ajeno.StockActual);
        Assert.Empty(_w.Compras.Items);
    }

    [Fact]
    public async Task Buscar_FiltraPorProveedorYNoMuestraComprasDeOtroNegocio()
    {
        var prov1 = _w.AddProveedor(nombre: "P1");
        var prov2 = _w.AddProveedor(nombre: "P2");
        var provAjeno = _w.AddProveedor(businessId: TestWorld.Negocio2, nombre: "PA");
        var p = _w.AddProducto();
        var ajeno = _w.AddProducto(businessId: TestWorld.Negocio2, sku: "X");
        _w.AddCompra(TestWorld.Negocio1, DateTime.UtcNow, EstadoDocumento.Completada, prov1, (p, 1, 1m));
        _w.AddCompra(TestWorld.Negocio1, DateTime.UtcNow, EstadoDocumento.Completada, prov2, (p, 1, 1m));
        _w.AddCompra(TestWorld.Negocio2, DateTime.UtcNow, EstadoDocumento.Completada, provAjeno, (ajeno, 1, 1m));

        var todas = await _w.CompraService.BuscarAsync(TestWorld.Negocio1, new CompraFiltro(null, null, null));
        var soloP1 = await _w.CompraService.BuscarAsync(TestWorld.Negocio1, new CompraFiltro(null, null, prov1.Id));

        Assert.Equal(2, todas.TotalCount);
        Assert.Equal(1, soloP1.TotalCount);
    }

    // ---------- Anulación ----------

    [Fact]
    public async Task Anular_RevierteElInventarioYRegistraMovimientoDeSalida()
    {
        var prov = _w.AddProveedor();
        var p = _w.AddProducto(stock: 10);
        var compra = (await Registrar(Compra(prov.Id, (p.Id, 5, 1m)))).Value!;
        Assert.Equal(15, p.StockActual);

        var r = await _w.CompraService.AnularCompraAsync(TestWorld.Negocio1, compra.Id);

        Assert.True(r.Success);
        Assert.Equal(10, p.StockActual);
        Assert.Equal(EstadoDocumento.Anulada, _w.Compras.Items.Single().Estado);
        var reverso = _w.Movimientos.Items.Last();
        Assert.Equal(TipoMovimientoInventario.Salida, reverso.Tipo);
        Assert.Equal(MotivoMovimiento.DevolucionProveedor, reverso.Motivo);
    }

    [Fact]
    public async Task Anular_SiYaSeVendioParteDeLoComprado_FallaYNoDejaStockNegativo()
    {
        var prov = _w.AddProveedor();
        var p = _w.AddProducto(stock: 0);
        var compra = (await Registrar(Compra(prov.Id, (p.Id, 5, 1m)))).Value!;
        p.StockActual = 2; // se vendieron 3 de las 5 unidades

        var r = await _w.CompraService.AnularCompraAsync(TestWorld.Negocio1, compra.Id);

        Assert.False(r.Success);
        Assert.Equal(2, p.StockActual);
        Assert.Equal(EstadoDocumento.Completada, _w.Compras.Items.Single().Estado);
    }

    [Fact]
    public async Task Anular_DosVeces_Falla()
    {
        var prov = _w.AddProveedor();
        var p = _w.AddProducto(stock: 10);
        var compra = (await Registrar(Compra(prov.Id, (p.Id, 5, 1m)))).Value!;
        await _w.CompraService.AnularCompraAsync(TestWorld.Negocio1, compra.Id);

        var r = await _w.CompraService.AnularCompraAsync(TestWorld.Negocio1, compra.Id);

        Assert.False(r.Success);
        Assert.Equal(10, p.StockActual);
    }

    [Fact]
    public async Task Anular_CompraDeOtroNegocio_Falla()
    {
        var prov = _w.AddProveedor();
        var p = _w.AddProducto(stock: 10);
        var compra = (await Registrar(Compra(prov.Id, (p.Id, 5, 1m)))).Value!;

        var r = await _w.CompraService.AnularCompraAsync(TestWorld.Negocio2, compra.Id);

        Assert.False(r.Success);
        Assert.Equal(15, p.StockActual);
    }
}
