using SmartStockAI.Application.DTOs;

namespace SmartStockAI.Web.ViewModels;

public class ReportesViewModel
{
    public string Tipo { get; set; } = "ventas";
    public ReporteFiltro Filtro { get; set; } = new(null, null, null, null, null);

    public IReadOnlyList<ReporteVentasDto> Ventas { get; set; } = Array.Empty<ReporteVentasDto>();
    public IReadOnlyList<ReporteProductoMasVendidoDto> MasVendidos { get; set; } = Array.Empty<ReporteProductoMasVendidoDto>();
    public IReadOnlyList<ReporteComprasDto> Compras { get; set; } = Array.Empty<ReporteComprasDto>();
    public IReadOnlyList<ReporteInventarioDto> Inventario { get; set; } = Array.Empty<ReporteInventarioDto>();
    public IReadOnlyList<ReporteMovimientoDto> Movimientos { get; set; } = Array.Empty<ReporteMovimientoDto>();
    public IReadOnlyList<ReporteBajaRotacionDto> BajaRotacion { get; set; } = Array.Empty<ReporteBajaRotacionDto>();
    public IReadOnlyList<ReporteGananciasDto> Ganancias { get; set; } = Array.Empty<ReporteGananciasDto>();
}

public class ConfiguracionViewModel
{
    public string NombreCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public NegocioDto? Negocio { get; set; }
}
