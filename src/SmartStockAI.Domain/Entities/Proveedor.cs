namespace SmartStockAI.Domain.Entities;

public class Proveedor : TenantEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string? Contacto { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }

    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
    public ICollection<Compra> Compras { get; set; } = new List<Compra>();
}
