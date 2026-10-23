using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Infrastructure.AI;
using Xunit;

namespace SmartStockAI.Tests.AI;

/// <summary>Pruebas del cliente de IA sin llamar a internet: se reemplaza el HttpMessageHandler.</summary>
public class ChatCompletionsAIServiceTests
{
    private const string RespuestaValida =
        """{"recommendations":[{"productId":1,"type":"LOW_STOCK","priority":"HIGH","recommendation":"Considerar reponer","reason":"Stock 3 vs mínimo 10","confidence":0.9}]}""";

    private static readonly InventoryAnalysisRequest Solicitud = new(
        new DateOnly(2026, 10, 12),
        new[] { new ProductSnapshot(1, "Arroz", "Abarrotes", 3, 10, 8, 4, 2) });

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        public int Calls { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return _responder(request);
        }
    }

    private static HttpResponseMessage Ok(string contenidoDelModelo) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = contenidoDelModelo } } } }),
                Encoding.UTF8, "application/json")
        };

    private static ChatCompletionsAIService Crear(StubHandler handler, AIOptions options, string baseAddress) =>
        new(new HttpClient(handler) { BaseAddress = new Uri(baseAddress) },
            Options.Create(options),
            NullLogger<ChatCompletionsAIService>.Instance);

    private static AIOptions OpenAI() => new() { Provider = "OpenAI", Model = "modelo-prueba", ApiKey = "clave" };

    private static AIOptions Azure() => new()
    {
        Provider = "AzureOpenAI", Model = "mi-deployment", ApiKey = "clave",
        Endpoint = "https://recurso.openai.azure.com", ApiVersion = "2099-01-01"
    };

    // ---------- Configuración ----------

    [Fact]
    public async Task SinConfiguracion_NoLlamaAInternetYDevuelveFallo()
    {
        var handler = new StubHandler(_ => Ok(RespuestaValida));
        var servicio = Crear(handler, new AIOptions(), "https://api.test/v1/");

        var r = await servicio.AnalyzeInventoryAsync(Solicitud);

        Assert.False(servicio.IsConfigured);
        Assert.False(r.Success);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("OpenAI", "", "clave", "", "", false)]                       // falta modelo
    [InlineData("OpenAI", "m", "", "", "", false)]                           // falta clave
    [InlineData("OpenAI", "m", "k", "", "", true)]                           // OpenAI completo
    [InlineData("AzureOpenAI", "d", "k", "", "", false)]                     // Azure sin endpoint
    [InlineData("AzureOpenAI", "d", "k", "https://x", "", false)]            // Azure sin api-version
    [InlineData("AzureOpenAI", "d", "k", "https://x", "2099-01-01", true)]   // Azure completo
    public void IsConfigured_DependeDelProveedor(string proveedor, string modelo, string clave, string endpoint, string version, bool esperado)
    {
        var o = new AIOptions { Provider = proveedor, Model = modelo, ApiKey = clave, Endpoint = endpoint, ApiVersion = version };

        Assert.Equal(esperado, o.IsConfigured);
    }

    // ---------- Formato de las peticiones ----------

    [Fact]
    public async Task OpenAI_EnviaModeloEnElCuerpoYNoUsaApiVersion()
    {
        var handler = new StubHandler(_ => Ok(RespuestaValida));
        var servicio = Crear(handler, OpenAI(), "https://api.test/v1/");

        var r = await servicio.AnalyzeInventoryAsync(Solicitud);

        Assert.True(r.Success);
        Assert.Equal("OpenAI", servicio.ProviderName);
        Assert.Equal("/v1/chat/completions", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.DoesNotContain("api-version", handler.LastRequest.RequestUri.Query);

        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("modelo-prueba", body.RootElement.GetProperty("model").GetString());
        Assert.Equal("json_object", body.RootElement.GetProperty("response_format").GetProperty("type").GetString());
    }

    [Fact]
    public async Task Azure_UsaApiVersionEnLaUrlYNoEnviaModeloEnElCuerpo()
    {
        var handler = new StubHandler(_ => Ok(RespuestaValida));
        var servicio = Crear(handler, Azure(), "https://recurso.openai.azure.com/openai/deployments/mi-deployment/");

        var r = await servicio.AnalyzeInventoryAsync(Solicitud);

        Assert.True(r.Success);
        Assert.Equal("Azure OpenAI", servicio.ProviderName);
        Assert.Contains("api-version=2099-01-01", handler.LastRequest!.RequestUri!.Query);

        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.False(body.RootElement.TryGetProperty("model", out _));
    }

    [Fact]
    public async Task ElMensajeDelSistemaIncluyeLasReglasAntiInvencion()
    {
        var handler = new StubHandler(_ => Ok(RespuestaValida));
        var servicio = Crear(handler, OpenAI(), "https://api.test/v1/");

        await servicio.AnalyzeInventoryAsync(Solicitud);

        using var body = JsonDocument.Parse(handler.LastBody!);
        var mensajes = body.RootElement.GetProperty("messages");
        Assert.Equal("system", mensajes[0].GetProperty("role").GetString());
        var prompt = mensajes[0].GetProperty("content").GetString()!;
        Assert.Contains("No inventes", prompt);
        Assert.Contains("JSON", prompt);
    }

    [Fact]
    public async Task MinimizacionDeDatos_ElProveedorSoloRecibeLasMetricasPermitidas()
    {
        var handler = new StubHandler(_ => Ok(RespuestaValida));
        var servicio = Crear(handler, OpenAI(), "https://api.test/v1/");

        await servicio.AnalyzeInventoryAsync(Solicitud);

        using var body = JsonDocument.Parse(handler.LastBody!);
        var contenidoUsuario = body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;
        using var datos = JsonDocument.Parse(contenidoUsuario);

        var propiedades = datos.RootElement.GetProperty("products")[0].EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        var permitidas = new[] { "productId", "name", "category", "stock", "minStock", "soldLast30Days", "soldPrev30Days", "daysSinceLastSale" }
            .OrderBy(n => n).ToArray();

        Assert.Equal(permitidas, propiedades);
    }

    // ---------- Respuestas ----------

    [Fact]
    public async Task RespuestaValida_SeConvierteEnRecomendaciones()
    {
        var servicio = Crear(new StubHandler(_ => Ok(RespuestaValida)), OpenAI(), "https://api.test/v1/");

        var r = await servicio.AnalyzeInventoryAsync(Solicitud);

        var rec = Assert.Single(r.Recommendations);
        Assert.Equal(1, rec.ProductId);
        Assert.Equal("LOW_STOCK", rec.Type);
        Assert.Equal(0.9, rec.Confidence);
    }

    [Fact]
    public async Task RespuestaConProductoQueNoEnviamos_SeDescarta()
    {
        const string inventada =
            """{"recommendations":[{"productId":999,"type":"LOW_STOCK","priority":"HIGH","recommendation":"x","reason":"y","confidence":0.9}]}""";
        var servicio = Crear(new StubHandler(_ => Ok(inventada)), OpenAI(), "https://api.test/v1/");

        var r = await servicio.AnalyzeInventoryAsync(Solicitud);

        Assert.True(r.Success);
        Assert.Empty(r.Recommendations);
    }

    [Fact]
    public async Task ContenidoQueNoEsJson_NoLanzaExcepcionYDevuelveVacio()
    {
        var servicio = Crear(new StubHandler(_ => Ok("lo siento, no puedo")), OpenAI(), "https://api.test/v1/");

        var r = await servicio.AnalyzeInventoryAsync(Solicitud);

        Assert.True(r.Success);
        Assert.Empty(r.Recommendations);
    }

    // ---------- Errores ----------

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, 401)]
    [InlineData(HttpStatusCode.TooManyRequests, 429)]
    [InlineData(HttpStatusCode.InternalServerError, 500)]
    public async Task ErrorHttp_DevuelveFalloControladoSinExponerElDetalle(HttpStatusCode codigo, int esperado)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(codigo) { Content = new StringContent("detalle interno secreto") });
        var servicio = Crear(handler, OpenAI(), "https://api.test/v1/");

        var r = await servicio.AnalyzeInventoryAsync(Solicitud);

        Assert.False(r.Success);
        Assert.Contains(esperado.ToString(), r.Error);
        Assert.DoesNotContain("secreto", r.Error);
    }

    [Fact]
    public async Task RespuestaSinChoices_DevuelveFalloControlado()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"otra":"cosa"}""") });
        var servicio = Crear(handler, OpenAI(), "https://api.test/v1/");

        var r = await servicio.AnalyzeInventoryAsync(Solicitud);

        Assert.False(r.Success);
    }

    [Fact]
    public async Task ContenidoVacio_DevuelveFallo()
    {
        var servicio = Crear(new StubHandler(_ => Ok("")), OpenAI(), "https://api.test/v1/");

        var r = await servicio.AnalyzeInventoryAsync(Solicitud);

        Assert.False(r.Success);
    }

    [Fact]
    public async Task FalloDeRed_DevuelveFalloGenericoSinPropagarLaExcepcion()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("no hay conexión con 10.0.0.5"));
        var servicio = Crear(handler, OpenAI(), "https://api.test/v1/");

        var r = await servicio.AnalyzeInventoryAsync(Solicitud);

        Assert.False(r.Success);
        Assert.DoesNotContain("10.0.0.5", r.Error);
    }

    [Fact]
    public async Task CancelacionDelUsuario_SePropagaYNoSeTrataComoFalloDeLaIA()
    {
        var handler = new StubHandler(_ => Ok(RespuestaValida));
        var servicio = Crear(handler, OpenAI(), "https://api.test/v1/");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => servicio.AnalyzeInventoryAsync(Solicitud, cts.Token));
    }
}
