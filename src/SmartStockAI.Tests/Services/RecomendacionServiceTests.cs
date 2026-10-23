using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Services;
using SmartStockAI.Domain.Enums;
using SmartStockAI.Tests.Support;
using Xunit;

namespace SmartStockAI.Tests.Services;

public class RecomendacionServiceTests
{
    private readonly TestWorld _w = new();
    private readonly FakeAIService _ai = new();

    private RecomendacionService Servicio => new(_w.Productos, _w.Ventas, _ai);

    private static AIRecommendation Rec(int productId, string prioridad, double confianza, string tipo = "LOW_STOCK") =>
        new(productId, tipo, prioridad, "Considerar reponer", "Stock bajo respecto al mínimo", confianza);

    // ---------- Reglas deterministas (no dependen de la IA) ----------

    [Fact]
    public async Task Alertas_PorRegla_StockAgotadoEsAltaYStockBajoEsMedia_ConConfianzaTotal()
    {
        _w.AddProducto(nombre: "Sano", sku: "1", stock: 50, stockMinimo: 5);
        _w.AddProducto(nombre: "Bajo", sku: "2", stock: 3, stockMinimo: 5);
        _w.AddProducto(nombre: "Agotado", sku: "3", stock: 0, stockMinimo: 5);
        _ai.Configured = false;

        var r = await Servicio.GenerarAsync(TestWorld.Negocio1);

        Assert.Equal(2, r.AlertasPorRegla.Count);
        var agotado = r.AlertasPorRegla[0];
        Assert.Equal("Agotado", agotado.ProductoNombre);
        Assert.Equal("LOW_STOCK", agotado.Tipo);
        Assert.Equal("HIGH", agotado.Prioridad);
        Assert.Equal("Regla", agotado.Origen);
        Assert.Equal(1.0, agotado.Confianza);
        Assert.Equal("MEDIUM", r.AlertasPorRegla[1].Prioridad);
    }

    [Fact]
    public async Task Alertas_PorRegla_SeGeneranAunqueLaIANoEsteConfigurada_YNoSeLlamaALaIA()
    {
        _w.AddProducto(nombre: "Bajo", stock: 1, stockMinimo: 5);
        _ai.Configured = false;

        var r = await Servicio.GenerarAsync(TestWorld.Negocio1);

        Assert.False(r.IaDisponible);
        Assert.Equal(0, _ai.Calls);
        Assert.Single(r.AlertasPorRegla);
        Assert.Empty(r.RecomendacionesIA);
        Assert.Contains("no está configurada", r.Mensaje);
    }

    [Fact]
    public async Task Alertas_PorRegla_SeCalculanSobreTodosLosProductosAunqueSoloSeEnvien80()
    {
        for (var i = 0; i < 100; i++)
            _w.AddProducto(nombre: $"P{i:000}", sku: $"S{i}", stock: 0, stockMinimo: 5);

        var r = await Servicio.GenerarAsync(TestWorld.Negocio1);

        Assert.Equal(100, r.AlertasPorRegla.Count);
        Assert.Equal(80, r.DatosEnviados.Products.Count);
    }

    // ---------- Datos enviados a la IA (minimización) ----------

    [Fact]
    public async Task DatosEnviados_CalculanVentasDe30DiasPrevias30YDiasSinVenta_IgnorandoAnuladas()
    {
        var p = _w.AddProducto(nombre: "Arroz", stock: 20, stockMinimo: 5);
        var ahora = DateTime.UtcNow;
        _w.AddVenta(TestWorld.Negocio1, ahora.AddDays(-10), EstadoDocumento.Completada, (p, 5, 0m));
        _w.AddVenta(TestWorld.Negocio1, ahora.AddDays(-40), EstadoDocumento.Completada, (p, 3, 0m));
        _w.AddVenta(TestWorld.Negocio1, ahora.AddDays(-5), EstadoDocumento.Anulada, (p, 99, 0m));
        _w.AddVenta(TestWorld.Negocio1, ahora.AddDays(-90), EstadoDocumento.Completada, (p, 77, 0m)); // fuera de ambas ventanas

        var r = await Servicio.GenerarAsync(TestWorld.Negocio1);

        var snap = Assert.Single(r.DatosEnviados.Products);
        Assert.Equal("Arroz", snap.Name);
        Assert.Equal(20, snap.Stock);
        Assert.Equal(5, snap.MinStock);
        Assert.Equal(5, snap.SoldLast30Days);
        Assert.Equal(3, snap.SoldPrev30Days);
        Assert.Equal(10, snap.DaysSinceLastSale);
    }

