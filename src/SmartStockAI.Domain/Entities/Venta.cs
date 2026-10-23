using SmartStockAI.Domain.Enums;

namespace SmartStockAI.Domain.Entities;

public class Venta : TenantEntity
{
    public string NumeroDocumento { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public string UsuarioId { get; set; } = string.Empty;
    public string? UsuarioNombre { get; set; }

    public decimal Subtotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal Total { get; set; }

    public MetodoPago MetodoPago { get; set; } = MetodoPago.Efectivo;
    public EstadoDocumento Estado { get; set; } = EstadoDocumento.Completada;

    public ICollection<VentaDetalle> Detalles { get; set; } = new List<VentaDetalle>();
    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();
}
