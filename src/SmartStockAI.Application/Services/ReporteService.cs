using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Domain.Entities;
using SmartStockAI.Domain.Enums;
using SmartStockAI.Domain.Interfaces;

namespace SmartStockAI.Application.Services;

/// <summary>
/// Reportes del negocio. Todos admiten filtros por fecha, producto, categoría y proveedor
/// (sección 6.9 del prompt maestro) y excluyen documentos anulados y datos de otros negocios.
/// </summary>
public class ReporteService : IReporteService
{
    private readonly IRepository<Venta> _ventas;
    private readonly IRepository<Compra> _compras;
    private readonly IRepository<Producto> _productos;
    private readonly IRepository<MovimientoInventario> _movimientos;

    public ReporteService(
        IRepository<Venta> ventas,
        IRepository<Compra> compras,
        IRepository<Producto> productos,
        IRepository<MovimientoInventario> movimientos)
    {
        _ventas = ventas;
        _compras = compras;
        _productos = productos;
        _movimientos = movimientos;
    }

    public async Task<IReadOnlyList<ReporteVentasDto>> ReporteVentasAsync(int businessId, ReporteFiltro filtro)
    {
        var ventas = await _ventas.FindAsync(v => v.BusinessId == businessId && v.Estado == EstadoDocumento.Completada);
        var query = FiltrarPorFecha(ventas.AsEnumerable(), v => v.Fecha, filtro.Desde, filtro.Hasta);

        return query
            .SelectMany(v => v.Detalles.Select(d => new { v.Fecha, d }))
            .Where(x => CumpleFiltroDeProducto(x.d.ProductoId, x.d.Producto, filtro))
            .Select(x => new ReporteVentasDto(x.Fecha, x.d.Producto?.Nombre ?? string.Empty, x.d.Cantidad, x.d.Subtotal))
            .OrderByDescending(x => x.Fecha)
            .ToList();
    }

    public async Task<IReadOnlyList<ReporteComprasDto>> ReporteComprasAsync(int businessId, ReporteFiltro filtro)
    {
        var compras = await _compras.FindAsync(c => c.BusinessId == businessId && c.Estado == EstadoDocumento.Completada);
        var query = FiltrarPorFecha(compras.AsEnumerable(), c => c.Fecha, filtro.Desde, filtro.Hasta);

        // El proveedor filtrado es el de la COMPRA (a quién se le compró), no el habitual del producto.
        if (filtro.ProveedorId.HasValue)
            query = query.Where(c => c.ProveedorId == filtro.ProveedorId.Value);

        return query
            .SelectMany(c => c.Detalles.Select(d => new { c.Fecha, c.Proveedor, d }))
            .Where(x => CumpleFiltroDeProducto(x.d.ProductoId, x.d.Producto, filtro with { ProveedorId = null }))
            .Select(x => new ReporteComprasDto(x.Fecha, x.d.Producto?.Nombre ?? string.Empty, x.Proveedor?.Nombre ?? string.Empty, x.d.Cantidad, x.d.Subtotal))
            .OrderByDescending(x => x.Fecha)
            .ToList();
    }

    public async Task<IReadOnlyList<ReporteInventarioDto>> ReporteInventarioAsync(int businessId, ReporteFiltro filtro)
    {
        var productos = await _productos.FindAsync(p => p.BusinessId == businessId && p.Activo);

        return productos
            .Where(p => CumpleFiltroDeProducto(p.Id, p, filtro))
            .Select(p => new ReporteInventarioDto(p.Nombre, p.Categoria?.Nombre ?? string.Empty, p.StockActual, p.StockMinimo, p.StockActual * p.PrecioVenta))
            .OrderBy(x => x.ProductoNombre)
            .ToList();
    }

    public async Task<IReadOnlyList<ReporteBajaRotacionDto>> ReporteBajaRotacionAsync(int businessId, int diasSinVenta = 30)
    {
        // Se parte de Ventas (que ya trae Detalles.Producto incluidos por el repositorio)
        // en vez de navegar Producto -> VentaDetalles, que no viene incluido por defecto.
        var productos = await _productos.FindAsync(p => p.BusinessId == businessId && p.Activo);
        var ventas = await _ventas.FindAsync(v => v.BusinessId == businessId && v.Estado == EstadoDocumento.Completada);
        var limite = DateTime.UtcNow.AddDays(-diasSinVenta);

        var detallesPorProducto = ventas
            .SelectMany(v => v.Detalles.Select(d => new { v.Fecha, d.ProductoId, d.Cantidad }))
            .GroupBy(x => x.ProductoId)
            .ToDictionary(g => g.Key, g => new
            {
                CantidadReciente = g.Where(x => x.Fecha >= limite).Sum(x => x.Cantidad),
                UltimaVenta = g.Max(x => (DateTime?)x.Fecha)
            });

        return productos
            .Select(p =>
            {
                detallesPorProducto.TryGetValue(p.Id, out var info);
                return new ReporteBajaRotacionDto(p.Nombre, info?.CantidadReciente ?? 0, info?.UltimaVenta);
            })
            .Where(r => r.UnidadesVendidasUltimos30Dias == 0)
            .OrderBy(r => r.ProductoNombre)
            .ToList();
    }

