using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;

namespace SmartStockAI.Application.Interfaces;

/// <summary>
/// Acceso a los usuarios de un negocio. La implementación (Infrastructure) usa ASP.NET Core Identity;
/// las reglas de negocio viven en UsuarioService, que solo conoce esta interfaz.
/// </summary>
public interface IUserDirectory
{
    Task<IReadOnlyList<UsuarioDto>> ListByBusinessAsync(int businessId);
    Task<UsuarioDto?> FindAsync(int businessId, string userId);
    Task<bool> EmailExistsAsync(string email);
    Task<int> CountActiveAdminsAsync(int businessId);

    /// <summary>Devuelve el id del usuario creado. Los errores de Identity (p. ej. contraseña débil) vienen en Error.</summary>
    Task<Result<string>> CreateAsync(int businessId, string nombreCompleto, string email, string contrasena, string rol);
    Task<Result> UpdateAsync(int businessId, string userId, string nombreCompleto, string rol, bool activo);
    Task<Result> SetPasswordAsync(int businessId, string userId, string nuevaContrasena);
}
