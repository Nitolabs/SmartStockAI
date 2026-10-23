using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Infrastructure.Identity;

namespace SmartStockAI.Web.Controllers;

/// <summary>Gestión de usuarios y roles del negocio. Solo el administrador.</summary>
[Authorize(Roles = AppRoles.Administrador)]
public class UsuariosController : BaseController
{
    private readonly IUsuarioService _usuarioService;

    public UsuariosController(UserManager<ApplicationUser> userManager, IUsuarioService usuarioService)
        : base(userManager)
    {
        _usuarioService = usuarioService;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.UsuarioActualId = CurrentUserId;
        var usuarios = await _usuarioService.ListarAsync(CurrentBusinessId);
        return View(usuarios);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UsuarioCreateDto dto)
    {
        var result = await _usuarioService.CrearAsync(CurrentBusinessId, dto);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Success"] = "Usuario creado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UsuarioUpdateDto dto)
    {
        var result = await _usuarioService.ActualizarAsync(CurrentBusinessId, CurrentUserId, dto);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Success"] = "Usuario actualizado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id, string nuevaContrasena)
    {
        var result = await _usuarioService.RestablecerContrasenaAsync(CurrentBusinessId, id, nuevaContrasena);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Success"] = "Contraseña restablecida. Compártela con el usuario de forma segura.";
        return RedirectToAction(nameof(Index));
    }
}
