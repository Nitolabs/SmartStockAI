namespace SmartStockAI.Application.DTOs;

public record CategoriaDto(int Id, string Nombre, string? Descripcion, bool Activo, int TotalProductos);

public record CategoriaCreateDto(string Nombre, string? Descripcion);

public record CategoriaUpdateDto(int Id, string Nombre, string? Descripcion, bool Activo);
