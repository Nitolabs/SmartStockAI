using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Domain.Entities;
using SmartStockAI.Domain.Enums;
using SmartStockAI.Domain.Interfaces;

namespace SmartStockAI.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IRepository<Producto> _productos;
    private readonly IRepository<Venta> _ventas;
    private readonly IRepository<Compra> _compras;

    public DashboardService(IRepository<Producto> productos, IRepository<Venta> ventas, IRepository<Compra> compras)
    {
        _productos = productos;
        _ventas = ventas;
        _compras = compras;
    }

    public async Task<DashboardDto> GetDashboardAsync(int businessId)
    {
        var hoy = DateTime.UtcNow.Date;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);

        var productos = await _productos.FindAsync(p => p.BusinessId == businessId && p.Activo);
        var ventas = await _ventas.FindAsync(v => v.BusinessId == businessId && v.Estado == EstadoDocumento.Completada);
        var compras = await _compras.FindAsync(c => c.BusinessId == businessId && c.Estado == EstadoDocumento.Completada);

        var ventasHoy = ventas.Where(v => v.Fecha.Date == hoy).ToList();
        var ventasMes = ventas.Where(v => v.Fecha.Date >= inicioMes).ToList();
        var comprasRecientes = compras.Where(c => c.Fecha >= hoy.AddDays(-7)).ToList();

        // --- Reglas deterministas de alertas (sección 10 del prompt maestro) ---
        var alertas = new List<AlertaDto>();
        foreach (var p in productos.Where(p => p.TieneStockBajo).OrderBy(p => p.StockActual))
        {
            alertas.Add(new AlertaDto(
                Tipo: p.StockActual == 0 ? nameof(TipoAlerta.SinStock) : nameof(TipoAlerta.StockBajo),
                Prioridad: p.StockActual == 0 ? "ALTA" : "MEDIA",
                ProductoNombre: p.Nombre,
                Mensaje: p.StockActual == 0
                    ? $"'{p.Nombre}' está agotado."
                    : $"'{p.Nombre}' tiene {p.StockActual} unidades, por debajo del mínimo ({p.StockMinimo})."));
        }

        var productosMasVendidos = ventasMes
            .SelectMany(v => v.Detalles)
            .GroupBy(d => d.Producto?.Nombre ?? "Producto")
            .Select(g => new ProductoMasVendidoDto(g.Key, g.Sum(d => d.Cantidad), g.Sum(d => d.Subtotal)))
            .OrderByDescending(x => x.CantidadVendida)
            .Take(5)
            .ToList();

        var tendencia = Enumerable.Range(0, 7)
            .Select(i => hoy.AddDays(-6 + i))
            .Select(fecha => new VentaTendenciaDto(fecha, ventas.Where(v => v.Fecha.Date == fecha).Sum(v => v.Total)))
            .ToList();

        return new DashboardDto(
            VentasHoy: ventasHoy.Sum(v => v.Total),
            VentasMes: ventasMes.Sum(v => v.Total),
            ProductosVendidosHoy: ventasHoy.SelectMany(v => v.Detalles).Sum(d => d.Cantidad),
            ProductosStockBajo: productos.Count(p => p.TieneStockBajo),
            ValorInventario: productos.Sum(p => p.StockActual * p.PrecioVenta),
            ComprasRecientesCount: comprasRecientes.Count,
            ProductosMasVendidos: productosMasVendidos,
            Alertas: alertas.Take(10).ToList(),
            TendenciaVentas: tendencia,
            ComprasRecientes: comprasRecientes
                .OrderByDescending(c => c.Fecha)
                .Take(5)
                .Select(c => new CompraResumenDto(c.NumeroDocumento, c.Fecha, c.Proveedor?.Nombre ?? string.Empty, c.Total))
                .ToList());
    }
}
