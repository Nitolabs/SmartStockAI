using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Domain.Entities;
using SmartStockAI.Domain.Enums;
using SmartStockAI.Domain.Interfaces;

namespace SmartStockAI.Application.Services;

public class CompraService : ICompraService
{
    private readonly IRepository<Compra> _compras;
    private readonly IRepository<Producto> _productos;
    private readonly IRepository<Proveedor> _proveedores;
    private readonly IRepository<MovimientoInventario> _movimientos;
    private readonly IUnitOfWork _unitOfWork;

    public CompraService(
        IRepository<Compra> compras,
        IRepository<Producto> productos,
        IRepository<Proveedor> proveedores,
        IRepository<MovimientoInventario> movimientos,
        IUnitOfWork unitOfWork)
    {
        _compras = compras;
        _productos = productos;
        _proveedores = proveedores;
        _movimientos = movimientos;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<CompraDto>> BuscarAsync(int businessId, CompraFiltro filtro)
    {
        var items = await _compras.FindAsync(c => c.BusinessId == businessId);
        var query = items.AsEnumerable();

        if (filtro.Desde.HasValue) query = query.Where(c => c.Fecha.Date >= filtro.Desde.Value.Date);
        if (filtro.Hasta.HasValue) query = query.Where(c => c.Fecha.Date <= filtro.Hasta.Value.Date);
        if (filtro.ProveedorId.HasValue) query = query.Where(c => c.ProveedorId == filtro.ProveedorId.Value);

        var total = query.Count();
        var pageItems = query
            .OrderByDescending(c => c.Fecha)
            .Skip((filtro.Page - 1) * filtro.PageSize)
            .Take(filtro.PageSize)
            .Select(Map)
            .ToList();

        return new PagedResult<CompraDto> { Items = pageItems, TotalCount = total, Page = filtro.Page, PageSize = filtro.PageSize };
    }

    public async Task<CompraDto?> GetByIdAsync(int businessId, int id)
    {
        var c = await _compras.FirstOrDefaultAsync(x => x.Id == id && x.BusinessId == businessId);
        return c is null ? null : Map(c);
    }

    public async Task<Result<CompraDto>> RegistrarCompraAsync(int businessId, string usuarioId, string? usuarioNombre, CompraCreateDto dto)
    {
        if (dto.Detalles is null || dto.Detalles.Count == 0)
            return Result<CompraDto>.Fail("La compra debe tener al menos un producto.");

        var proveedor = await _proveedores.FirstOrDefaultAsync(p => p.Id == dto.ProveedorId && p.BusinessId == businessId);
        if (proveedor is null)
            return Result<CompraDto>.Fail("Proveedor no encontrado.");

        var productosEnCompra = new List<Producto>();
        foreach (var linea in dto.Detalles)
        {
            if (linea.Cantidad <= 0)
                return Result<CompraDto>.Fail("La cantidad de cada producto debe ser mayor que cero.");
            if (linea.CostoUnitario < 0)
                return Result<CompraDto>.Fail("El costo unitario no puede ser negativo.");

            var producto = await _productos.FirstOrDefaultAsync(p => p.Id == linea.ProductoId && p.BusinessId == businessId);
            if (producto is null)
                return Result<CompraDto>.Fail($"Producto {linea.ProductoId} no encontrado.");

            productosEnCompra.Add(producto);
        }

        var compra = new Compra
        {
            BusinessId = businessId,
            NumeroDocumento = $"C-{DateTime.UtcNow:yyyyMMddHHmmss}",
            Fecha = DateTime.UtcNow,
            ProveedorId = dto.ProveedorId,
            UsuarioId = usuarioId,
            UsuarioNombre = usuarioNombre,
            Estado = EstadoDocumento.Completada
        };

        decimal total = 0;
        foreach (var linea in dto.Detalles)
        {
            var detalle = new CompraDetalle
            {
                ProductoId = linea.ProductoId,
                Cantidad = linea.Cantidad,
                CostoUnitario = linea.CostoUnitario
            };
            total += detalle.Subtotal;
            compra.Detalles.Add(detalle);
        }
        compra.Total = total;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _compras.AddAsync(compra);
            await _compras.SaveChangesAsync();

            foreach (var linea in dto.Detalles)
            {
                var producto = productosEnCompra.First(p => p.Id == linea.ProductoId);
                producto.StockActual += linea.Cantidad;
                // Sección 6.7: la compra confirmada actualiza automáticamente el costo del producto.
                producto.PrecioCompra = linea.CostoUnitario;
                producto.FechaModificacion = DateTime.UtcNow;
                _productos.Update(producto);

                await _movimientos.AddAsync(new MovimientoInventario
                {
                    BusinessId = businessId,
                    ProductoId = producto.Id,
                    Tipo = TipoMovimientoInventario.Entrada,
                    Motivo = MotivoMovimiento.Compra,
                    Cantidad = linea.Cantidad,
                    StockResultante = producto.StockActual,
                    UsuarioId = usuarioId,
                    UsuarioNombre = usuarioNombre,
                    CompraId = compra.Id,
                    Observaciones = $"Compra {compra.NumeroDocumento} - {proveedor.Nombre}"
                });
            }

            await _productos.SaveChangesAsync();
            await _movimientos.SaveChangesAsync();
        });

