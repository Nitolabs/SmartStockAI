using SmartStockAI.Application.DTOs;
using SmartStockAI.Tests.Support;
using Xunit;

namespace SmartStockAI.Tests.Services;

public class CategoriaServiceTests
{
    private readonly TestWorld _w = new();

    [Fact]
    public async Task Crear_Valida_AsignaNegocio()
    {
        var r = await _w.CategoriaService.CreateAsync(TestWorld.Negocio1, new CategoriaCreateDto("  Bebidas ", null));

        Assert.True(r.Success);
        var c = Assert.Single(_w.Categorias.Items);
        Assert.Equal("Bebidas", c.Nombre);
        Assert.Equal(TestWorld.Negocio1, c.BusinessId);
    }

    [Fact]
    public async Task Crear_NombreVacio_Falla()
    {
        var r = await _w.CategoriaService.CreateAsync(TestWorld.Negocio1, new CategoriaCreateDto("  ", null));

        Assert.False(r.Success);
        Assert.Empty(_w.Categorias.Items);
    }

    [Fact]
    public async Task Crear_NombreDuplicadoEnElNegocio_FallaPeroEnOtroNegocioSePermite()
    {
        await _w.CategoriaService.CreateAsync(TestWorld.Negocio1, new CategoriaCreateDto("Bebidas", null));

        var duplicada = await _w.CategoriaService.CreateAsync(TestWorld.Negocio1, new CategoriaCreateDto("bebidas", null));
        var otroNegocio = await _w.CategoriaService.CreateAsync(TestWorld.Negocio2, new CategoriaCreateDto("Bebidas", null));

        Assert.False(duplicada.Success);
        Assert.True(otroNegocio.Success);
    }

    [Fact]
    public async Task Listar_SoloDelNegocioYOrdenadasPorNombre()
    {
        _w.AddCategoria(nombre: "Zeta");
        _w.AddCategoria(nombre: "Alfa");
        _w.AddCategoria(businessId: TestWorld.Negocio2, nombre: "Ajena");

        var lista = await _w.CategoriaService.GetAllAsync(TestWorld.Negocio1);

        Assert.Equal(new[] { "Alfa", "Zeta" }, lista.Select(c => c.Nombre).ToArray());
    }

    [Fact]
    public async Task Editar_ActualizaYDesactivar_MarcaInactiva()
    {
        var c = _w.AddCategoria(nombre: "Vieja");

        var editar = await _w.CategoriaService.UpdateAsync(TestWorld.Negocio1, new CategoriaUpdateDto(c.Id, "Nueva", "d", true));
        var desactivar = await _w.CategoriaService.DeactivateAsync(TestWorld.Negocio1, c.Id);

        Assert.True(editar.Success);
        Assert.True(desactivar.Success);
        Assert.Equal("Nueva", c.Nombre);
        Assert.False(c.Activo);
    }

    [Fact]
    public async Task EditarYDesactivar_CategoriaDeOtroNegocio_Falla()
    {
        var ajena = _w.AddCategoria(businessId: TestWorld.Negocio2, nombre: "Ajena");

        var editar = await _w.CategoriaService.UpdateAsync(TestWorld.Negocio1, new CategoriaUpdateDto(ajena.Id, "X", null, true));
        var desactivar = await _w.CategoriaService.DeactivateAsync(TestWorld.Negocio1, ajena.Id);

        Assert.False(editar.Success);
        Assert.False(desactivar.Success);
        Assert.Equal("Ajena", ajena.Nombre);
        Assert.True(ajena.Activo);
    }
}

public class ProveedorServiceTests
{
    private readonly TestWorld _w = new();

    [Fact]
    public async Task Crear_Valido_AsignaNegocio()
    {
        var r = await _w.ProveedorService.CreateAsync(TestWorld.Negocio1,
            new ProveedorCreateDto(" La Constancia ", "Carlos", "7777-1111", "v@lc.com", "San Salvador"));

        Assert.True(r.Success);
        var p = Assert.Single(_w.Proveedores.Items);
        Assert.Equal("La Constancia", p.Nombre);
        Assert.Equal(TestWorld.Negocio1, p.BusinessId);
    }

    [Fact]
    public async Task Crear_NombreVacio_Falla()
    {
        var r = await _w.ProveedorService.CreateAsync(TestWorld.Negocio1, new ProveedorCreateDto("", null, null, null, null));

        Assert.False(r.Success);
        Assert.Empty(_w.Proveedores.Items);
    }

    [Fact]
    public async Task Listar_NoMuestraProveedoresDeOtroNegocio()
    {
        _w.AddProveedor(nombre: "Mio");
        _w.AddProveedor(businessId: TestWorld.Negocio2, nombre: "Ajeno");

        var lista = await _w.ProveedorService.GetAllAsync(TestWorld.Negocio1);

        Assert.Equal("Mio", Assert.Single(lista).Nombre);
    }

    [Fact]
    public async Task EditarYDesactivar_ProveedorDeOtroNegocio_Falla()
    {
        var ajeno = _w.AddProveedor(businessId: TestWorld.Negocio2, nombre: "Ajeno");

        var editar = await _w.ProveedorService.UpdateAsync(TestWorld.Negocio1,
            new ProveedorUpdateDto(ajeno.Id, "X", null, null, null, null, true));
        var desactivar = await _w.ProveedorService.DeactivateAsync(TestWorld.Negocio1, ajeno.Id);

        Assert.False(editar.Success);
        Assert.False(desactivar.Success);
        Assert.True(ajeno.Activo);
    }

    [Fact]
    public async Task Desactivar_MarcaInactivo()
    {
        var p = _w.AddProveedor();

        var r = await _w.ProveedorService.DeactivateAsync(TestWorld.Negocio1, p.Id);

        Assert.True(r.Success);
        Assert.False(p.Activo);
    }
}
