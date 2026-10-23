using SmartStockAI.Application.DTOs;
using SmartStockAI.Domain.Enums;
using SmartStockAI.Tests.Support;
using Xunit;

namespace SmartStockAI.Tests.Services;

public class ProductoServiceTests
{
    private readonly TestWorld _w = new();

    private static ProductoCreateDto Dto(
        string nombre = "Arroz 1lb", string sku = "ABR-001", int stock = 10, int min = 5,
        decimal compra = 1m, decimal venta = 2m) =>
        new(nombre, null, sku, 1, null, compra, venta, stock, min);

    // ---------- Crear producto ----------

    [Fact]
    public async Task Crear_Valido_PersisteConBusinessIdYRegistraMovimientoInicial()
    {
        var r = await _w.ProductoService.CreateAsync(TestWorld.Negocio1, "u1", "Ana", Dto());

        Assert.True(r.Success);
        var p = Assert.Single(_w.Productos.Items);
        Assert.Equal(TestWorld.Negocio1, p.BusinessId);
        Assert.Equal(10, p.StockActual);

        var m = Assert.Single(_w.Movimientos.Items);
        Assert.Equal(p.Id, m.ProductoId);
        Assert.Equal(TipoMovimientoInventario.Entrada, m.Tipo);
        Assert.Equal(10, m.Cantidad);
        Assert.Equal(10, m.StockResultante);
        Assert.Equal("u1", m.UsuarioId);
        Assert.Equal(TestWorld.Negocio1, m.BusinessId);
        Assert.Equal(1, _w.UnitOfWork.Transactions);
    }

    [Fact]
    public async Task Crear_StockInicialCero_NoRegistraMovimiento()
    {
        var r = await _w.ProductoService.CreateAsync(TestWorld.Negocio1, "u1", "Ana", Dto(stock: 0));

        Assert.True(r.Success);
        Assert.Empty(_w.Movimientos.Items);
    }

    [Fact]
    public async Task Crear_SkuDuplicadoEnMismoNegocio_FallaSinDistinguirMayusculas()
    {
        await _w.ProductoService.CreateAsync(TestWorld.Negocio1, "u1", "Ana", Dto(sku: "abr-001"));

        var r = await _w.ProductoService.CreateAsync(TestWorld.Negocio1, "u1", "Ana", Dto(nombre: "Otro", sku: "ABR-001"));

        Assert.False(r.Success);
        Assert.Contains("SKU", r.Error);
        Assert.Single(_w.Productos.Items);
    }

    [Fact]
    public async Task Crear_MismoSkuEnOtroNegocio_EsPermitido()
    {
        await _w.ProductoService.CreateAsync(TestWorld.Negocio1, "u1", "Ana", Dto(sku: "ABR-001"));

        var r = await _w.ProductoService.CreateAsync(TestWorld.Negocio2, "u2", "Luis", Dto(sku: "ABR-001"));

        Assert.True(r.Success);
        Assert.Equal(2, _w.Productos.Items.Count);
    }

    [Theory]
    [InlineData("", "SKU-1", 1, 2, 0, 0)]      // nombre vacío
    [InlineData("   ", "SKU-1", 1, 2, 0, 0)]   // nombre solo espacios
    [InlineData("Arroz", "", 1, 2, 0, 0)]      // SKU vacío
    [InlineData("Arroz", "SKU-1", -1, 2, 0, 0)] // precio de compra negativo
    [InlineData("Arroz", "SKU-1", 1, -2, 0, 0)] // precio de venta negativo
    [InlineData("Arroz", "SKU-1", 1, 2, -5, 0)] // stock inicial negativo
    [InlineData("Arroz", "SKU-1", 1, 2, 0, -1)] // stock mínimo negativo
    public async Task Crear_DatosInvalidos_Falla(string nombre, string sku, int compra, int venta, int stock, int min)
    {
        var r = await _w.ProductoService.CreateAsync(
            TestWorld.Negocio1, "u1", "Ana", Dto(nombre, sku, stock, min, compra, venta));

        Assert.False(r.Success);
        Assert.Empty(_w.Productos.Items);
        Assert.Empty(_w.Movimientos.Items);
    }

    // ---------- Editar / desactivar ----------

