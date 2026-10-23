using SmartStockAI.Application.DTOs;

namespace SmartStockAI.Application.Interfaces;

/// <summary>
/// Abstracción del proveedor de IA. La lógica de negocio solo conoce esta interfaz;
/// cambiar de OpenAI a Azure OpenAI es un cambio de configuración, no de código.
/// </summary>
public interface IAIService
{
    string ProviderName { get; }
    string? ModelName { get; }
    bool IsConfigured { get; }

    Task<AIAnalysisResult> AnalyzeInventoryAsync(InventoryAnalysisRequest request, CancellationToken ct = default);
}
