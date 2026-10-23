using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Infrastructure.Identity;

namespace SmartStockAI.Web.Controllers;

[Authorize(Roles = AppRoles.AdministradorOVendedor)]
public class VentasController : BaseController
{
    private readonly IVentaService _ventaService;
    private readonly IProductoService _productoService;

    public VentasController(
        UserManager<ApplicationUser> userManager,
        IVentaService ventaService,
        IProductoService productoService)
        : base(userManager)
    {
        _ventaService = ventaService;
        _productoService = productoService;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        var filtro = new VentaFiltro(null, null, null, page, 10);
        var ventas = await _ventaService.BuscarAsync(CurrentBusinessId, filtro);
        return View(ventas);
    }

    /// <summary>Autocompletado de productos desde el buscador de la pantalla de ventas.</summary>
    [HttpGet]
    public async Task<IActionResult> BuscarProductos(string? texto)
    {
        var filtro = new ProductoBusquedaFiltro(texto, null, null, 1, 15);
        var resultado = await _productoService.BuscarAsync(CurrentBusinessId, filtro);
        return Json(resultado.Items.Where(p => p.Activo));
    }

    /// <summary>Registra la venta vía AJAX (JSON) desde el "Gestor de Ventas".</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar([FromBody] VentaCreateDto dto)
    {
        var result = await _ventaService.RegistrarVentaAsync(CurrentBusinessId, CurrentUserId, CurrentUserName, dto);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(new { message = "Venta registrada correctamente.", venta = result.Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anular(int id)
    {
        var result = await _ventaService.AnularVentaAsync(CurrentBusinessId, id);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Success"] = "Venta anulada; el inventario fue restaurado.";
        return RedirectToAction(nameof(Index));
    }
}