    public async Task<IReadOnlyList<ReporteGananciasDto>> ReporteGananciasAsync(int businessId, ReporteFiltro filtro)
    {
        var ventas = await _ventas.FindAsync(v => v.BusinessId == businessId && v.Estado == EstadoDocumento.Completada);
        var query = FiltrarPorFecha(ventas.AsEnumerable(), v => v.Fecha, filtro.Desde, filtro.Hasta);

        // Con filtro de producto/categoría/proveedor se calcula sobre las líneas que cumplen el filtro.
        return query
            .SelectMany(v => v.Detalles
                .Where(d => CumpleFiltroDeProducto(d.ProductoId, d.Producto, filtro))
                .Select(d => new { v.Fecha, Venta = d.Subtotal, Costo = d.Cantidad * (d.Producto?.PrecioCompra ?? 0) }))
            .GroupBy(x => x.Fecha.Date)
            .Select(g => new ReporteGananciasDto(g.Key, g.Sum(x => x.Venta), g.Sum(x => x.Costo), g.Sum(x => x.Venta) - g.Sum(x => x.Costo)))
            .OrderByDescending(r => r.Fecha)
            .ToList();
    }

    public async Task<IReadOnlyList<ReporteProductoMasVendidoDto>> ReporteProductosMasVendidosAsync(int businessId, ReporteFiltro filtro, int top = 10)
    {
        var ventas = await _ventas.FindAsync(v => v.BusinessId == businessId && v.Estado == EstadoDocumento.Completada);
        var query = FiltrarPorFecha(ventas.AsEnumerable(), v => v.Fecha, filtro.Desde, filtro.Hasta);

        return query
            .SelectMany(v => v.Detalles)
            .Where(d => CumpleFiltroDeProducto(d.ProductoId, d.Producto, filtro))
            .GroupBy(d => d.ProductoId)
            .Select(g =>
            {
                var producto = g.First().Producto;
                var total = g.Sum(d => d.Subtotal);
                var costo = g.Sum(d => d.Cantidad * (d.Producto?.PrecioCompra ?? 0));
                return new ReporteProductoMasVendidoDto(
                    producto?.Nombre ?? string.Empty,
                    producto?.Categoria?.Nombre ?? string.Empty,
                    g.Sum(d => d.Cantidad), total, total - costo);
            })
            .OrderByDescending(r => r.CantidadVendida)
            .ThenBy(r => r.ProductoNombre)
            .Take(top)
            .ToList();
    }

    public async Task<IReadOnlyList<ReporteMovimientoDto>> ReporteMovimientosAsync(int businessId, ReporteFiltro filtro)
    {
        var movimientos = await _movimientos.FindAsync(m => m.BusinessId == businessId);
        var query = FiltrarPorFecha(movimientos.AsEnumerable(), m => m.Fecha, filtro.Desde, filtro.Hasta);

        return query
            .Where(m => CumpleFiltroDeProducto(m.ProductoId, m.Producto, filtro))
            .OrderByDescending(m => m.Fecha)
            .Select(m => new ReporteMovimientoDto(
                m.Fecha, m.Producto?.Nombre ?? string.Empty, m.Tipo.ToString(), m.Motivo.ToString(),
                m.Cantidad, m.StockResultante, m.UsuarioNombre, m.Observaciones))
            .ToList();
    }

    /// <summary>Filtros por producto, categoría y proveedor habitual del producto.</summary>
    private static bool CumpleFiltroDeProducto(int productoId, Producto? producto, ReporteFiltro filtro)
    {
        if (filtro.ProductoId.HasValue && productoId != filtro.ProductoId.Value) return false;
        if (filtro.CategoriaId.HasValue && producto?.CategoriaId != filtro.CategoriaId.Value) return false;
        if (filtro.ProveedorId.HasValue && producto?.ProveedorId != filtro.ProveedorId.Value) return false;
        return true;
    }

    private static IEnumerable<T> FiltrarPorFecha<T>(IEnumerable<T> query, Func<T, DateTime> fechaSelector, DateTime? desde, DateTime? hasta)
    {
        if (desde.HasValue) query = query.Where(x => fechaSelector(x).Date >= desde.Value.Date);
        if (hasta.HasValue) query = query.Where(x => fechaSelector(x).Date <= hasta.Value.Date); // el día final se incluye completo
        return query;
    }
}
