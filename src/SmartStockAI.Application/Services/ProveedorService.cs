using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Domain.Entities;
using SmartStockAI.Domain.Interfaces;

namespace SmartStockAI.Application.Services;

public class ProveedorService : IProveedorService
{
    private readonly IRepository<Proveedor> _proveedores;

    public ProveedorService(IRepository<Proveedor> proveedores)
    {
        _proveedores = proveedores;
    }

    public async Task<IReadOnlyList<ProveedorDto>> GetAllAsync(int businessId)
    {
        var items = await _proveedores.FindAsync(p => p.BusinessId == businessId);
        return items.OrderBy(p => p.Nombre).Select(Map).ToList();
    }

    public async Task<ProveedorDto?> GetByIdAsync(int businessId, int id)
    {
        var p = await _proveedores.FirstOrDefaultAsync(x => x.Id == id && x.BusinessId == businessId);
        return p is null ? null : Map(p);
    }

    public async Task<Result<ProveedorDto>> CreateAsync(int businessId, ProveedorCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return Result<ProveedorDto>.Fail("El nombre del proveedor es obligatorio.");

        var proveedor = new Proveedor
        {
            BusinessId = businessId,
            Nombre = dto.Nombre.Trim(),
            Contacto = dto.Contacto,
            Telefono = dto.Telefono,
            Email = dto.Email,
            Direccion = dto.Direccion
        };

        await _proveedores.AddAsync(proveedor);
        await _proveedores.SaveChangesAsync();
        return Result<ProveedorDto>.Ok(Map(proveedor));
    }

    public async Task<Result> UpdateAsync(int businessId, ProveedorUpdateDto dto)
    {
        var proveedor = await _proveedores.FirstOrDefaultAsync(p => p.Id == dto.Id && p.BusinessId == businessId);
        if (proveedor is null)
            return Result.Fail("Proveedor no encontrado.");

        proveedor.Nombre = dto.Nombre.Trim();
        proveedor.Contacto = dto.Contacto;
        proveedor.Telefono = dto.Telefono;
        proveedor.Email = dto.Email;
        proveedor.Direccion = dto.Direccion;
        proveedor.Activo = dto.Activo;
        proveedor.FechaModificacion = DateTime.UtcNow;

        _proveedores.Update(proveedor);
        await _proveedores.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> DeactivateAsync(int businessId, int id)
    {
        var proveedor = await _proveedores.FirstOrDefaultAsync(p => p.Id == id && p.BusinessId == businessId);
        if (proveedor is null)
            return Result.Fail("Proveedor no encontrado.");

        proveedor.Activo = false;
        proveedor.FechaModificacion = DateTime.UtcNow;
        _proveedores.Update(proveedor);
        await _proveedores.SaveChangesAsync();
        return Result.Ok();
    }

    private static ProveedorDto Map(Proveedor p) =>
        new(p.Id, p.Nombre, p.Contacto, p.Telefono, p.Email, p.Direccion, p.Activo);
}