        return Result<CompraDto>.Ok(Map(compra));
    }

    public async Task<Result> AnularCompraAsync(int businessId, int compraId)
    {
        var compra = await _compras.FirstOrDefaultAsync(c => c.Id == compraId && c.BusinessId == businessId);
        if (compra is null)
            return Result.Fail("Compra no encontrada.");
        if (compra.Estado == EstadoDocumento.Anulada)
            return Result.Fail("La compra ya se encuentra anulada.");

        foreach (var detalle in compra.Detalles)
        {
            var producto = await _productos.GetByIdAsync(detalle.ProductoId);
            if (producto is not null && producto.StockActual < detalle.Cantidad)
                return Result.Fail($"No se puede anular: el producto '{producto.Nombre}' ya no tiene suficiente stock para revertir la compra.");
        }

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            foreach (var detalle in compra.Detalles)
            {
                var producto = await _productos.GetByIdAsync(detalle.ProductoId);
                if (producto is null) continue;

                producto.StockActual -= detalle.Cantidad;
                _productos.Update(producto);

                await _movimientos.AddAsync(new MovimientoInventario
                {
                    BusinessId = businessId,
                    ProductoId = producto.Id,
                    Tipo = TipoMovimientoInventario.Salida,
                    Motivo = MotivoMovimiento.DevolucionProveedor,
                    Cantidad = detalle.Cantidad,
                    StockResultante = producto.StockActual,
                    UsuarioId = compra.UsuarioId,
                    UsuarioNombre = compra.UsuarioNombre,
                    CompraId = compra.Id,
                    Observaciones = $"Anulación de compra {compra.NumeroDocumento}"
                });
            }

            compra.Estado = EstadoDocumento.Anulada;
            compra.FechaModificacion = DateTime.UtcNow;
            _compras.Update(compra);

            await _productos.SaveChangesAsync();
            await _movimientos.SaveChangesAsync();
            await _compras.SaveChangesAsync();
        });

        return Result.Ok();
    }

    private static CompraDto Map(Compra c) => new(
        c.Id, c.NumeroDocumento, c.Fecha, c.ProveedorId, c.Proveedor?.Nombre ?? string.Empty, c.UsuarioNombre,
        c.Total, c.Estado,
        c.Detalles.Select(d => new CompraDetalleDto(
            d.ProductoId, d.Producto?.Nombre ?? string.Empty, d.Cantidad, d.CostoUnitario, d.Subtotal)).ToList());
}
