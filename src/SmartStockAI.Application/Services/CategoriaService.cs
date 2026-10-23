using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Domain.Entities;
using SmartStockAI.Domain.Interfaces;

namespace SmartStockAI.Application.Services;

public class CategoriaService : ICategoriaService
{
    private readonly IRepository<Categoria> _categorias;

    public CategoriaService(IRepository<Categoria> categorias)
    {
        _categorias = categorias;
    }

    public async Task<IReadOnlyList<CategoriaDto>> GetAllAsync(int businessId)
    {
        var items = await _categorias.FindAsync(c => c.BusinessId == businessId);
        return items
            .OrderBy(c => c.Nombre)
            .Select(Map)
            .ToList();
    }

    public async Task<CategoriaDto?> GetByIdAsync(int businessId, int id)
    {
        var c = await _categorias.FirstOrDefaultAsync(x => x.Id == id && x.BusinessId == businessId);
        return c is null ? null : Map(c);
    }

    public async Task<Result<CategoriaDto>> CreateAsync(int businessId, CategoriaCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return Result<CategoriaDto>.Fail("El nombre de la categoría es obligatorio.");

        var duplicada = await _categorias.FirstOrDefaultAsync(
            c => c.BusinessId == businessId && c.Nombre.ToLower() == dto.Nombre.ToLower());
        if (duplicada is not null)
            return Result<CategoriaDto>.Fail("Ya existe una categoría con ese nombre.");

        var categoria = new Categoria
        {
            BusinessId = businessId,
            Nombre = dto.Nombre.Trim(),
            Descripcion = dto.Descripcion
        };

        await _categorias.AddAsync(categoria);
        await _categorias.SaveChangesAsync();

        return Result<CategoriaDto>.Ok(Map(categoria));
    }

    public async Task<Result> UpdateAsync(int businessId, CategoriaUpdateDto dto)
    {
        var categoria = await _categorias.FirstOrDefaultAsync(c => c.Id == dto.Id && c.BusinessId == businessId);
        if (categoria is null)
            return Result.Fail("Categoría no encontrada.");

        categoria.Nombre = dto.Nombre.Trim();
        categoria.Descripcion = dto.Descripcion;
        categoria.Activo = dto.Activo;
        categoria.FechaModificacion = DateTime.UtcNow;

        _categorias.Update(categoria);
        await _categorias.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> DeactivateAsync(int businessId, int id)
    {
        var categoria = await _categorias.FirstOrDefaultAsync(c => c.Id == id && c.BusinessId == businessId);
        if (categoria is null)
            return Result.Fail("Categoría no encontrada.");

        categoria.Activo = false;
        categoria.FechaModificacion = DateTime.UtcNow;
        _categorias.Update(categoria);
        await _categorias.SaveChangesAsync();
        return Result.Ok();
    }

    private static CategoriaDto Map(Categoria c) =>
        new(c.Id, c.Nombre, c.Descripcion, c.Activo, c.Productos?.Count ?? 0);
}
