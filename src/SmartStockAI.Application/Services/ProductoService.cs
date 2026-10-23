using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Domain.Entities;
using SmartStockAI.Domain.Enums;
using SmartStockAI.Domain.Interfaces;

namespace SmartStockAI.Application.Services;

public class ProductoService : IProductoService
{
    private readonly IRepository<Producto> _productos;
    private readonly IRepository<MovimientoInventario> _movimientos;
    private readonly IUnitOfWork _unitOfWork;

    public ProductoService(
        IRepository<Producto> productos,
        IRepository<MovimientoInventario> movimientos,
        IUnitOfWork unitOfWork)
    {
        _productos = productos;
        _movimientos = movimientos;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<ProductoDto>> BuscarAsync(int businessId, ProductoBusquedaFiltro filtro)
    {
        var items = await _productos.FindAsync(p => p.BusinessId == businessId);

        var query = items.AsEnumerable();

        // Un producto "eliminado" (desactivado) no se lista salvo que se pida explícitamente.
        if (!filtro.IncluirInactivos)
            query = query.Where(p => p.Activo);

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim().ToLower();
            query = query.Where(p =>
                p.Nombre.ToLower().Contains(texto) ||
                p.Sku.ToLower().Contains(texto) ||
                (p.Descripcion ?? string.Empty).ToLower().Contains(texto));
        }

        if (filtro.CategoriaId.HasValue)
            query = query.Where(p => p.CategoriaId == filtro.CategoriaId.Value);

        if (filtro.SoloStockBajo == true)
            query = query.Where(p => p.TieneStockBajo);

        var total = query.Count();

        var pageItems = query
            .OrderBy(p => p.Nombre)
            .Skip((filtro.Page - 1) * filtro.PageSize)
            .Take(filtro.PageSize)
            .Select(Map)
            .ToList();

        return new PagedResult<ProductoDto>
        {
            Items = pageItems,
            TotalCount = total,
            Page = filtro.Page,
            PageSize = filtro.PageSize
        };
    }

    public async Task<ProductoDto?> GetByIdAsync(int businessId, int id)
    {
        var p = await _productos.FirstOrDefaultAsync(x => x.Id == id && x.BusinessId == businessId);
        return p is null ? null : Map(p);
    }

    public async Task<Result<ProductoDto>> CreateAsync(int businessId, string usuarioId, string? usuarioNombre, ProductoCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return Result<ProductoDto>.Fail("El nombre del producto es obligatorio.");
        if (string.IsNullOrWhiteSpace(dto.Sku))
            return Result<ProductoDto>.Fail("El SKU/código del producto es obligatorio.");
        if (dto.PrecioVenta < 0 || dto.PrecioCompra < 0)
            return Result<ProductoDto>.Fail("Los precios no pueden ser negativos.");
        if (dto.StockInicial < 0 || dto.StockMinimo < 0)
            return Result<ProductoDto>.Fail("El stock no puede ser negativo.");

        var skuDuplicado = await _productos.FirstOrDefaultAsync(
            p => p.BusinessId == businessId && p.Sku.ToLower() == dto.Sku.ToLower());
        if (skuDuplicado is not null)
            return Result<ProductoDto>.Fail("Ya existe un producto con ese SKU/código.");

        var producto = new Producto
        {
            BusinessId = businessId,
            Nombre = dto.Nombre.Trim(),
            Descripcion = dto.Descripcion,
            Sku = dto.Sku.Trim(),
            CategoriaId = dto.CategoriaId,
            ProveedorId = dto.ProveedorId,
            PrecioCompra = dto.PrecioCompra,
            PrecioVenta = dto.PrecioVenta,
            StockActual = dto.StockInicial,
            StockMinimo = dto.StockMinimo
        };

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _productos.AddAsync(producto);
            await _productos.SaveChangesAsync();

            if (dto.StockInicial > 0)
            {
                await _movimientos.AddAsync(new MovimientoInventario
                {
                    BusinessId = businessId,
                    ProductoId = producto.Id,
                    Tipo = TipoMovimientoInventario.Entrada,
                    Motivo = MotivoMovimiento.AjusteManual,
                    Cantidad = dto.StockInicial,
                    StockResultante = producto.StockActual,
                    UsuarioId = usuarioId,
                    UsuarioNombre = usuarioNombre,
                    Observaciones = "Stock inicial al crear el producto"
                });
                await _movimientos.SaveChangesAsync();
            }
        });

        return Result<ProductoDto>.Ok(Map(producto));
    }

    public async Task<Result> UpdateAsync(int businessId, ProductoUpdateDto dto)
    {
        var producto = await _productos.FirstOrDefaultAsync(p => p.Id == dto.Id && p.BusinessId == businessId);
        if (producto is null)
            return Result.Fail("Producto no encontrado.");

        var skuDuplicado = await _productos.FirstOrDefaultAsync(
            p => p.BusinessId == businessId && p.Id != dto.Id && p.Sku.ToLower() == dto.Sku.ToLower());
        if (skuDuplicado is not null)
            return Result.Fail("Ya existe otro producto con ese SKU/código.");

        producto.Nombre = dto.Nombre.Trim();
        producto.Descripcion = dto.Descripcion;
        producto.Sku = dto.Sku.Trim();
        producto.CategoriaId = dto.CategoriaId;
        producto.ProveedorId = dto.ProveedorId;
        producto.PrecioCompra = dto.PrecioCompra;
        producto.PrecioVenta = dto.PrecioVenta;
        producto.StockMinimo = dto.StockMinimo;
        producto.Activo = dto.Activo;
        producto.FechaModificacion = DateTime.UtcNow;

        _productos.Update(producto);
        await _productos.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> DeactivateAsync(int businessId, int id)
    {
        var producto = await _productos.FirstOrDefaultAsync(p => p.Id == id && p.BusinessId == businessId);
        if (producto is null)
            return Result.Fail("Producto no encontrado.");

        producto.Activo = false;
        producto.FechaModificacion = DateTime.UtcNow;
        _productos.Update(producto);
        await _productos.SaveChangesAsync();
        return Result.Ok();
    }

    private static ProductoDto Map(Producto p) => new(
        p.Id, p.Nombre, p.Descripcion, p.Sku,
        p.CategoriaId, p.Categoria?.Nombre ?? string.Empty,
        p.ProveedorId, p.Proveedor?.Nombre,
        p.PrecioCompra, p.PrecioVenta,
        p.StockActual, p.StockMinimo, p.Activo, p.TieneStockBajo);
}
