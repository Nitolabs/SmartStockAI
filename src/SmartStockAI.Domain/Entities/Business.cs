namespace SmartStockAI.Domain.Entities;

/// <summary>
/// Representa a la MYPE (tenant) dueña de los datos. Todo usuario,
/// producto, venta, compra e inventario pertenece a un único Business.
/// </summary>
public class Business : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string? Rubro { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }

    /// <summary>
    /// Configuración explícita (sección 6.8 del prompt maestro): si es true, se permite vender
    /// aunque el stock no alcance. Por defecto está desactivada.
    /// </summary>
    public bool PermitirVentaSinStock { get; set; }

    public ICollection<Categoria> Categorias { get; set; } = new List<Categoria>();
    public ICollection<Proveedor> Proveedores { get; set; } = new List<Proveedor>();
    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
    public ICollection<Compra> Compras { get; set; } = new List<Compra>();
    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();
}
