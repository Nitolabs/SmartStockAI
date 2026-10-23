using SmartStockAI.Infrastructure.AI;
using Xunit;

namespace SmartStockAI.Tests;

public class AIResponseParserTests
{
    private static readonly HashSet<int> Ids = new() { 1, 2 };

    private static string Json(int productId = 1, string type = "LOW_STOCK", string priority = "HIGH",
                               string reason = "Stock 3, mínimo 10", double confidence = 0.8)
    {
        var conf = confidence.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return "{\"recommendations\":[{\"productId\":" + productId
            + ",\"type\":\"" + type + "\",\"priority\":\"" + priority
            + "\",\"recommendation\":\"Considerar reponer\",\"reason\":\"" + reason
            + "\",\"confidence\":" + conf + "}]}";
    }

    [Fact]
    public void Parse_AceptaRecomendacionValida()
    {
        var result = AIResponseParser.Parse(Json(), Ids);
        var r = Assert.Single(result);
        Assert.Equal(1, r.ProductId);
        Assert.Equal("LOW_STOCK", r.Type);
    }

    [Fact]
    public void Parse_DescartaProductIdQueNoEnviamos()
        => Assert.Empty(AIResponseParser.Parse(Json(productId: 999), Ids));

    [Fact]
    public void Parse_DescartaTipoFueraDelVocabulario()
        => Assert.Empty(AIResponseParser.Parse(Json(type: "INVENTADO"), Ids));

    [Fact]
    public void Parse_DescartaPrioridadInvalida()
        => Assert.Empty(AIResponseParser.Parse(Json(priority: "URGENTE"), Ids));

    [Fact]
    public void Parse_DescartaRazonVacia()
        => Assert.Empty(AIResponseParser.Parse(Json(reason: ""), Ids));

    [Fact]
    public void Parse_AjustaConfianzaAlRango0a1()
    {
        var r = Assert.Single(AIResponseParser.Parse(Json(confidence: 7.5), Ids));
        Assert.Equal(1.0, r.Confidence);
    }

    [Fact]
    public void Parse_NormalizaMayusculas()
    {
        var r = Assert.Single(AIResponseParser.Parse(Json(type: "low_stock", priority: "high"), Ids));
        Assert.Equal("LOW_STOCK", r.Type);
        Assert.Equal("HIGH", r.Priority);
    }

    [Fact]
    public void Parse_JsonInvalido_DevuelveVacio()
        => Assert.Empty(AIResponseParser.Parse("esto no es json", Ids));

    [Fact]
    public void Parse_AceptaBloqueMarkdown()
    {
        var content = "```json\n" + Json() + "\n```";
        Assert.Single(AIResponseParser.Parse(content, Ids));
    }
}