    [Fact]
    public async Task DatosEnviados_ProductoSinVentas_TieneDiasSinVentaNulo()
    {
        _w.AddProducto();

        var r = await Servicio.GenerarAsync(TestWorld.Negocio1);

        var snap = Assert.Single(r.DatosEnviados.Products);
        Assert.Null(snap.DaysSinceLastSale);
        Assert.Equal(0, snap.SoldLast30Days);
    }

    [Fact]
    public async Task DatosEnviados_NoIncluyenProductosInactivosNiDeOtroNegocio()
    {
        _w.AddProducto(nombre: "Mio", sku: "1");
        _w.AddProducto(nombre: "Inactivo", sku: "2", activo: false);
        _w.AddProducto(businessId: TestWorld.Negocio2, nombre: "Ajeno", sku: "3");

        var r = await Servicio.GenerarAsync(TestWorld.Negocio1);

        Assert.Equal("Mio", Assert.Single(r.DatosEnviados.Products).Name);
    }

    [Fact]
    public async Task DatosEnviados_ConMasDe80Productos_SeEnvianLos80MasCriticos()
    {
        for (var i = 1; i <= 100; i++)
            _w.AddProducto(nombre: $"P{i:000}", sku: $"S{i}", stock: i, stockMinimo: 0);

        var r = await Servicio.GenerarAsync(TestWorld.Negocio1);

        Assert.Equal(80, r.DatosEnviados.Products.Count);
        Assert.Contains(r.DatosEnviados.Products, p => p.Stock == 1);    // el más crítico va
        Assert.DoesNotContain(r.DatosEnviados.Products, p => p.Stock == 100); // el más holgado se omite
    }

    // ---------- Respuesta de la IA ----------

    [Fact]
    public async Task ConIA_LasRecomendacionesSeMapeanConNombreYSeOrdenanPorPrioridadYConfianza()
    {
        var a = _w.AddProducto(nombre: "A", sku: "A", stock: 1, stockMinimo: 5);
        var b = _w.AddProducto(nombre: "B", sku: "B", stock: 1, stockMinimo: 5);
        var c = _w.AddProducto(nombre: "C", sku: "C", stock: 1, stockMinimo: 5);
        _ai.Result = AIAnalysisResult.Ok(new[]
        {
            Rec(a.Id, "LOW", 0.9),
            Rec(b.Id, "HIGH", 0.5),
            Rec(c.Id, "HIGH", 0.9)
        });

        var r = await Servicio.GenerarAsync(TestWorld.Negocio1);

        Assert.True(r.IaDisponible);
        Assert.Equal(1, _ai.Calls);
        Assert.Equal(new[] { "C", "B", "A" }, r.RecomendacionesIA.Select(x => x.ProductoNombre).ToArray());
        Assert.All(r.RecomendacionesIA, x => Assert.Equal("IA", x.Origen));
        Assert.Equal("FakeAI", r.Proveedor);
        Assert.Equal("fake-model", r.Modelo);
        Assert.Null(r.Mensaje);
    }

    [Fact]
    public async Task ConIA_AlRecibirLaSolicitud_ContieneLosProductosDelNegocio()
    {
        _w.AddProducto(nombre: "Arroz");

        await Servicio.GenerarAsync(TestWorld.Negocio1);

        Assert.NotNull(_ai.LastRequest);
        Assert.Equal("Arroz", Assert.Single(_ai.LastRequest!.Products).Name);
    }

    [Fact]
    public async Task ConIA_SinRecomendaciones_InformaQueNoHuboInformacionSuficiente()
    {
        _w.AddProducto();
        _ai.Result = AIAnalysisResult.Ok(Array.Empty<AIRecommendation>());

        var r = await Servicio.GenerarAsync(TestWorld.Negocio1);

        Assert.True(r.IaDisponible);
        Assert.Empty(r.RecomendacionesIA);
        Assert.Contains("no encontró recomendaciones", r.Mensaje);
    }

    [Fact]
    public async Task SiLaIAFalla_SeConservanLasAlertasPorReglasYSeInformaElError()
    {
        _w.AddProducto(nombre: "Bajo", stock: 1, stockMinimo: 5);
        _ai.Result = AIAnalysisResult.Fail("el proveedor respondió con estado 500");

        var r = await Servicio.GenerarAsync(TestWorld.Negocio1);

        Assert.False(r.IaDisponible);
        Assert.Single(r.AlertasPorRegla);
        Assert.Empty(r.RecomendacionesIA);
        Assert.Contains("estado 500", r.Mensaje);
    }

    [Fact]
    public async Task SinProductosActivos_NoLlamaALaIAYLoInforma()
    {
        var r = await Servicio.GenerarAsync(TestWorld.Negocio1);

        Assert.Equal(0, _ai.Calls);
        Assert.Contains("No hay productos", r.Mensaje);
    }
}
