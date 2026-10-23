using SmartStockAI.Application.Services;
using SmartStockAI.Domain.Entities;
using SmartStockAI.Domain.Enums;

namespace SmartStockAI.Tests.Support;

/// <summary>
/// Escenario de pruebas: repositorios en memoria compartidos entre servicios (como un DbContext por request)
/// y constructores de datos de prueba.
/// </summary>
public class TestWorld
{
    public const int Negocio1 = 1;
    public const int Negocio2 = 2;

    public FakeRepository<Producto> Productos { get; } = new();
    public FakeRepository<Venta> Ventas { get; } = new();
    public FakeRepository<Compra> Compras { get; } = new();
    public FakeRepository<Proveedor> Proveedores { get; } = new();
    public FakeRepository<Categoria> Categorias { get; } = new();
    public FakeRepository<MovimientoInventario> Movimientos { get; } = new();
    public FakeRepository<Business> Negocios { get; } = new();
    public FakeUnitOfWork UnitOfWork { get; } = new();

    public ProductoService ProductoService => new(Productos, Movimientos, UnitOfWork);
    public CategoriaService CategoriaService => new(Categorias);
    public ProveedorService ProveedorService => new(Proveedores);
    public InventarioService InventarioService => new(Productos, Movimientos, UnitOfWork);
    public VentaService VentaService => new(Ventas, Productos, Movimientos, Negocios, UnitOfWork);
    public CompraService CompraService => new(Compras, Productos, Proveedores, Movimientos, UnitOfWork);
    public DashboardService DashboardService => new(Productos, Ventas, Compras);
    public ReporteService ReporteService => new(Ventas, Compras, Productos, Movimientos);
    public NegocioService NegocioService => new(Negocios);

    public Business AddNegocio(int id, string nombre = "Negocio", bool permitirVentaSinStock = false)
    {
        return Negocios.Add(new Business { Id = id, Nombre = nombre, PermitirVentaSinStock = permitirVentaSinStock });
    }

    public Categoria AddCategoria(int businessId = Negocio1, string nombre = "Abarrotes") =>
        Categorias.Add(new Categoria { BusinessId = businessId, Nombre = nombre });

    public Proveedor AddProveedor(int businessId = Negocio1, string nombre = "Distribuidora X") =>
        Proveedores.Add(new Proveedor { BusinessId = businessId, Nombre = nombre });

    public Producto AddProducto(
        int businessId = Negocio1,
        string nombre = "Arroz 1lb",
        string sku = "SKU-1",
        int stock = 10,
        int stockMinimo = 5,
        decimal precioVenta = 10m,
        decimal precioCompra = 6m,
        bool activo = true,
        Categoria? categoria = null,
        Proveedor? proveedor = null)
    {
        return Productos.Add(new Producto
        {
            BusinessId = businessId,
            Nombre = nombre,
            Sku = sku,
            StockActual = stock,
            StockMinimo = stockMinimo,
            PrecioVenta = precioVenta,
            PrecioCompra = precioCompra,
            Activo = activo,
            CategoriaId = categoria?.Id ?? 0,
            Categoria = categoria!,
            ProveedorId = proveedor?.Id,
            Proveedor = proveedor
        });
    }

    /// <summary>Inserta una venta directamente (sin pasar por el servicio) para armar escenarios de reportes.</summary>
    public Venta AddVenta(
        int businessId,
        DateTime fecha,
        EstadoDocumento estado,
        params (Producto Producto, int Cantidad, decimal Descuento)[] lineas)
    {
        var venta = new Venta
        {
            BusinessId = businessId,
            NumeroDocumento = $"V-TEST-{Ventas.Items.Count + 1}",
            Fecha = fecha,
            Estado = estado
        };

        foreach (var (producto, cantidad, descuento) in lineas)
        {
            venta.Detalles.Add(new VentaDetalle
            {
                ProductoId = producto.Id,
                Producto = producto,
                Cantidad = cantidad,
                PrecioUnitario = producto.PrecioVenta,
                Descuento = descuento
            });
            venta.Subtotal += cantidad * producto.PrecioVenta;
            venta.Descuento += descuento;
        }
        venta.Total = venta.Subtotal - venta.Descuento;

        return Ventas.Add(venta);
    }

    public Compra AddCompra(
        int businessId,
        DateTime fecha,
        EstadoDocumento estado,
        Proveedor proveedor,
        params (Producto Producto, int Cantidad, decimal Costo)[] lineas)
    {
        var compra = new Compra
        {
            BusinessId = businessId,
            NumeroDocumento = $"C-TEST-{Compras.Items.Count + 1}",
            Fecha = fecha,
            Estado = estado,
            ProveedorId = proveedor.Id,
            Proveedor = proveedor
        };

        foreach (var (producto, cantidad, costo) in lineas)
        {
            compra.Detalles.Add(new CompraDetalle
            {
                ProductoId = producto.Id,
                Producto = producto,
                Cantidad = cantidad,
                CostoUnitario = costo
            });
            compra.Total += cantidad * costo;
        }

        return Compras.Add(compra);
    }
}
