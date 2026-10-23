using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Infrastructure.Identity;

namespace SmartStockAI.Web.Controllers;

[Authorize(Roles = AppRoles.AdministradorOInventario)]
public class InventarioController : BaseController
{
    private readonly IInventarioService _inventarioService;
    private readonly IProductoService _productoService;

    public InventarioController(
        UserManager<ApplicationUser> userManager,
        IInventarioService inventarioService,
        IProductoService productoService)
        : base(userManager)
    {
        _inventarioService = inventarioService;
        _productoService = productoService;
    }

    public async Task<IActionResult> Index(int? productoId)
    {
        ViewBag.Resumen = await _inventarioService.GetResumenAsync(CurrentBusinessId);
        ViewBag.Productos = (await _productoService.BuscarAsync(CurrentBusinessId, new ProductoBusquedaFiltro(null, null, null, 1, 500))).Items;

        var movimientos = await _inventarioService.GetMovimientosAsync(CurrentBusinessId, productoId);
        return View(movimientos);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ajustar(AjusteInventarioDto dto)
    {
        var result = await _inventarioService.RegistrarAjusteAsync(CurrentBusinessId, CurrentUserId, CurrentUserName, dto);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Success"] = "Movimiento de inventario registrado correctamente.";
        return RedirectToAction(nameof(Index));
    }
}
