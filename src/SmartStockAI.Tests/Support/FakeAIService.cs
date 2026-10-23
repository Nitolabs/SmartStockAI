using SmartStockAI.Application.DTOs;
using SmartStockAI.Application.Interfaces;

namespace SmartStockAI.Tests.Support;

/// <summary>IAIService de prueba: permite simular IA configurada, caída o con respuestas concretas.</summary>
public class FakeAIService : IAIService
{
    public bool Configured { get; set; } = true;
    public AIAnalysisResult Result { get; set; } = AIAnalysisResult.Ok(Array.Empty<AIRecommendation>());

    public int Calls { get; private set; }
    public InventoryAnalysisRequest? LastRequest { get; private set; }

    public string ProviderName => "FakeAI";
    public string? ModelName => "fake-model";
    public bool IsConfigured => Configured;

    public Task<AIAnalysisResult> AnalyzeInventoryAsync(InventoryAnalysisRequest request, CancellationToken ct = default)
    {
        Calls++;
        LastRequest = request;
        return Task.FromResult(Result);
    }
}
