using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;

namespace SmartStockAI.Application.Interfaces;

public interface ICompraService
{
    Task<PagedResult<CompraDto>> BuscarAsync(int businessId, CompraFiltro filtro);
    Task<CompraDto?> GetByIdAsync(int businessId, int id);

    /// <summary>Registra la compra y aumenta el inventario automáticamente (sección 6.7).</summary>
    Task<Result<CompraDto>> RegistrarCompraAsync(int businessId, string usuarioId, string? usuarioNombre, CompraCreateDto dto);

    Task<Result> AnularCompraAsync(int businessId, int compraId);
}
