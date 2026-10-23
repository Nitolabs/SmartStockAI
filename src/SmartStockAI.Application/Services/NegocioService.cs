using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Domain.Entities;
using SmartStockAI.Domain.Interfaces;

namespace SmartStockAI.Application.Services;

public class NegocioService : INegocioService
{
    private readonly IRepository<Business> _negocios;

    public NegocioService(IRepository<Business> negocios)
    {
        _negocios = negocios;
    }

    public async Task<NegocioDto?> ObtenerAsync(int businessId)
    {
        var b = await _negocios.GetByIdAsync(businessId);
        return b is null ? null : Map(b);
    }

    public async Task<Result> ActualizarAsync(int businessId, NegocioUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return Result.Fail("El nombre del negocio es obligatorio.");

        var b = await _negocios.GetByIdAsync(businessId);
        if (b is null)
            return Result.Fail("Negocio no encontrado.");

        b.Nombre = dto.Nombre.Trim();
        b.Rubro = dto.Rubro?.Trim();
        b.Direccion = dto.Direccion?.Trim();
        b.Telefono = dto.Telefono?.Trim();
        b.Email = dto.Email?.Trim();
        b.PermitirVentaSinStock = dto.PermitirVentaSinStock;
        b.FechaModificacion = DateTime.UtcNow;

        _negocios.Update(b);
        await _negocios.SaveChangesAsync();
        return Result.Ok();
    }

    private static NegocioDto Map(Business b) =>
        new(b.Id, b.Nombre, b.Rubro, b.Direccion, b.Telefono, b.Email, b.PermitirVentaSinStock);
}
