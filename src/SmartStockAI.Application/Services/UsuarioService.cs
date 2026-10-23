using System.ComponentModel.DataAnnotations;
using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;

namespace SmartStockAI.Application.Services;

/// <summary>
/// Gestión de usuarios de un negocio. Reglas: solo se administran usuarios del propio negocio,
/// nadie puede quitarse a sí mismo el rol o el acceso, y siempre debe quedar al menos un administrador activo.
/// </summary>
public class UsuarioService : IUsuarioService
{
    private readonly IUserDirectory _directory;

    public UsuarioService(IUserDirectory directory)
    {
        _directory = directory;
    }

    public Task<IReadOnlyList<UsuarioDto>> ListarAsync(int businessId) =>
        _directory.ListByBusinessAsync(businessId);

    public async Task<Result<string>> CrearAsync(int businessId, UsuarioCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NombreCompleto))
            return Result<string>.Fail("El nombre completo es obligatorio.");
        if (string.IsNullOrWhiteSpace(dto.Email) || !new EmailAddressAttribute().IsValid(dto.Email))
            return Result<string>.Fail("El correo electrónico no es válido.");
        if (string.IsNullOrWhiteSpace(dto.Contrasena))
            return Result<string>.Fail("La contraseña es obligatoria.");
        if (!AppRoles.EsValido(dto.Rol))
            return Result<string>.Fail("El rol seleccionado no es válido.");

        var email = dto.Email.Trim();
        if (await _directory.EmailExistsAsync(email))
            return Result<string>.Fail("Ya existe un usuario con ese correo.");

        return await _directory.CreateAsync(businessId, dto.NombreCompleto.Trim(), email, dto.Contrasena, dto.Rol);
    }

    public async Task<Result> ActualizarAsync(int businessId, string usuarioActualId, UsuarioUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NombreCompleto))
            return Result.Fail("El nombre completo es obligatorio.");
        if (!AppRoles.EsValido(dto.Rol))
            return Result.Fail("El rol seleccionado no es válido.");

        var usuario = await _directory.FindAsync(businessId, dto.Id);
        if (usuario is null)
            return Result.Fail("Usuario no encontrado.");

        var cambiaRol = usuario.Rol != dto.Rol;
        var pierdeAcceso = usuario.Activo && !dto.Activo;

        if (dto.Id == usuarioActualId && (cambiaRol || pierdeAcceso))
            return Result.Fail("No puedes cambiar tu propio rol ni desactivar tu propia cuenta.");

        var dejaDeSerAdminActivo = usuario.Rol == AppRoles.Administrador && usuario.Activo
                                   && (dto.Rol != AppRoles.Administrador || !dto.Activo);
        if (dejaDeSerAdminActivo && await _directory.CountActiveAdminsAsync(businessId) <= 1)
            return Result.Fail("Debe quedar al menos un administrador activo en el negocio.");

        return await _directory.UpdateAsync(businessId, dto.Id, dto.NombreCompleto.Trim(), dto.Rol, dto.Activo);
    }

    public async Task<Result> RestablecerContrasenaAsync(int businessId, string usuarioId, string nuevaContrasena)
    {
        if (string.IsNullOrWhiteSpace(nuevaContrasena))
            return Result.Fail("La nueva contraseña es obligatoria.");

        var usuario = await _directory.FindAsync(businessId, usuarioId);
        if (usuario is null)
            return Result.Fail("Usuario no encontrado.");

        return await _directory.SetPasswordAsync(businessId, usuarioId, nuevaContrasena);
    }
}
