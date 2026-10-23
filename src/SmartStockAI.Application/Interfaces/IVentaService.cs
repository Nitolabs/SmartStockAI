using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;

namespace SmartStockAI.Application.Interfaces;

public interface IVentaService
{
    Task<PagedResult<VentaDto>> BuscarAsync(int businessId, VentaFiltro filtro);
    Task<VentaDto?> GetByIdAsync(int businessId, int id);

    /// <summary>
    /// Registra la venta y reduce el inventario de forma atómica.
    /// Rechaza la operación si no hay stock suficiente para algún renglón,
    /// salvo que el negocio tenga habilitada la venta sin stock (sección 6.8).
    /// </summary>
    Task<Result<VentaDto>> RegistrarVentaAsync(int businessId, string usuarioId, string? usuarioNombre, VentaCreateDto dto);

    Task<Result> AnularVentaAsync(int businessId, int ventaId);
}
