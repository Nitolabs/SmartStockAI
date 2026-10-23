using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;

namespace SmartStockAI.Tests.Support;

/// <summary>Directorio de usuarios en memoria para probar las reglas de UsuarioService sin Identity.</summary>
public class FakeUserDirectory : IUserDirectory
{
    private sealed class Registro
    {
        public required string Id { get; init; }
        public int BusinessId { get; init; }
        public required string Nombre { get; set; }
        public required string Email { get; init; }
        public required string Rol { get; set; }
        public bool Activo { get; set; }
        public string? Contrasena { get; set; }
    }

    private readonly List<Registro> _usuarios = new();
    private int _next = 1;

    public string? RechazarContrasenaCon { get; set; }

    public string Add(int businessId, string nombre, string email, string rol, bool activo = true)
    {
        var id = $"u{_next++}";
        _usuarios.Add(new Registro { Id = id, BusinessId = businessId, Nombre = nombre, Email = email, Rol = rol, Activo = activo });
        return id;
    }

    public string? ContrasenaDe(string id) => _usuarios.Single(u => u.Id == id).Contrasena;

    public Task<IReadOnlyList<UsuarioDto>> ListByBusinessAsync(int businessId) =>
        Task.FromResult<IReadOnlyList<UsuarioDto>>(_usuarios.Where(u => u.BusinessId == businessId).Select(ToDto).ToList());

    public Task<UsuarioDto?> FindAsync(int businessId, string userId) =>
        Task.FromResult(_usuarios.Where(u => u.BusinessId == businessId && u.Id == userId).Select(ToDto).FirstOrDefault());

    public Task<bool> EmailExistsAsync(string email) =>
        Task.FromResult(_usuarios.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<int> CountActiveAdminsAsync(int businessId) =>
        Task.FromResult(_usuarios.Count(u => u.BusinessId == businessId && u.Activo && u.Rol == AppRoles.Administrador));

    public Task<Result<string>> CreateAsync(int businessId, string nombreCompleto, string email, string contrasena, string rol)
    {
        if (RechazarContrasenaCon is not null && contrasena == RechazarContrasenaCon)
            return Task.FromResult(Result<string>.Fail("La contraseña no cumple los requisitos."));

        var id = Add(businessId, nombreCompleto, email, rol);
        _usuarios.Single(u => u.Id == id).Contrasena = contrasena;
        return Task.FromResult(Result<string>.Ok(id));
    }

    public Task<Result> UpdateAsync(int businessId, string userId, string nombreCompleto, string rol, bool activo)
    {
        var u = _usuarios.Single(x => x.BusinessId == businessId && x.Id == userId);
        u.Nombre = nombreCompleto;
        u.Rol = rol;
        u.Activo = activo;
        return Task.FromResult(Result.Ok());
    }

    public Task<Result> SetPasswordAsync(int businessId, string userId, string nuevaContrasena)
    {
        _usuarios.Single(x => x.BusinessId == businessId && x.Id == userId).Contrasena = nuevaContrasena;
        return Task.FromResult(Result.Ok());
    }

    private static UsuarioDto ToDto(Registro u) => new(u.Id, u.Nombre, u.Email, u.Rol, u.Activo);
}