    [Fact]
    public async Task Editar_ActualizaCamposYFechaModificacion()
    {
        var p = _w.AddProducto(sku: "A-1");

        var r = await _w.ProductoService.UpdateAsync(TestWorld.Negocio1,
            new ProductoUpdateDto(p.Id, "  Nuevo nombre ", "desc", "A-2", 1, null, 3m, 4m, 7, true));

        Assert.True(r.Success);
        Assert.Equal("Nuevo nombre", p.Nombre);
        Assert.Equal("A-2", p.Sku);
        Assert.Equal(4m, p.PrecioVenta);
        Assert.Equal(7, p.StockMinimo);
        Assert.NotNull(p.FechaModificacion);
    }

    [Fact]
    public async Task Editar_ProductoDeOtroNegocio_NoLoEncuentra()
    {
        var ajeno = _w.AddProducto(businessId: TestWorld.Negocio2, nombre: "Ajeno", sku: "X-1");

        var r = await _w.ProductoService.UpdateAsync(TestWorld.Negocio1,
            new ProductoUpdateDto(ajeno.Id, "Hackeado", null, "X-1", 1, null, 1m, 1m, 1, true));

        Assert.False(r.Success);
        Assert.Equal("Ajeno", ajeno.Nombre);
    }

    [Fact]
    public async Task Editar_SkuQueYaUsaOtroProductoDelMismoNegocio_Falla()
    {
        _w.AddProducto(nombre: "Uno", sku: "A-1");
        var dos = _w.AddProducto(nombre: "Dos", sku: "A-2");

        var r = await _w.ProductoService.UpdateAsync(TestWorld.Negocio1,
            new ProductoUpdateDto(dos.Id, "Dos", null, "a-1", 1, null, 1m, 1m, 1, true));

        Assert.False(r.Success);
        Assert.Equal("A-2", dos.Sku);
    }

    [Fact]
    public async Task Desactivar_MarcaInactivoSinEliminar()
    {
        var p = _w.AddProducto();

        var r = await _w.ProductoService.DeactivateAsync(TestWorld.Negocio1, p.Id);

        Assert.True(r.Success);
        Assert.False(p.Activo);
        Assert.Single(_w.Productos.Items);
    }

    [Fact]
    public async Task Desactivar_ProductoDeOtroNegocio_Falla()
    {
        var ajeno = _w.AddProducto(businessId: TestWorld.Negocio2);

        var r = await _w.ProductoService.DeactivateAsync(TestWorld.Negocio1, ajeno.Id);

        Assert.False(r.Success);
        Assert.True(ajeno.Activo);
    }

    // ---------- Consultar / buscar ----------

    [Fact]
    public async Task Buscar_SoloDevuelveProductosDelNegocioActual()
    {
        _w.AddProducto(nombre: "Mio", sku: "M-1");
        _w.AddProducto(businessId: TestWorld.Negocio2, nombre: "Ajeno", sku: "A-1");

        var r = await _w.ProductoService.BuscarAsync(TestWorld.Negocio1, new ProductoBusquedaFiltro(null, null, null));

        var item = Assert.Single(r.Items);
        Assert.Equal("Mio", item.Nombre);
    }

    [Fact]
    public async Task Buscar_PorTextoEnNombreOSku_SinDistinguirMayusculas()
    {
        _w.AddProducto(nombre: "Arroz Premium", sku: "ABR-1");
        _w.AddProducto(nombre: "Frijol", sku: "ABR-2");
        _w.AddProducto(nombre: "Jabón", sku: "LIM-1");

        var porNombre = await _w.ProductoService.BuscarAsync(TestWorld.Negocio1, new ProductoBusquedaFiltro("arroz", null, null));
        var porSku = await _w.ProductoService.BuscarAsync(TestWorld.Negocio1, new ProductoBusquedaFiltro("ABR", null, null));

        Assert.Single(porNombre.Items);
        Assert.Equal(2, porSku.TotalCount);
    }

    [Fact]
    public async Task Buscar_PorCategoria()
    {
        var abarrotes = _w.AddCategoria(nombre: "Abarrotes");
        var limpieza = _w.AddCategoria(nombre: "Limpieza");
        _w.AddProducto(nombre: "Arroz", sku: "1", categoria: abarrotes);
        _w.AddProducto(nombre: "Cloro", sku: "2", categoria: limpieza);

        var r = await _w.ProductoService.BuscarAsync(TestWorld.Negocio1, new ProductoBusquedaFiltro(null, limpieza.Id, null));

        Assert.Equal("Cloro", Assert.Single(r.Items).Nombre);
    }

