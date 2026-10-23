using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Services;
using SmartStockAI.Tests.Support;
using Xunit;

namespace SmartStockAI.Tests.Services;

public class UsuarioServiceTests
{
    private const int Negocio1 = TestWorld.Negocio1;
    private const int Negocio2 = TestWorld.Negocio2;

    private readonly FakeUserDirectory _dir = new();
    private UsuarioService Servicio => new(_dir);

    private static UsuarioCreateDto Nuevo(string email = "ana@negocio.com", string rol = AppRoles.Vendedor, string pass = "Clave123!", string nombre = "Ana") =>
        new(nombre, email, pass, rol);

    // ---------- Crear ----------

    [Fact]
    public async Task Crear_Valido_DevuelveElIdYQuedaEnElNegocio()
    {
        var r = await Servicio.CrearAsync(Negocio1, Nuevo());

        Assert.True(r.Success);
        var lista = await Servicio.ListarAsync(Negocio1);
        var u = Assert.Single(lista);
        Assert.Equal("ana@negocio.com", u.Email);
        Assert.Equal(AppRoles.Vendedor, u.Rol);
        Assert.True(u.Activo);
        Assert.Empty(await Servicio.ListarAsync(Negocio2));
    }

    [Theory]
    [InlineData("", "ana@negocio.com", "Clave123!", AppRoles.Vendedor)]       // nombre vacío
    [InlineData("Ana", "no-es-correo", "Clave123!", AppRoles.Vendedor)]       // correo inválido
    [InlineData("Ana", "", "Clave123!", AppRoles.Vendedor)]                   // correo vacío
    [InlineData("Ana", "ana@negocio.com", "", AppRoles.Vendedor)]             // contraseña vacía
    [InlineData("Ana", "ana@negocio.com", "Clave123!", "SuperAdmin")]         // rol inventado
    [InlineData("Ana", "ana@negocio.com", "Clave123!", "")]                   // rol vacío
    public async Task Crear_DatosInvalidos_Falla(string nombre, string email, string pass, string rol)
    {
        var r = await Servicio.CrearAsync(Negocio1, new UsuarioCreateDto(nombre, email, pass, rol));

        Assert.False(r.Success);
        Assert.Empty(await Servicio.ListarAsync(Negocio1));
    }

    [Fact]
    public async Task Crear_CorreoRepetido_FallaAunqueSeaDeOtroNegocio()
    {
        _dir.Add(Negocio2, "Otro", "ana@negocio.com", AppRoles.Administrador);

        var r = await Servicio.CrearAsync(Negocio1, Nuevo(email: "ANA@negocio.com"));

        Assert.False(r.Success);
        Assert.Contains("correo", r.Error);
    }

    [Fact]
    public async Task Crear_ConContrasenaQueIdentityRechaza_PropagaElError()
    {
        _dir.RechazarContrasenaCon = "debil";

        var r = await Servicio.CrearAsync(Negocio1, Nuevo(pass: "debil"));

        Assert.False(r.Success);
        Assert.Contains("contraseña", r.Error);
    }

    // ---------- Actualizar ----------

    [Fact]
    public async Task Actualizar_CambiaNombreRolYEstadoDeOtroUsuario()
    {
        var admin = _dir.Add(Negocio1, "Admin", "admin@n.com", AppRoles.Administrador);
        var ana = _dir.Add(Negocio1, "Ana", "ana@n.com", AppRoles.Vendedor);

        var r = await Servicio.ActualizarAsync(Negocio1, admin, new UsuarioUpdateDto(ana, "Ana Pérez", AppRoles.Inventario, false));

        Assert.True(r.Success);
        var u = (await Servicio.ListarAsync(Negocio1)).Single(x => x.Id == ana);
        Assert.Equal("Ana Pérez", u.NombreCompleto);
        Assert.Equal(AppRoles.Inventario, u.Rol);
        Assert.False(u.Activo);
    }

    [Fact]
    public async Task Actualizar_UsuarioDeOtroNegocio_NoSeEncuentra()
    {
        var admin = _dir.Add(Negocio1, "Admin", "admin@n.com", AppRoles.Administrador);
        var ajeno = _dir.Add(Negocio2, "Ajeno", "ajeno@n.com", AppRoles.Vendedor);

        var r = await Servicio.ActualizarAsync(Negocio1, admin, new UsuarioUpdateDto(ajeno, "Hackeado", AppRoles.Administrador, true));

        Assert.False(r.Success);
        Assert.Equal("Ajeno", (await Servicio.ListarAsync(Negocio2)).Single().NombreCompleto);
    }

