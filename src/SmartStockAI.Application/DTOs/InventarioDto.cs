using SmartStockAI.Domain.Enums;

namespace SmartStockAI.Application.DTOs;

public record MovimientoInventarioDto(
    int Id,
    int ProductoId,
    string ProductoNombre,
    TipoMovimientoInventario Tipo,
    MotivoMovimiento Motivo,
    int Cantidad,
    int StockResultante,
    DateTime Fecha,
    string? UsuarioNombre,
    string? Observaciones);

/// <summary>Ajuste manual solicitado desde la pantalla de Inventario.</summary>
public record AjusteInventarioDto(int ProductoId, TipoMovimientoInventario Tipo, int Cantidad, string? Observaciones);

public record InventarioResumenDto(int TotalProductos, int ProductosStockBajo, decimal ValorTotalInventario, int MovimientosHoy);
