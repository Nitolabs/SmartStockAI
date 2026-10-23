using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;

namespace SmartStockAI.Application.Interfaces;

public interface INegocioService
{
    Task<NegocioDto?> ObtenerAsync(int businessId);
    Task<Result> ActualizarAsync(int businessId, NegocioUpdateDto dto);
}
