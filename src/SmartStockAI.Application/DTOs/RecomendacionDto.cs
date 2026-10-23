namespace SmartStockAI.Application.DTOs;

/// <summary>Métricas por producto calculadas por el backend. Es lo ÚNICO que se envía a la IA.</summary>
public record ProductSnapshot(
    int ProductId,
    string Name,
    string Category,
    int Stock,
    int MinStock,
    int SoldLast30Days,
    int SoldPrev30Days,
    int? DaysSinceLastSale);

public record InventoryAnalysisRequest(DateOnly Date, IReadOnlyList<ProductSnapshot> Products);

public record AIRecommendation(
    int ProductId,
    string Type,
    string Priority,
    string Recommendation,
    string Reason,
    double Confidence);

public record AIAnalysisResult(bool Success, IReadOnlyList<AIRecommendation> Recommendations, string? Error = null)
{
    public static AIAnalysisResult Ok(IReadOnlyList<AIRecommendation> recommendations) => new(true, recommendations);
    public static AIAnalysisResult Fail(string error) => new(false, Array.Empty<AIRecommendation>(), error);
}

/// <summary>Elemento listo para mostrar en pantalla. Origen: "Regla" (determinista) o "IA".</summary>
public record RecomendacionItemDto(
    int ProductoId,
    string ProductoNombre,
    string Tipo,
    string Prioridad,
    string Recomendacion,
    string Razon,
    double Confianza,
    string Origen);

/// <summary>
/// Resultado completo de un análisis. Incluye los datos enviados y el procesamiento para
/// poder evidenciar entrada → procesamiento → resultado (requisito del PDF de la Fase II).
/// </summary>
public record RecomendacionesResultadoDto(
    InventoryAnalysisRequest DatosEnviados,
    IReadOnlyList<RecomendacionItemDto> AlertasPorRegla,
    IReadOnlyList<RecomendacionItemDto> RecomendacionesIA,
    bool IaDisponible,
    string Proveedor,
    string? Modelo,
    long DuracionMs,
    string? Mensaje);
