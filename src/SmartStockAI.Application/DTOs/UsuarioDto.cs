namespace SmartStockAI.Application.DTOs;

public record UsuarioDto(string Id, string NombreCompleto, string Email, string Rol, bool Activo);

public record UsuarioCreateDto(string NombreCompleto, string Email, string Contrasena, string Rol);

public record UsuarioUpdateDto(string Id, string NombreCompleto, string Rol, bool Activo);
