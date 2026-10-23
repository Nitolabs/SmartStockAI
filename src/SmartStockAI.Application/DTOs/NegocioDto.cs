namespace SmartStockAI.Application.DTOs;

public record NegocioDto(
    int Id, string Nombre, string? Rubro, string? Direccion,
    string? Telefono, string? Email, bool PermitirVentaSinStock);

public record NegocioUpdateDto(
    string Nombre, string? Rubro, string? Direccion,
    string? Telefono, string? Email, bool PermitirVentaSinStock);
