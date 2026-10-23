using System.Text.Json;
using SmartStockAI.Application.Common;
using SmartStockAI.Application.DTOs;

namespace SmartStockAI.Infrastructure.AI;

/// <summary>
/// Valida la respuesta del LLM. Todo lo que no cumpla el contrato se descarta:
/// IDs que no enviamos, tipos/prioridades fuera del vocabulario, razones vacías.
/// </summary>
public static class AIResponseParser
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<AIRecommendation> Parse(string content, ISet<int> validProductIds)
    {
        Wrapper? wrapper;
        try
        {
            wrapper = JsonSerializer.Deserialize<Wrapper>(StripFences(content), Options);
        }
        catch (JsonException)
        {
            return Array.Empty<AIRecommendation>();
        }

        if (wrapper?.Recommendations is null)
            return Array.Empty<AIRecommendation>();

        return wrapper.Recommendations
            .Where(r => r is not null)
            .Select(r => r with
            {
                Type = (r.Type ?? string.Empty).Trim().ToUpperInvariant(),
                Priority = (r.Priority ?? string.Empty).Trim().ToUpperInvariant(),
                Confidence = double.IsNaN(r.Confidence) ? 0 : Math.Clamp(r.Confidence, 0, 1)
            })
            .Where(r => validProductIds.Contains(r.ProductId)
                        && AIVocabulary.Types.Contains(r.Type)
                        && AIVocabulary.Priorities.Contains(r.Priority)
                        && !string.IsNullOrWhiteSpace(r.Reason)
                        && !string.IsNullOrWhiteSpace(r.Recommendation))
            .Take(10)
            .ToList();
    }

    private static string StripFences(string content)
    {
        var text = content.Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal)) return text;

        var firstNewLine = text.IndexOf('\n');
        var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
        if (firstNewLine < 0 || lastFence <= firstNewLine) return text;
        return text[(firstNewLine + 1)..lastFence].Trim();
    }

    internal sealed record Wrapper(List<AIRecommendation>? Recommendations);
}
