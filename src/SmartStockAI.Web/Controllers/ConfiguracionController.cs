using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Infrastructure.Identity;
using SmartStockAI.Web.ViewModels;

namespace SmartStockAI.Web.Controllers;

/// <summary>Configuración: datos de la cuenta (todos los roles) y del negocio (solo administrador).</summary>
public class ConfiguracionController : BaseController
{
    private readonly INegocioService _negocioService;

    public ConfiguracionController(UserManager<ApplicationUser> userManager, INegocioService negocioService)
        : base(userManager)
    {
        _negocioService = negocioService;
    }

    public async Task<IActionResult> Index()
    {
        var vm = new ConfiguracionViewModel
        {
            NombreCompleto = CurrentUserName ?? string.Empty,
            Email = User.Identity?.Name ?? string.Empty,
            Rol = AppRoles.Todos.FirstOrDefault(User.IsInRole) ?? string.Empty
        };

        if (User.IsInRole(AppRoles.Administrador))
            vm.Negocio = await _negocioService.ObtenerAsync(CurrentBusinessId);

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Administrador)]
    public async Task<IActionResult> GuardarNegocio(NegocioUpdateDto dto)
    {
        var result = await _negocioService.ActualizarAsync(CurrentBusinessId, dto);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Success"] = "Configuración del negocio guardada.";
        return RedirectToAction(nameof(Index));
    }
}
