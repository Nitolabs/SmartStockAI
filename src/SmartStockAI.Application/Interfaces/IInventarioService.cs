using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;

namespace SmartStockAI.Application.Interfaces;

public interface IInventarioService
{
    Task<InventarioResumenDto> GetResumenAsync(int businessId);
    Task<IReadOnlyList<MovimientoInventarioDto>> GetMovimientosAsync(int businessId, int? productoId = null, int take = 100);
    Task<Result> RegistrarAjusteAsync(int businessId, string usuarioId, string? usuarioNombre, AjusteInventarioDto dto);
}
