using System.Diagnostics;
using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;
using SmartStockAI.Domain.Entities;
using SmartStockAI.Domain.Enums;
using SmartStockAI.Domain.Interfaces;

namespace SmartStockAI.Application.Services;

/// <summary>
/// Orquesta el análisis: el backend calcula las métricas y las alertas críticas con reglas
/// deterministas; la IA solo interpreta esas métricas y sugiere acciones.
/// </summary>
public class RecomendacionService : IRecomendacionService
{
    private const int MaxProductosEnviados = 80;
    private static readonly string[] OrdenPrioridad = { "HIGH", "MEDIUM", "LOW" };

    private readonly IRepository<Producto> _productos;
    private readonly IRepository<Venta> _ventas;
    private readonly IAIService _ai;

    public RecomendacionService(IRepository<Producto> productos, IRepository<Venta> ventas, IAIService ai)
    {
        _productos = productos;
        _ventas = ventas;
        _ai = ai;
    }

    public async Task<RecomendacionesResultadoDto> GenerarAsync(int businessId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var todos = await ConstruirSnapshotsAsync(businessId);

        // Reglas deterministas: se calculan siempre, con o sin IA, sobre TODOS los productos.
        var alertas = todos
            .Where(s => s.Stock <= s.MinStock)
            .OrderBy(s => s.Stock)
            .Select(AlertaPorRegla)
            .ToList();

        // Minimización de datos: solo los productos más "sensibles" (menor margen sobre el mínimo).
        var enviados = todos.OrderBy(s => s.Stock - s.MinStock).Take(MaxProductosEnviados).ToList();
        var request = new InventoryAnalysisRequest(DateOnly.FromDateTime(DateTime.UtcNow), enviados);

        IReadOnlyList<RecomendacionItemDto> recomendacionesIa = Array.Empty<RecomendacionItemDto>();
        var iaOk = false;
        string? mensaje = null;

        if (enviados.Count == 0)
        {
            mensaje = "No hay productos activos para analizar.";
        }
        else if (!_ai.IsConfigured)
        {
            mensaje = "La IA no está configurada (faltan AI:ApiKey / AI:Model). Se muestran solo las alertas por reglas.";
        }
        else
        {
            var resultado = await _ai.AnalyzeInventoryAsync(request, ct);
            if (resultado.Success)
            {
                iaOk = true;
                var nombres = enviados.ToDictionary(p => p.ProductId, p => p.Name);
                recomendacionesIa = resultado.Recommendations
                    .OrderBy(r => Array.IndexOf(OrdenPrioridad, r.Priority))
                    .ThenByDescending(r => r.Confidence)
                    .Select(r => new RecomendacionItemDto(
                        r.ProductId, nombres.GetValueOrDefault(r.ProductId, "Producto"),
                        r.Type, r.Priority, r.Recommendation, r.Reason, r.Confidence, "IA"))
                    .ToList();

                if (recomendacionesIa.Count == 0)
                    mensaje = "La IA no encontró recomendaciones con información suficiente.";
            }
            else
            {
                mensaje = $"No se pudo obtener respuesta de la IA ({resultado.Error}). Se muestran solo las alertas por reglas.";
            }
        }

        sw.Stop();
        return new RecomendacionesResultadoDto(
            request, alertas, recomendacionesIa, iaOk,
            _ai.ProviderName, _ai.ModelName, sw.ElapsedMilliseconds, mensaje);
    }

    private async Task<List<ProductSnapshot>> ConstruirSnapshotsAsync(int businessId)
    {
        var productos = await _productos.FindAsync(p => p.BusinessId == businessId && p.Activo);
        var ventas = await _ventas.FindAsync(v => v.BusinessId == businessId && v.Estado == EstadoDocumento.Completada);

        var ahora = DateTime.UtcNow;
        var hace30 = ahora.AddDays(-30);
        var hace60 = ahora.AddDays(-60);

        var lineasPorProducto = ventas
            .SelectMany(v => v.Detalles.Select(d => new { v.Fecha, d.ProductoId, d.Cantidad }))
            .ToLookup(x => x.ProductoId);

        return productos.Select(p =>
        {
            var lineas = lineasPorProducto[p.Id].ToList();
            DateTime? ultima = lineas.Count == 0 ? null : lineas.Max(x => x.Fecha);

            return new ProductSnapshot(
                p.Id,
                p.Nombre,
                p.Categoria?.Nombre ?? string.Empty,
                p.StockActual,
                p.StockMinimo,
                lineas.Where(x => x.Fecha >= hace30).Sum(x => x.Cantidad),
                lineas.Where(x => x.Fecha < hace30 && x.Fecha >= hace60).Sum(x => x.Cantidad),
                ultima is null ? null : (int)(ahora - ultima.Value).TotalDays);
        }).ToList();
    }

    private static RecomendacionItemDto AlertaPorRegla(ProductSnapshot s)
    {
        var agotado = s.Stock == 0;
        return new RecomendacionItemDto(
            s.ProductId, s.Name, "LOW_STOCK",
            agotado ? "HIGH" : "MEDIUM",
            agotado
                ? "Producto agotado: considerar reponer lo antes posible."
                : "Stock en o por debajo del mínimo: considerar reponer.",
            $"Stock actual {s.Stock}, mínimo configurado {s.MinStock}.",
            1.0, "Regla");
    }
}
