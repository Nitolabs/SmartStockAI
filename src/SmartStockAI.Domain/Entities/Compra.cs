using SmartStockAI.Domain.Enums;

namespace SmartStockAI.Domain.Entities;

public class Compra : TenantEntity
{
    public string NumeroDocumento { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public int ProveedorId { get; set; }
    public Proveedor Proveedor { get; set; } = null!;

    public string UsuarioId { get; set; } = string.Empty;
    public string? UsuarioNombre { get; set; }

    public decimal Total { get; set; }
    public EstadoDocumento Estado { get; set; } = EstadoDocumento.Completada;

    public ICollection<CompraDetalle> Detalles { get; set; } = new List<CompraDetalle>();
    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();
}
