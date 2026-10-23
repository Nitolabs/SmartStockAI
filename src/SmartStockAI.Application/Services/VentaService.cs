using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Domain.Entities;
using SmartStockAI.Domain.Enums;
using SmartStockAI.Domain.Interfaces;

namespace SmartStockAI.Application.Services;

public class VentaService : IVentaService
{
    private readonly IRepository<Venta> _ventas;
    private readonly IRepository<Producto> _productos;
    private readonly IRepository<MovimientoInventario> _movimientos;
    private readonly IRepository<Business> _negocios;
    private readonly IUnitOfWork _unitOfWork;

    public VentaService(
        IRepository<Venta> ventas,
        IRepository<Producto> productos,
        IRepository<MovimientoInventario> movimientos,
        IRepository<Business> negocios,
        IUnitOfWork unitOfWork)
    {
        _ventas = ventas;
        _productos = productos;
        _movimientos = movimientos;
        _negocios = negocios;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<VentaDto>> BuscarAsync(int businessId, VentaFiltro filtro)
    {
        var items = await _ventas.FindAsync(v => v.BusinessId == businessId);
        var query = items.AsEnumerable();

        if (filtro.Desde.HasValue) query = query.Where(v => v.Fecha.Date >= filtro.Desde.Value.Date);
        if (filtro.Hasta.HasValue) query = query.Where(v => v.Fecha.Date <= filtro.Hasta.Value.Date);
        if (filtro.ProductoId.HasValue) query = query.Where(v => v.Detalles.Any(d => d.ProductoId == filtro.ProductoId.Value));

        var total = query.Count();
        var pageItems = query
            .OrderByDescending(v => v.Fecha)
            .Skip((filtro.Page - 1) * filtro.PageSize)
            .Take(filtro.PageSize)
            .Select(Map)
            .ToList();

        return new PagedResult<VentaDto> { Items = pageItems, TotalCount = total, Page = filtro.Page, PageSize = filtro.PageSize };
    }

    public async Task<VentaDto?> GetByIdAsync(int businessId, int id)
    {
        var v = await _ventas.FirstOrDefaultAsync(x => x.Id == id && x.BusinessId == businessId);
        return v is null ? null : Map(v);
    }

    public async Task<Result<VentaDto>> RegistrarVentaAsync(int businessId, string usuarioId, string? usuarioNombre, VentaCreateDto dto)
    {
        if (dto.Detalles is null || dto.Detalles.Count == 0)
            return Result<VentaDto>.Fail("La venta debe tener al menos un producto.");

        // Regla determinista: validar TODO antes de tocar cualquier dato (sección 6.8 / 10).
        var permitirSinStock = (await _negocios.GetByIdAsync(businessId))?.PermitirVentaSinStock ?? false;

        var productosEnVenta = new List<Producto>();
        foreach (var linea in dto.Detalles)
        {
            if (linea.Cantidad <= 0)
                return Result<VentaDto>.Fail("La cantidad de cada producto debe ser mayor que cero.");

            var producto = await _productos.FirstOrDefaultAsync(p => p.Id == linea.ProductoId && p.BusinessId == businessId);
            if (producto is null)
                return Result<VentaDto>.Fail($"Producto {linea.ProductoId} no encontrado.");
            if (!producto.Activo)
                return Result<VentaDto>.Fail($"El producto '{producto.Nombre}' no está activo.");

            if (!productosEnVenta.Contains(producto))
                productosEnVenta.Add(producto);
        }

        // El stock se valida contra la suma de TODAS las líneas del mismo producto:
        // dos líneas de 6 unidades con stock 10 no pueden pasar la validación por separado.
        if (!permitirSinStock)
        {
            foreach (var grupo in dto.Detalles.GroupBy(l => l.ProductoId))
            {
                var producto = productosEnVenta.First(p => p.Id == grupo.Key);
                if (producto.StockActual < grupo.Sum(l => l.Cantidad))
                    return Result<VentaDto>.Fail($"Stock insuficiente para '{producto.Nombre}'. Disponible: {producto.StockActual}.");
            }
        }

        var venta = new Venta
        {
            BusinessId = businessId,
            NumeroDocumento = $"V-{DateTime.UtcNow:yyyyMMddHHmmss}",
            Fecha = DateTime.UtcNow,
            UsuarioId = usuarioId,
            UsuarioNombre = usuarioNombre,
            MetodoPago = dto.MetodoPago,
            Estado = EstadoDocumento.Completada
        };

        // Subtotal = suma BRUTA (cantidad x precio, sin descuentos) para no restar el
        // descuento dos veces: una a nivel de línea y otra a nivel de venta.
        decimal subtotalBruto = 0;
        decimal descuentoTotal = 0;
        foreach (var linea in dto.Detalles)
        {
            var producto = productosEnVenta.First(p => p.Id == linea.ProductoId);
            var detalle = new VentaDetalle
            {
                ProductoId = producto.Id,
                Cantidad = linea.Cantidad,
                PrecioUnitario = producto.PrecioVenta,
                Descuento = linea.Descuento
            };
            subtotalBruto += linea.Cantidad * producto.PrecioVenta;
            descuentoTotal += linea.Descuento;
            venta.Detalles.Add(detalle);
        }

        venta.Subtotal = subtotalBruto;
        venta.Descuento = descuentoTotal;
        venta.Total = Math.Max(0, subtotalBruto - descuentoTotal);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _ventas.AddAsync(venta);
            await _ventas.SaveChangesAsync();

            foreach (var linea in dto.Detalles)
            {
                var producto = productosEnVenta.First(p => p.Id == linea.ProductoId);
                producto.StockActual -= linea.Cantidad;
                producto.FechaModificacion = DateTime.UtcNow;
                _productos.Update(producto);

                await _movimientos.AddAsync(new MovimientoInventario
                {
                    BusinessId = businessId,
                    ProductoId = producto.Id,
                    Tipo = TipoMovimientoInventario.Salida,
                    Motivo = MotivoMovimiento.Venta,
                    Cantidad = linea.Cantidad,
                    StockResultante = producto.StockActual,
                    UsuarioId = usuarioId,
                    UsuarioNombre = usuarioNombre,
                    VentaId = venta.Id,
                    Observaciones = $"Venta {venta.NumeroDocumento}"
                });
            }

            await _productos.SaveChangesAsync();
            await _movimientos.SaveChangesAsync();
        });

