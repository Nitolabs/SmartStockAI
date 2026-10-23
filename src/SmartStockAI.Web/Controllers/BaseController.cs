using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartStockAI.Infrastructure.Identity;

namespace SmartStockAI.Web.Controllers;

/// <summary>
/// Controlador base: expone el BusinessId y el nombre del usuario autenticado, leídos de los claims de la
/// sesión, para que cada controlador hijo aplique el aislamiento multi-tenant sin repetir lógica
/// (sección 12 del prompt maestro: un usuario nunca ve datos de otro negocio).
/// </summary>
[Authorize]
public abstract class BaseController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;

    protected BaseController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    protected string CurrentUserId => _userManager.GetUserId(User) ?? string.Empty;

    protected string? CurrentUserName =>
        User.FindFirst("NombreCompleto")?.Value ?? User.Identity?.Name;

    /// <summary>BusinessId de la sesión. Si el claim falta devuelve 0, que no coincide con ningún negocio.</summary>
    protected int CurrentBusinessId =>
        int.TryParse(User.FindFirst("BusinessId")?.Value, out var id) ? id : 0;
}
