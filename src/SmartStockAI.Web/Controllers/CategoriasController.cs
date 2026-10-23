using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Infrastructure.Identity;

namespace SmartStockAI.Web.Controllers;

[Authorize(Roles = AppRoles.AdministradorOInventario)]
public class CategoriasController : BaseController
{
    private readonly ICategoriaService _categoriaService;

    public CategoriasController(UserManager<ApplicationUser> userManager, ICategoriaService categoriaService)
        : base(userManager)
    {
        _categoriaService = categoriaService;
    }

    public async Task<IActionResult> Index()
    {
        var categorias = await _categoriaService.GetAllAsync(CurrentBusinessId);
        return View(categorias);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoriaCreateDto dto)
    {
        var result = await _categoriaService.CreateAsync(CurrentBusinessId, dto);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Success"] = "Categoría creada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CategoriaUpdateDto dto)
    {
        var result = await _categoriaService.UpdateAsync(CurrentBusinessId, dto);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Success"] = "Categoría actualizada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        var result = await _categoriaService.DeactivateAsync(CurrentBusinessId, id);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Success"] = "Categoría desactivada.";
        return RedirectToAction(nameof(Index));
    }
}
