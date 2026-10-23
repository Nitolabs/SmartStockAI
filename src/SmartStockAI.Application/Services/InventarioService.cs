using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Domain.Entities;
using SmartStockAI.Domain.Enums;
using SmartStockAI.Domain.Interfaces;

namespace SmartStockAI.Application.Services;

public class InventarioService : IInventarioService
{
    private readonly IRepository<Producto> _productos;
    private readonly IRepository<MovimientoInventario> _movimientos;
    private readonly IUnitOfWork _unitOfWork;

    public InventarioService(
        IRepository<Producto> productos,
        IRepository<MovimientoInventario> movimientos,
        IUnitOfWork unitOfWork)
    {
        _productos = productos;
        _movimientos = movimientos;
        _unitOfWork = unitOfWork;
    }

    public async Task<InventarioResumenDto> GetResumenAsync(int businessId)
    {
        var productos = await _productos.FindAsync(p => p.BusinessId == businessId && p.Activo);
        var movimientosHoy = await _movimientos.FindAsync(
            m => m.BusinessId == businessId && m.Fecha.Date == DateTime.UtcNow.Date);

        return new InventarioResumenDto(
            TotalProductos: productos.Count,
            ProductosStockBajo: productos.Count(p => p.TieneStockBajo),
            ValorTotalInventario: productos.Sum(p => p.StockActual * p.PrecioVenta),
            MovimientosHoy: movimientosHoy.Count);
    }

    public async Task<IReadOnlyList<MovimientoInventarioDto>> GetMovimientosAsync(int businessId, int? productoId = null, int take = 100)
    {
        var movimientos = productoId.HasValue
            ? await _movimientos.FindAsync(m => m.BusinessId == businessId && m.ProductoId == productoId.Value)
            : await _movimientos.FindAsync(m => m.BusinessId == businessId);

        return movimientos
            .OrderByDescending(m => m.Fecha)
            .Take(take)
            .Select(Map)
            .ToList();
    }

    public async Task<Result> RegistrarAjusteAsync(int businessId, string usuarioId, string? usuarioNombre, AjusteInventarioDto dto)
    {
        if (dto.Cantidad <= 0)
            return Result.Fail("La cantidad del ajuste debe ser mayor que cero.");

        var producto = await _productos.FirstOrDefaultAsync(p => p.Id == dto.ProductoId && p.BusinessId == businessId);
        if (producto is null)
            return Result.Fail("Producto no encontrado.");

        int nuevoStock = dto.Tipo switch
        {
            TipoMovimientoInventario.Entrada => producto.StockActual + dto.Cantidad,
            TipoMovimientoInventario.Salida => producto.StockActual - dto.Cantidad,
            TipoMovimientoInventario.Ajuste => dto.Cantidad,
            _ => producto.StockActual
        };

        if (nuevoStock < 0)
            return Result.Fail("El ajuste dejaría el stock en un valor negativo.");

        var stockAnterior = producto.StockActual;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            producto.StockActual = nuevoStock;
            producto.FechaModificacion = DateTime.UtcNow;
            _productos.Update(producto);
            await _productos.SaveChangesAsync();

            await _movimientos.AddAsync(new MovimientoInventario
            {
                BusinessId = businessId,
                ProductoId = producto.Id,
                Tipo = dto.Tipo,
                Motivo = MotivoMovimiento.AjusteManual,
                Cantidad = dto.Tipo == TipoMovimientoInventario.Ajuste
                    ? Math.Abs(nuevoStock - stockAnterior)
                    : dto.Cantidad,
                StockResultante = nuevoStock,
                UsuarioId = usuarioId,
                UsuarioNombre = usuarioNombre,
                Observaciones = dto.Observaciones
            });
            await _movimientos.SaveChangesAsync();
        });

        return Result.Ok();
    }

    private static MovimientoInventarioDto Map(MovimientoInventario m) => new(
        m.Id, m.ProductoId, m.Producto?.Nombre ?? string.Empty,
        m.Tipo, m.Motivo, m.Cantidad, m.StockResultante, m.Fecha, m.UsuarioNombre, m.Observaciones);
}
