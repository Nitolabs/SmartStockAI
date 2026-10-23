using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;

namespace SmartStockAI.Infrastructure.Identity;

/// <summary>
/// Implementación de IUserDirectory sobre ASP.NET Core Identity. Todas las consultas se filtran por BusinessId,
/// de modo que un administrador solo ve y modifica usuarios de su propio negocio.
/// </summary>
public class UserDirectory : IUserDirectory
{
    private readonly UserManager<ApplicationUser> _users;

    public UserDirectory(UserManager<ApplicationUser> users)
    {
        _users = users;
    }

    public async Task<IReadOnlyList<UsuarioDto>> ListByBusinessAsync(int businessId)
    {
        var usuarios = await _users.Users
            .Where(u => u.BusinessId == businessId)
            .OrderBy(u => u.NombreCompleto)
            .ToListAsync();

        var resultado = new List<UsuarioDto>();
        foreach (var u in usuarios)
            resultado.Add(await ToDtoAsync(u));
        return resultado;
    }

    public async Task<UsuarioDto?> FindAsync(int businessId, string userId)
    {
        var user = await FindEntityAsync(businessId, userId);
        return user is null ? null : await ToDtoAsync(user);
    }

    public async Task<bool> EmailExistsAsync(string email) =>
        await _users.FindByEmailAsync(email) is not null;

    public async Task<int> CountActiveAdminsAsync(int businessId)
    {
        var admins = await _users.GetUsersInRoleAsync(AppRoles.Administrador);
        return admins.Count(u => u.BusinessId == businessId && u.Activo);
    }

    public async Task<Result<string>> CreateAsync(int businessId, string nombreCompleto, string email, string contrasena, string rol)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            NombreCompleto = nombreCompleto,
            BusinessId = businessId,
            Activo = true
        };

        var creado = await _users.CreateAsync(user, contrasena);
        if (!creado.Succeeded)
            return Result<string>.Fail(Errores(creado));

        var conRol = await _users.AddToRoleAsync(user, rol);
        if (!conRol.Succeeded)
        {
            await _users.DeleteAsync(user); // no dejar un usuario sin rol
            return Result<string>.Fail(Errores(conRol));
        }

        await _users.AddClaimsAsync(user, new[]
        {
            new Claim("BusinessId", businessId.ToString()),
            new Claim("NombreCompleto", nombreCompleto)
        });

        return Result<string>.Ok(user.Id);
    }

    public async Task<Result> UpdateAsync(int businessId, string userId, string nombreCompleto, string rol, bool activo)
    {
        var user = await FindEntityAsync(businessId, userId);
        if (user is null)
            return Result.Fail("Usuario no encontrado.");

        var estabaActivo = user.Activo;
        user.NombreCompleto = nombreCompleto;
        user.Activo = activo;

        var actualizado = await _users.UpdateAsync(user);
        if (!actualizado.Succeeded)
            return Result.Fail(Errores(actualizado));

        var rolesActuales = await _users.GetRolesAsync(user);
        if (!rolesActuales.Contains(rol) || rolesActuales.Count != 1)
        {
            var quitado = await _users.RemoveFromRolesAsync(user, rolesActuales);
            if (!quitado.Succeeded) return Result.Fail(Errores(quitado));
            var agregado = await _users.AddToRoleAsync(user, rol);
            if (!agregado.Succeeded) return Result.Fail(Errores(agregado));
        }

        await SincronizarClaimNombreAsync(user, nombreCompleto);

        // Al desactivar se invalida la sesión abierta: la cookie deja de ser válida en la próxima validación.
        if (estabaActivo && !activo)
            await _users.UpdateSecurityStampAsync(user);

        return Result.Ok();
    }

    public async Task<Result> SetPasswordAsync(int businessId, string userId, string nuevaContrasena)
    {
        var user = await FindEntityAsync(businessId, userId);
        if (user is null)
            return Result.Fail("Usuario no encontrado.");

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var resultado = await _users.ResetPasswordAsync(user, token, nuevaContrasena);
        return resultado.Succeeded ? Result.Ok() : Result.Fail(Errores(resultado));
    }

    private Task<ApplicationUser?> FindEntityAsync(int businessId, string userId) =>
        _users.Users.FirstOrDefaultAsync(u => u.Id == userId && u.BusinessId == businessId);

    private async Task<UsuarioDto> ToDtoAsync(ApplicationUser u)
    {
        var roles = await _users.GetRolesAsync(u);
        return new UsuarioDto(u.Id, u.NombreCompleto, u.Email ?? u.UserName ?? string.Empty, roles.FirstOrDefault() ?? string.Empty, u.Activo);
    }

    private async Task SincronizarClaimNombreAsync(ApplicationUser user, string nombreCompleto)
    {
        var claims = await _users.GetClaimsAsync(user);
        var actual = claims.FirstOrDefault(c => c.Type == "NombreCompleto");
        if (actual is null)
            await _users.AddClaimAsync(user, new Claim("NombreCompleto", nombreCompleto));
        else if (actual.Value != nombreCompleto)
            await _users.ReplaceClaimAsync(user, actual, new Claim("NombreCompleto", nombreCompleto));
    }

    private static string Errores(IdentityResult r) => string.Join(" ", r.Errors.Select(e => e.Description));
}
