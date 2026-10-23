using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Infrastructure.Identity;

namespace SmartStockAI.Web.Controllers;

[Authorize(Roles = AppRoles.AdministradorOInventario)]
public class ProveedoresController : BaseController
{
    private readonly IProveedorService _proveedorService;

    public ProveedoresController(UserManager<ApplicationUser> userManager, IProveedorService proveedorService)
        : base(userManager)
    {
        _proveedorService = proveedorService;
    }

    public async Task<IActionResult> Index()
    {
        var proveedores = await _proveedorService.GetAllAsync(CurrentBusinessId);
        return View(proveedores);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProveedorCreateDto dto)
    {
        var result = await _proveedorService.CreateAsync(CurrentBusinessId, dto);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Success"] = "Proveedor creado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProveedorUpdateDto dto)
    {
        var result = await _proveedorService.UpdateAsync(CurrentBusinessId, dto);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Success"] = "Proveedor actualizado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        var result = await _proveedorService.DeactivateAsync(CurrentBusinessId, id);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Success"] = "Proveedor desactivado.";
        return RedirectToAction(nameof(Index));
    }
}