    [Fact]
    public async Task Actualizar_NoPermiteCambiarElPropioRol()
    {
        var admin = _dir.Add(Negocio1, "Admin", "admin@n.com", AppRoles.Administrador);
        _dir.Add(Negocio1, "Otro admin", "otro@n.com", AppRoles.Administrador);

        var r = await Servicio.ActualizarAsync(Negocio1, admin, new UsuarioUpdateDto(admin, "Admin", AppRoles.Vendedor, true));

        Assert.False(r.Success);
        Assert.Equal(AppRoles.Administrador, (await Servicio.ListarAsync(Negocio1)).Single(u => u.Id == admin).Rol);
    }

    [Fact]
    public async Task Actualizar_NoPermiteDesactivarseASiMismo()
    {
        var admin = _dir.Add(Negocio1, "Admin", "admin@n.com", AppRoles.Administrador);
        _dir.Add(Negocio1, "Otro admin", "otro@n.com", AppRoles.Administrador);

        var r = await Servicio.ActualizarAsync(Negocio1, admin, new UsuarioUpdateDto(admin, "Admin", AppRoles.Administrador, false));

        Assert.False(r.Success);
        Assert.True((await Servicio.ListarAsync(Negocio1)).Single(u => u.Id == admin).Activo);
    }

    [Fact]
    public async Task Actualizar_PuedeEditarSuPropioNombreSinCambiarRolNiEstado()
    {
        var admin = _dir.Add(Negocio1, "Admin", "admin@n.com", AppRoles.Administrador);

        var r = await Servicio.ActualizarAsync(Negocio1, admin, new UsuarioUpdateDto(admin, "Nombre nuevo", AppRoles.Administrador, true));

        Assert.True(r.Success);
    }

    [Fact]
    public async Task Actualizar_NoPermiteQuitarAlUltimoAdministradorActivo()
    {
        var unicoAdmin = _dir.Add(Negocio1, "Admin", "a1@n.com", AppRoles.Administrador);
        _dir.Add(Negocio1, "Admin inactivo", "a2@n.com", AppRoles.Administrador, activo: false); // inactivo: no cuenta
        var actor = _dir.Add(Negocio1, "Otro usuario", "v@n.com", AppRoles.Vendedor);

        var degradar = await Servicio.ActualizarAsync(Negocio1, actor, new UsuarioUpdateDto(unicoAdmin, "Admin", AppRoles.Vendedor, true));
        var desactivar = await Servicio.ActualizarAsync(Negocio1, actor, new UsuarioUpdateDto(unicoAdmin, "Admin", AppRoles.Administrador, false));

        Assert.False(degradar.Success);
        Assert.False(desactivar.Success);
        Assert.Contains("al menos un administrador", degradar.Error);
        var admin = (await Servicio.ListarAsync(Negocio1)).Single(u => u.Id == unicoAdmin);
        Assert.Equal(AppRoles.Administrador, admin.Rol);
        Assert.True(admin.Activo);
    }

