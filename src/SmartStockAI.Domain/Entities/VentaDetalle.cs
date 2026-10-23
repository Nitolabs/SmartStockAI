namespace SmartStockAI.Domain.Entities;

public class VentaDetalle : BaseEntity
{
    public int VentaId { get; set; }
    public Venta Venta { get; set; } = null!;

    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Descuento { get; set; }
    public decimal Subtotal => (Cantidad * PrecioUnitario) - Descuento;
}