    [Fact]
    public async Task Buscar_SoloStockBajo_IncluyeProductosEnElMinimoOPorDebajo()
    {
        _w.AddProducto(nombre: "Sano", sku: "1", stock: 20, stockMinimo: 5);
        _w.AddProducto(nombre: "EnElMinimo", sku: "2", stock: 5, stockMinimo: 5);
        _w.AddProducto(nombre: "Agotado", sku: "3", stock: 0, stockMinimo: 5);

        var r = await _w.ProductoService.BuscarAsync(TestWorld.Negocio1, new ProductoBusquedaFiltro(null, null, true));

        Assert.Equal(new[] { "Agotado", "EnElMinimo" }, r.Items.Select(i => i.Nombre).ToArray());
        Assert.All(r.Items, i => Assert.True(i.TieneStockBajo));
    }

    [Fact]
    public async Task Buscar_Pagina_CalculaTotalesYPaginas()
    {
        for (var i = 1; i <= 25; i++)
            _w.AddProducto(nombre: $"P{i:00}", sku: $"S-{i}");

        var pagina3 = await _w.ProductoService.BuscarAsync(TestWorld.Negocio1, new ProductoBusquedaFiltro(null, null, null, 3, 10));

        Assert.Equal(25, pagina3.TotalCount);
        Assert.Equal(3, pagina3.TotalPages);
        Assert.Equal(5, pagina3.Items.Count);
        Assert.Equal("P21", pagina3.Items[0].Nombre);
    }

    [Fact]
    public async Task ObtenerPorId_ProductoDeOtroNegocio_DevuelveNull()
    {
        var ajeno = _w.AddProducto(businessId: TestWorld.Negocio2);

        Assert.Null(await _w.ProductoService.GetByIdAsync(TestWorld.Negocio1, ajeno.Id));
        Assert.NotNull(await _w.ProductoService.GetByIdAsync(TestWorld.Negocio2, ajeno.Id));
    }
}

public class ProductoServiceInactivosTests
{
    private readonly TestWorld _w = new();

    [Fact]
    public async Task Buscar_PorDefectoNoListaProductosDesactivados()
    {
        _w.AddProducto(nombre: "Activo", sku: "1");
        _w.AddProducto(nombre: "Eliminado", sku: "2", activo: false);

        var r = await _w.ProductoService.BuscarAsync(TestWorld.Negocio1, new ProductoBusquedaFiltro(null, null, null));

        Assert.Equal("Activo", Assert.Single(r.Items).Nombre);
        Assert.Equal(1, r.TotalCount);
    }

    [Fact]
    public async Task Buscar_ConIncluirInactivos_ListaTodos()
    {
        _w.AddProducto(nombre: "Activo", sku: "1");
        _w.AddProducto(nombre: "Eliminado", sku: "2", activo: false);

        var r = await _w.ProductoService.BuscarAsync(TestWorld.Negocio1, new ProductoBusquedaFiltro(null, null, null, 1, 10, IncluirInactivos: true));

        Assert.Equal(2, r.TotalCount);
        Assert.Contains(r.Items, p => !p.Activo);
    }

    [Fact]
    public async Task DesactivarUnProducto_LoSacaDelListadoPeroConservaSuHistorial()
    {
        var p = _w.AddProducto(nombre: "Para borrar");
        _w.AddVenta(TestWorld.Negocio1, DateTime.UtcNow, SmartStockAI.Domain.Enums.EstadoDocumento.Completada, (p, 1, 0m));

        await _w.ProductoService.DeactivateAsync(TestWorld.Negocio1, p.Id);
        var lista = await _w.ProductoService.BuscarAsync(TestWorld.Negocio1, new ProductoBusquedaFiltro(null, null, null));

        Assert.Empty(lista.Items);
        Assert.Single(_w.Ventas.Items);                       // la venta histórica sigue ahí
        Assert.Single(_w.Productos.Items);                    // y el producto no se borró físicamente
    }
}
