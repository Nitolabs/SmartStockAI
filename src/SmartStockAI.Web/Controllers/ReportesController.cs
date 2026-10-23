using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Infrastructure.Identity;
using SmartStockAI.Web.ViewModels;

namespace SmartStockAI.Web.Controllers;

[Authorize(Roles = AppRoles.AdministradorOInventario)]
public class ReportesController : BaseController
{
    /// <summary>Reportes con información de ventas y rentabilidad: solo para el administrador.</summary>
    private static readonly string[] TiposSoloAdministrador = { "ventas", "mas-vendidos", "ganancias" };

    private readonly IReporteService _reporteService;
    private readonly ICategoriaService _categoriaService;
    private readonly IProveedorService _proveedorService;
    private readonly IProductoService _productoService;

    public ReportesController(
        UserManager<ApplicationUser> userManager,
        IReporteService reporteService,
        ICategoriaService categoriaService,
        IProveedorService proveedorService,
        IProductoService productoService)
        : base(userManager)
    {
        _reporteService = reporteService;
        _categoriaService = categoriaService;
        _proveedorService = proveedorService;
        _productoService = productoService;
    }

    private string TipoPorDefecto() => User.IsInRole(AppRoles.Administrador) ? "ventas" : "inventario";

    private bool PuedeVer(string tipo) =>
        User.IsInRole(AppRoles.Administrador) || !TiposSoloAdministrador.Contains(tipo);

    public async Task<IActionResult> Index(DateTime? desde, DateTime? hasta, int? productoId, int? categoriaId, int? proveedorId, string? tipo = null)
    {
        // Sin tipo explícito: el administrador entra a ventas; los demás roles, a inventario.
        tipo ??= TipoPorDefecto();
        if (!PuedeVer(tipo)) return Forbid();

        var filtro = new ReporteFiltro(desde, hasta, productoId, categoriaId, proveedorId);

        ViewBag.Categorias = await _categoriaService.GetAllAsync(CurrentBusinessId);
        ViewBag.Proveedores = await _proveedorService.GetAllAsync(CurrentBusinessId);
        ViewBag.Productos = (await _productoService.BuscarAsync(CurrentBusinessId, new ProductoBusquedaFiltro(null, null, null, 1, 500))).Items;

        var vm = new ReportesViewModel { Tipo = tipo, Filtro = filtro };

        switch (tipo)
        {
            case "mas-vendidos":
                vm.MasVendidos = await _reporteService.ReporteProductosMasVendidosAsync(CurrentBusinessId, filtro);
                break;
            case "compras":
                vm.Compras = await _reporteService.ReporteComprasAsync(CurrentBusinessId, filtro);
                break;
            case "inventario":
                vm.Inventario = await _reporteService.ReporteInventarioAsync(CurrentBusinessId, filtro);
                break;
            case "movimientos":
                vm.Movimientos = await _reporteService.ReporteMovimientosAsync(CurrentBusinessId, filtro);
                break;
            case "baja-rotacion":
                vm.BajaRotacion = await _reporteService.ReporteBajaRotacionAsync(CurrentBusinessId);
                break;
            case "ganancias":
                vm.Ganancias = await _reporteService.ReporteGananciasAsync(CurrentBusinessId, filtro);
                break;
            default:
                vm.Tipo = "ventas";
                vm.Ventas = await _reporteService.ReporteVentasAsync(CurrentBusinessId, filtro);
                break;
        }

        return View(vm);
    }

    /// <summary>Exporta el reporte activo a CSV (el botón "Generar Reporte" del prototipo).</summary>
    public async Task<IActionResult> Exportar(DateTime? desde, DateTime? hasta, int? productoId, int? categoriaId, int? proveedorId, string? tipo = null)
    {
        tipo ??= TipoPorDefecto();
        if (!PuedeVer(tipo)) return Forbid();

        var filtro = new ReporteFiltro(desde, hasta, productoId, categoriaId, proveedorId);
        var sb = new StringBuilder();

        switch (tipo)
        {
            case "mas-vendidos":
                sb.AppendLine("Producto,Categoria,CantidadVendida,TotalVendido,GananciaEstimada");
                foreach (var r in await _reporteService.ReporteProductosMasVendidosAsync(CurrentBusinessId, filtro))
                    sb.AppendLine(Fila(r.ProductoNombre, r.CategoriaNombre, r.CantidadVendida, r.TotalVendido, r.GananciaEstimada));
                break;
            case "compras":
                sb.AppendLine("Fecha,Producto,Proveedor,Cantidad,Total");
                foreach (var r in await _reporteService.ReporteComprasAsync(CurrentBusinessId, filtro))
                    sb.AppendLine(Fila(r.Fecha, r.ProductoNombre, r.ProveedorNombre, r.Cantidad, r.Total));
                break;
            case "inventario":
                sb.AppendLine("Producto,Categoria,StockActual,StockMinimo,ValorStock");
                foreach (var r in await _reporteService.ReporteInventarioAsync(CurrentBusinessId, filtro))
                    sb.AppendLine(Fila(r.ProductoNombre, r.CategoriaNombre, r.StockActual, r.StockMinimo, r.ValorStock));
                break;
            case "movimientos":
                sb.AppendLine("Fecha,Producto,Tipo,Motivo,Cantidad,StockResultante,Usuario,Observaciones");
                foreach (var r in await _reporteService.ReporteMovimientosAsync(CurrentBusinessId, filtro))
                    sb.AppendLine(Fila(r.Fecha, r.ProductoNombre, r.Tipo, r.Motivo, r.Cantidad, r.StockResultante, r.UsuarioNombre, r.Observaciones));
                break;
            case "baja-rotacion":
                sb.AppendLine("Producto,UnidadesVendidas30Dias,UltimaVenta");
                foreach (var r in await _reporteService.ReporteBajaRotacionAsync(CurrentBusinessId))
                    sb.AppendLine(Fila(r.ProductoNombre, r.UnidadesVendidasUltimos30Dias, r.UltimaVenta));
                break;
            case "ganancias":
                sb.AppendLine("Fecha,TotalVentas,CostoEstimado,GananciaEstimada");
                foreach (var r in await _reporteService.ReporteGananciasAsync(CurrentBusinessId, filtro))
                    sb.AppendLine(Fila(r.Fecha, r.TotalVentas, r.CostoEstimado, r.GananciaEstimada));
                break;
            default:
                tipo = "ventas";
                sb.AppendLine("Fecha,Producto,Cantidad,Total");
                foreach (var r in await _reporteService.ReporteVentasAsync(CurrentBusinessId, filtro))
                    sb.AppendLine(Fila(r.Fecha, r.ProductoNombre, r.Cantidad, r.Total));
                break;
        }

        // BOM UTF-8 para que Excel muestre bien las tildes.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"reporte-{tipo}-{DateTime.UtcNow:yyyyMMddHHmm}.csv");
    }

    private static string Fila(params object?[] valores) => string.Join(",", valores.Select(Csv));

    /// <summary>Formato invariante y escape de comillas/comas/saltos; neutraliza fórmulas (=, +, -, @) de Excel.</summary>
    private static string Csv(object? valor)
    {
        var texto = valor switch
        {
            null => string.Empty,
            DateTime fecha => fecha.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            decimal d => d.ToString("0.00", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => valor.ToString() ?? string.Empty
        };

        if (valor is string && texto.Length > 0 && "=+-@".Contains(texto[0]))
            texto = "'" + texto;

        return texto.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            ? "\"" + texto.Replace("\"", "\"\"") + "\""
            : texto;
    }
}
