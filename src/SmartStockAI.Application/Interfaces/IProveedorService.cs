using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;

namespace SmartStockAI.Application.Interfaces;

public interface IProveedorService
{
    Task<IReadOnlyList<ProveedorDto>> GetAllAsync(int businessId);
    Task<ProveedorDto?> GetByIdAsync(int businessId, int id);
    Task<Result<ProveedorDto>> CreateAsync(int businessId, ProveedorCreateDto dto);
    Task<Result> UpdateAsync(int businessId, ProveedorUpdateDto dto);
    Task<Result> DeactivateAsync(int businessId, int id);
}
