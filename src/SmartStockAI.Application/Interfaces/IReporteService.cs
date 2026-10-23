using SmartStockAI.Application.DTOs;

namespace SmartStockAI.Application.Interfaces;

public interface IReporteService
{
    Task<IReadOnlyList<ReporteVentasDto>> ReporteVentasAsync(int businessId, ReporteFiltro filtro);
    Task<IReadOnlyList<ReporteComprasDto>> ReporteComprasAsync(int businessId, ReporteFiltro filtro);
    Task<IReadOnlyList<ReporteInventarioDto>> ReporteInventarioAsync(int businessId, ReporteFiltro filtro);
    Task<IReadOnlyList<ReporteBajaRotacionDto>> ReporteBajaRotacionAsync(int businessId, int diasSinVenta = 30);
    Task<IReadOnlyList<ReporteGananciasDto>> ReporteGananciasAsync(int businessId, ReporteFiltro filtro);
    Task<IReadOnlyList<ReporteProductoMasVendidoDto>> ReporteProductosMasVendidosAsync(int businessId, ReporteFiltro filtro, int top = 10);
    Task<IReadOnlyList<ReporteMovimientoDto>> ReporteMovimientosAsync(int businessId, ReporteFiltro filtro);
}
