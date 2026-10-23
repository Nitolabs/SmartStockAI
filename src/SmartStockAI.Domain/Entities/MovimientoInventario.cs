using SmartStockAI.Domain.Enums;

namespace SmartStockAI.Domain.Entities;

/// <summary>
/// Registro inmutable de cada entrada, salida o ajuste de stock.
/// El stock de un producto NUNCA debe modificarse sin generar este registro
/// (sección 6.6 del prompt maestro).
/// </summary>
public class MovimientoInventario : TenantEntity
{
    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    public TipoMovimientoInventario Tipo { get; set; }
    public MotivoMovimiento Motivo { get; set; }

    public int Cantidad { get; set; }
    public int StockResultante { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public string UsuarioId { get; set; } = string.Empty;
    public string? UsuarioNombre { get; set; }

    public string? Observaciones { get; set; }

    public int? VentaId { get; set; }
    public Venta? Venta { get; set; }

    public int? CompraId { get; set; }
    public Compra? Compra { get; set; }
}
