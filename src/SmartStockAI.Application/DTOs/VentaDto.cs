using SmartStockAI.Domain.Enums;

namespace SmartStockAI.Application.DTOs;

public record VentaDetalleDto(int ProductoId, string ProductoNombre, int Cantidad, decimal PrecioUnitario, decimal Descuento, decimal Subtotal);

public record VentaDto(
    int Id,
    string NumeroDocumento,
    DateTime Fecha,
    string? UsuarioNombre,
    decimal Subtotal,
    decimal Descuento,
    decimal Total,
    MetodoPago MetodoPago,
    EstadoDocumento Estado,
    IReadOnlyList<VentaDetalleDto> Detalles);

public record VentaDetalleCreateDto(int ProductoId, int Cantidad, decimal Descuento);

public record VentaCreateDto(MetodoPago MetodoPago, decimal Descuento, IReadOnlyList<VentaDetalleCreateDto> Detalles);

public record VentaFiltro(DateTime? Desde, DateTime? Hasta, int? ProductoId, int Page = 1, int PageSize = 10);
