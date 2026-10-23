using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Infrastructure.Identity;

namespace SmartStockAI.Web.Controllers;

[Authorize(Roles = AppRoles.AdministradorOInventario)]
public class ComprasController : BaseController
{
    private readonly ICompraService _compraService;
    private readonly IProductoService _productoService;
    private readonly IProveedorService _proveedorService;

    public ComprasController(
        UserManager<ApplicationUser> userManager,
        ICompraService compraService,
        IProductoService productoService,
        IProveedorService proveedorService)
        : base(userManager)
    {
        _compraService = compraService;
        _productoService = productoService;
        _proveedorService = proveedorService;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        var filtro = new CompraFiltro(null, null, null, page, 10);
        var compras = await _compraService.BuscarAsync(CurrentBusinessId, filtro);
        ViewBag.Proveedores = await _proveedorService.GetAllAsync(CurrentBusinessId);
        return View(compras);
    }

    [HttpGet]
    public async Task<IActionResult> BuscarProductos(string? texto)
    {
        var filtro = new ProductoBusquedaFiltro(texto, null, null, 1, 15);
        var resultado = await _productoService.BuscarAsync(CurrentBusinessId, filtro);
        return Json(resultado.Items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar([FromBody] CompraCreateDto dto)
    {
        var result = await _compraService.RegistrarCompraAsync(CurrentBusinessId, CurrentUserId, CurrentUserName, dto);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(new { message = "Compra registrada; el inventario se actualizó automáticamente.", compra = result.Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anular(int id)
    {
        var result = await _compraService.AnularCompraAsync(CurrentBusinessId, id);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Success"] = "Compra anulada; el inventario fue revertido.";
        return RedirectToAction(nameof(Index));
    }
}
