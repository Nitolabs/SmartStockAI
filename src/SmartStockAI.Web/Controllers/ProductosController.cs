using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Infrastructure.Identity;

namespace SmartStockAI.Web.Controllers;

public class ProductosController : BaseController
{
    private readonly IProductoService _productoService;
    private readonly ICategoriaService _categoriaService;
    private readonly IProveedorService _proveedorService;

    public ProductosController(
        UserManager<ApplicationUser> userManager,
        IProductoService productoService,
        ICategoriaService categoriaService,
        IProveedorService proveedorService)
        : base(userManager)
    {
        _productoService = productoService;
        _categoriaService = categoriaService;
        _proveedorService = proveedorService;
    }

    public async Task<IActionResult> Index(string? texto, int? categoriaId, bool? soloStockBajo, bool incluirInactivos = false, int page = 1)
    {
        var filtro = new ProductoBusquedaFiltro(texto, categoriaId, soloStockBajo, page, 10, incluirInactivos);
        var resultado = await _productoService.BuscarAsync(CurrentBusinessId, filtro);

        ViewBag.Categorias = await _categoriaService.GetAllAsync(CurrentBusinessId);
        ViewBag.Proveedores = await _proveedorService.GetAllAsync(CurrentBusinessId);
        ViewBag.Filtro = filtro;

        return View(resultado);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.AdministradorOInventario)]
    public async Task<IActionResult> Create(ProductoCreateDto dto)
    {
        var result = await _productoService.CreateAsync(CurrentBusinessId, CurrentUserId, CurrentUserName, dto);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Success"] = "Producto creado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.AdministradorOInventario)]
    public async Task<IActionResult> Edit(ProductoUpdateDto dto)
    {
        var result = await _productoService.UpdateAsync(CurrentBusinessId, dto);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Success"] = "Producto actualizado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.AdministradorOInventario)]
    public async Task<IActionResult> Deactivate(int id)
    {
        var result = await _productoService.DeactivateAsync(CurrentBusinessId, id);
        if (!result.Success) TempData["Error"] = result.Error;
        else TempData["Success"] = "Producto desactivado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> BuscarJson(string texto)
    {
        var filtro = new ProductoBusquedaFiltro(texto, null, null, 1, 15);
        var resultado = await _productoService.BuscarAsync(CurrentBusinessId, filtro);
        return Json(resultado.Items);
    }
}
