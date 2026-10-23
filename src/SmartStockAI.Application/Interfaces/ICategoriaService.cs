using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;

namespace SmartStockAI.Application.Interfaces;

public interface ICategoriaService
{
    Task<IReadOnlyList<CategoriaDto>> GetAllAsync(int businessId);
    Task<CategoriaDto?> GetByIdAsync(int businessId, int id);
    Task<Result<CategoriaDto>> CreateAsync(int businessId, CategoriaCreateDto dto);
    Task<Result> UpdateAsync(int businessId, CategoriaUpdateDto dto);
    Task<Result> DeactivateAsync(int businessId, int id);
}
