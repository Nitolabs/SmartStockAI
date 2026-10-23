namespace SmartStockAI.Application.Common;

/// <summary>
/// Vocabulario permitido para las recomendaciones. Cualquier valor fuera de estas listas
/// se descarta: no se confía ciegamente en el texto generado por el LLM (sección 9 del prompt maestro).
/// </summary>
public static class AIVocabulary
{
    public static readonly string[] Types =
    {
        "LOW_STOCK", "HIGH_DEMAND", "LOW_ROTATION",
        "PURCHASE_RECOMMENDATION", "SALES_TREND", "GENERAL_ALERT"
    };

    public static readonly string[] Priorities = { "HIGH", "MEDIUM", "LOW" };
}