    [Fact]
    public async Task Actualizar_SiHayOtroAdministradorActivo_SePuedeDegradarAUno()
    {
        var admin1 = _dir.Add(Negocio1, "Admin 1", "a1@n.com", AppRoles.Administrador);
        var admin2 = _dir.Add(Negocio1, "Admin 2", "a2@n.com", AppRoles.Administrador);

        var r = await Servicio.ActualizarAsync(Negocio1, admin1, new UsuarioUpdateDto(admin2, "Admin 2", AppRoles.Vendedor, true));

        Assert.True(r.Success);
    }

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("")]
    public async Task Actualizar_ConRolInvalido_Falla(string rol)
    {
        var admin = _dir.Add(Negocio1, "Admin", "admin@n.com", AppRoles.Administrador);
        var ana = _dir.Add(Negocio1, "Ana", "ana@n.com", AppRoles.Vendedor);

        var r = await Servicio.ActualizarAsync(Negocio1, admin, new UsuarioUpdateDto(ana, "Ana", rol, true));

        Assert.False(r.Success);
    }

    // ---------- Restablecer contraseña ----------

    [Fact]
    public async Task RestablecerContrasena_DeUsuarioDelNegocio_Funciona()
    {
        var ana = _dir.Add(Negocio1, "Ana", "ana@n.com", AppRoles.Vendedor);

        var r = await Servicio.RestablecerContrasenaAsync(Negocio1, ana, "NuevaClave1!");

        Assert.True(r.Success);
        Assert.Equal("NuevaClave1!", _dir.ContrasenaDe(ana));
    }

    [Fact]
    public async Task RestablecerContrasena_DeUsuarioDeOtroNegocio_Falla()
    {
        var ajeno = _dir.Add(Negocio2, "Ajeno", "ajeno@n.com", AppRoles.Vendedor);

        var r = await Servicio.RestablecerContrasenaAsync(Negocio1, ajeno, "NuevaClave1!");

        Assert.False(r.Success);
        Assert.Null(_dir.ContrasenaDe(ajeno));
    }

    [Fact]
    public async Task RestablecerContrasena_Vacia_Falla()
    {
        var ana = _dir.Add(Negocio1, "Ana", "ana@n.com", AppRoles.Vendedor);

        var r = await Servicio.RestablecerContrasenaAsync(Negocio1, ana, "  ");

        Assert.False(r.Success);
    }
}

public class AppRolesTests
{
    [Theory]
    [InlineData("Administrador", true)]
    [InlineData("Vendedor", true)]
    [InlineData("Inventario", true)]
    [InlineData("administrador", false)] // distingue mayúsculas: coincide con los roles guardados en Identity
    [InlineData("Otro", false)]
    [InlineData("", false)]
    public void EsValido_SoloAceptaLosTresRoles(string rol, bool esperado) =>
        Assert.Equal(esperado, AppRoles.EsValido(rol));

    [Fact]
    public void LasCombinacionesParaAuthorizeUsanLosNombresExactos()
    {
        Assert.Equal("Administrador,Inventario", AppRoles.AdministradorOInventario);
        Assert.Equal("Administrador,Vendedor", AppRoles.AdministradorOVendedor);
    }
}

public class NegocioServiceTests
{
    private readonly TestWorld _w = new();

    [Fact]
    public async Task Obtener_DevuelveLaConfiguracionDelNegocio()
    {
        _w.AddNegocio(TestWorld.Negocio1, "Mi tienda", permitirVentaSinStock: true);

        var n = await _w.NegocioService.ObtenerAsync(TestWorld.Negocio1);

        Assert.Equal("Mi tienda", n!.Nombre);
        Assert.True(n.PermitirVentaSinStock);
    }

    [Fact]
    public async Task Obtener_NegocioInexistente_DevuelveNull() =>
        Assert.Null(await _w.NegocioService.ObtenerAsync(99));

    [Fact]
    public async Task Actualizar_GuardaLosCamposYRecortaEspacios()
    {
        var b = _w.AddNegocio(TestWorld.Negocio1, "Viejo");

        var r = await _w.NegocioService.ActualizarAsync(TestWorld.Negocio1,
            new NegocioUpdateDto("  Nuevo ", " Ferretería ", "Calle 1", "2222-0000", "a@b.com", true));

        Assert.True(r.Success);
        Assert.Equal("Nuevo", b.Nombre);
        Assert.Equal("Ferretería", b.Rubro);
        Assert.True(b.PermitirVentaSinStock);
        Assert.NotNull(b.FechaModificacion);
    }

    [Fact]
    public async Task Actualizar_NombreVacio_Falla()
    {
        var b = _w.AddNegocio(TestWorld.Negocio1, "Viejo");

        var r = await _w.NegocioService.ActualizarAsync(TestWorld.Negocio1, new NegocioUpdateDto(" ", null, null, null, null, false));

        Assert.False(r.Success);
        Assert.Equal("Viejo", b.Nombre);
    }

    [Fact]
    public async Task Actualizar_SoloAfectaAlNegocioIndicado()
    {
        var a = _w.AddNegocio(TestWorld.Negocio1, "A");
        var b = _w.AddNegocio(TestWorld.Negocio2, "B");

        await _w.NegocioService.ActualizarAsync(TestWorld.Negocio1, new NegocioUpdateDto("A2", null, null, null, null, true));

        Assert.Equal("A2", a.Nombre);
        Assert.Equal("B", b.Nombre);
        Assert.False(b.PermitirVentaSinStock);
    }
}