        return Result<VentaDto>.Ok(Map(venta));
    }

    public async Task<Result> AnularVentaAsync(int businessId, int ventaId)
    {
        var venta = await _ventas.FirstOrDefaultAsync(v => v.Id == ventaId && v.BusinessId == businessId);
        if (venta is null)
            return Result.Fail("Venta no encontrada.");
        if (venta.Estado == EstadoDocumento.Anulada)
            return Result.Fail("La venta ya se encuentra anulada.");

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            foreach (var detalle in venta.Detalles)
            {
                var producto = await _productos.GetByIdAsync(detalle.ProductoId);
                if (producto is null) continue;

                producto.StockActual += detalle.Cantidad;
                _productos.Update(producto);

                await _movimientos.AddAsync(new MovimientoInventario
                {
                    BusinessId = businessId,
                    ProductoId = producto.Id,
                    Tipo = TipoMovimientoInventario.Entrada,
                    Motivo = MotivoMovimiento.DevolucionCliente,
                    Cantidad = detalle.Cantidad,
                    StockResultante = producto.StockActual,
                    UsuarioId = venta.UsuarioId,
                    UsuarioNombre = venta.UsuarioNombre,
                    VentaId = venta.Id,
                    Observaciones = $"Anulación de venta {venta.NumeroDocumento}"
                });
            }

            venta.Estado = EstadoDocumento.Anulada;
            venta.FechaModificacion = DateTime.UtcNow;
            _ventas.Update(venta);

            await _productos.SaveChangesAsync();
            await _movimientos.SaveChangesAsync();
            await _ventas.SaveChangesAsync();
        });

        return Result.Ok();
    }

    private static VentaDto Map(Venta v) => new(
        v.Id, v.NumeroDocumento, v.Fecha, v.UsuarioNombre,
        v.Subtotal, v.Descuento, v.Total, v.MetodoPago, v.Estado,
        v.Detalles.Select(d => new VentaDetalleDto(
            d.ProductoId, d.Producto?.Nombre ?? string.Empty, d.Cantidad, d.PrecioUnitario, d.Descuento, d.Subtotal)).ToList());
}
