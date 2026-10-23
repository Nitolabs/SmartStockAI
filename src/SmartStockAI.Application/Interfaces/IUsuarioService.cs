using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;

namespace SmartStockAI.Application.Interfaces;

public interface IUsuarioService
{
    Task<IReadOnlyList<UsuarioDto>> ListarAsync(int businessId);
    Task<Result<string>> CrearAsync(int businessId, UsuarioCreateDto dto);
    Task<Result> ActualizarAsync(int businessId, string usuarioActualId, UsuarioUpdateDto dto);
    Task<Result> RestablecerContrasenaAsync(int businessId, string usuarioId, string nuevaContrasena);
}
