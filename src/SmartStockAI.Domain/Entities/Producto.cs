namespace SmartStockAI.Domain.Entities;

public class Producto : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Sku { get; set; } = string.Empty;

    public int CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;

    public int? ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }

    public decimal PrecioCompra { get; set; }
    public decimal PrecioVenta { get; set; }

    public int StockActual { get; set; }
    public int StockMinimo { get; set; }

    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();
    public ICollection<VentaDetalle> VentaDetalles { get; set; } = new List<VentaDetalle>();
    public ICollection<CompraDetalle> CompraDetalles { get; set; } = new List<CompraDetalle>();

    /// <summary>
    /// Regla determinista (sección 10 del prompt maestro): nunca se calcula
    /// mediante IA si el stock está por debajo del mínimo configurado.
    /// </summary>
    public bool TieneStockBajo => StockActual <= StockMinimo;
}
