namespace SmartStockAI.Application.DTOs;

public record ProveedorDto(int Id, string Nombre, string? Contacto, string? Telefono, string? Email, string? Direccion, bool Activo);

public record ProveedorCreateDto(string Nombre, string? Contacto, string? Telefono, string? Email, string? Direccion);

public record ProveedorUpdateDto(int Id, string Nombre, string? Contacto, string? Telefono, string? Email, string? Direccion, bool Activo);
