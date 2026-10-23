namespace SmartStockAI.Application.DTOs;

public record ReporteFiltro(DateTime? Desde, DateTime? Hasta, int? ProductoId, int? CategoriaId, int? ProveedorId);

public record ReporteVentasDto(DateTime Fecha, string ProductoNombre, int Cantidad, decimal Total);

public record ReporteComprasDto(DateTime Fecha, string ProductoNombre, string ProveedorNombre, int Cantidad, decimal Total);

public record ReporteInventarioDto(string ProductoNombre, string CategoriaNombre, int StockActual, int StockMinimo, decimal ValorStock);

public record ReporteBajaRotacionDto(string ProductoNombre, int UnidadesVendidasUltimos30Dias, DateTime? UltimaVenta);

public record ReporteGananciasDto(DateTime Fecha, decimal TotalVentas, decimal CostoEstimado, decimal GananciaEstimada);

public record ReporteProductoMasVendidoDto(
    string ProductoNombre, string CategoriaNombre, int CantidadVendida, decimal TotalVendido, decimal GananciaEstimada);

public record ReporteMovimientoDto(
    DateTime Fecha, string ProductoNombre, string Tipo, string Motivo,
    int Cantidad, int StockResultante, string? UsuarioNombre, string? Observaciones);
