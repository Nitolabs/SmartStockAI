using SmartStockAI.Domain.Enums;

namespace SmartStockAI.Application.DTOs;

public record CompraDetalleDto(int ProductoId, string ProductoNombre, int Cantidad, decimal CostoUnitario, decimal Subtotal);

public record CompraDto(
    int Id,
    string NumeroDocumento,
    DateTime Fecha,
    int ProveedorId,
    string ProveedorNombre,
    string? UsuarioNombre,
    decimal Total,
    EstadoDocumento Estado,
    IReadOnlyList<CompraDetalleDto> Detalles);

public record CompraDetalleCreateDto(int ProductoId, int Cantidad, decimal CostoUnitario);

public record CompraCreateDto(int ProveedorId, IReadOnlyList<CompraDetalleCreateDto> Detalles);

public record CompraFiltro(DateTime? Desde, DateTime? Hasta, int? ProveedorId, int Page = 1, int PageSize = 10);
