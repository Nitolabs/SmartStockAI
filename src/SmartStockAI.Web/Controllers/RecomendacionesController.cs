using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Infrastructure.Identity;

namespace SmartStockAI.Web.Controllers;

[Authorize(Roles = AppRoles.AdministradorOInventario)]
public class RecomendacionesController : BaseController
{
    private readonly IRecomendacionService _recomendacionService;

    public RecomendacionesController(UserManager<ApplicationUser> userManager, IRecomendacionService recomendacionService)
        : base(userManager)
    {
        _recomendacionService = recomendacionService;
    }

    [HttpGet]
    public IActionResult Index() => View((RecomendacionesResultadoDto?)null);

    // POST para que el análisis (y su costo en tokens) sea una acción explícita del usuario.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generar(CancellationToken ct)
    {
        var resultado = await _recomendacionService.GenerarAsync(CurrentBusinessId, ct);
        return View("Index", resultado);
    }

    /// <summary>Versión resumida para el widget del dashboard: las 3 recomendaciones más importantes.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resumen(CancellationToken ct)
    {
        var r = await _recomendacionService.GenerarAsync(CurrentBusinessId, ct);

        // Si la IA no respondió, se muestran las alertas por reglas para que el widget siga siendo útil.
        var principales = (r.IaDisponible && r.RecomendacionesIA.Count > 0 ? r.RecomendacionesIA : r.AlertasPorRegla)
            .Take(3)
            .Select(x => new
            {
                producto = x.ProductoNombre,
                prioridad = x.Prioridad,
                recomendacion = x.Recomendacion,
                razon = x.Razon,
                origen = x.Origen
            });

        return Json(new
        {
            iaDisponible = r.IaDisponible,
            mensaje = r.Mensaje,
            proveedor = r.Proveedor,
            totalAlertas = r.AlertasPorRegla.Count,
            items = principales
        });
    }
}
