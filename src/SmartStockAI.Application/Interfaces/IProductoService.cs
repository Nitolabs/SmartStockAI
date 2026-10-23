using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;

namespace SmartStockAI.Application.Interfaces;

public interface IProductoService
{
    Task<PagedResult<ProductoDto>> BuscarAsync(int businessId, ProductoBusquedaFiltro filtro);
    Task<ProductoDto?> GetByIdAsync(int businessId, int id);
    Task<Result<ProductoDto>> CreateAsync(int businessId, string usuarioId, string? usuarioNombre, ProductoCreateDto dto);
    Task<Result> UpdateAsync(int businessId, ProductoUpdateDto dto);
    Task<Result> DeactivateAsync(int businessId, int id);
}
