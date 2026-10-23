using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;

namespace SmartStockAI.Infrastructure.AI;

/// <summary>
/// Implementación de IAIService sobre el endpoint "chat/completions", común a OpenAI y Azure OpenAI.
/// Solo cambian la URL base, el header de autenticación y la forma de indicar el modelo;
/// eso se resuelve al registrar el HttpClient en Program.cs.
/// </summary>
public class ChatCompletionsAIService : IAIService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly AIOptions _options;
    private readonly ILogger<ChatCompletionsAIService> _logger;

    public ChatCompletionsAIService(
        HttpClient http,
        IOptions<AIOptions> options,
        ILogger<ChatCompletionsAIService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => _options.IsAzure ? "Azure OpenAI" : "OpenAI";
    public string? ModelName => string.IsNullOrWhiteSpace(_options.Model) ? null : _options.Model;
    public bool IsConfigured => _options.IsConfigured;

    public async Task<AIAnalysisResult> AnalyzeInventoryAsync(InventoryAnalysisRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return AIAnalysisResult.Fail("la IA no está configurada");

        var url = _options.IsAzure
            ? $"chat/completions?api-version={Uri.EscapeDataString(_options.ApiVersion)}"
            : "chat/completions";

        var body = new Dictionary<string, object>
        {
            ["response_format"] = new { type = "json_object" },
            ["messages"] = new object[]
            {
                new { role = "system", content = InventoryPrompts.System },
                new { role = "user", content = JsonSerializer.Serialize(request, Json) }
            }
        };
        // En Azure el modelo va en la URL (deployment); en OpenAI va en el cuerpo.
        if (!_options.IsAzure)
            body["model"] = _options.Model;

        try
        {
            using var response = await _http.PostAsJsonAsync(url, body, ct);

            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("El proveedor de IA respondió {Status}: {Detail}",
                    (int)response.StatusCode, detail.Length > 500 ? detail[..500] : detail);
                return AIAnalysisResult.Fail($"el proveedor respondió con estado {(int)response.StatusCode}");
            }

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
                return AIAnalysisResult.Fail("respuesta vacía del proveedor");

            var validIds = request.Products.Select(p => p.ProductId).ToHashSet();
            return AIAnalysisResult.Ok(AIResponseParser.Parse(content, validIds));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested &&
            ex is HttpRequestException or TaskCanceledException or JsonException
               or KeyNotFoundException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            // El detalle técnico va al log; al usuario solo un mensaje genérico (sección 19).
            _logger.LogError(ex, "Falló la llamada al proveedor de IA");
            return AIAnalysisResult.Fail("error de comunicación con el proveedor");
        }
    }
}
