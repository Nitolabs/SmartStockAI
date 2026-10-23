using SmartStockAI.Application.DTOs;

namespace SmartStockAI.Application.Interfaces;

public interface IRecomendacionService
{
    Task<RecomendacionesResultadoDto> GenerarAsync(int businessId, CancellationToken ct = default);
}
