namespace SmartStockAI.Application.DTOs;

public record ProductoDto(
    int Id,
    string Nombre,
    string? Descripcion,
    string Sku,
    int CategoriaId,
    string CategoriaNombre,
    int? ProveedorId,
    string? ProveedorNombre,
    decimal PrecioCompra,
    decimal PrecioVenta,
    int StockActual,
    int StockMinimo,
    bool Activo,
    bool TieneStockBajo);

public record ProductoCreateDto(
    string Nombre,
    string? Descripcion,
    string Sku,
    int CategoriaId,
    int? ProveedorId,
    decimal PrecioCompra,
    decimal PrecioVenta,
    int StockInicial,
    int StockMinimo);

public record ProductoUpdateDto(
    int Id,
    string Nombre,
    string? Descripcion,
    string Sku,
    int CategoriaId,
    int? ProveedorId,
    decimal PrecioCompra,
    decimal PrecioVenta,
    int StockMinimo,
    bool Activo);

public record ProductoBusquedaFiltro(string? Texto, int? CategoriaId, bool? SoloStockBajo, int Page = 1, int PageSize = 10, bool IncluirInactivos = false);
