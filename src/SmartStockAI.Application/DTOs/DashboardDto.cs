namespace SmartStockAI.Application.DTOs;

public record DashboardDto(
    decimal VentasHoy,
    decimal VentasMes,
    int ProductosVendidosHoy,
    int ProductosStockBajo,
    decimal ValorInventario,
    int ComprasRecientesCount,
    IReadOnlyList<ProductoMasVendidoDto> ProductosMasVendidos,
    IReadOnlyList<AlertaDto> Alertas,
    IReadOnlyList<VentaTendenciaDto> TendenciaVentas,
    IReadOnlyList<CompraResumenDto> ComprasRecientes);

public record CompraResumenDto(string NumeroDocumento, DateTime Fecha, string ProveedorNombre, decimal Total);

public record ProductoMasVendidoDto(string ProductoNombre, int CantidadVendida, decimal TotalVendido);

public record AlertaDto(string Tipo, string Prioridad, string ProductoNombre, string Mensaje);

public record VentaTendenciaDto(DateTime Fecha, decimal Total);
