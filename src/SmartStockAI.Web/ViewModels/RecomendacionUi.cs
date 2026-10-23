namespace SmartStockAI.Web.ViewModels;

/// <summary>Textos y clases CSS para mostrar recomendaciones (compartido por la vista y el parcial).</summary>
public static class RecomendacionUi
{
    public static string PrioridadTexto(string p) => p switch
    {
        "HIGH" => "Alta",
        "MEDIUM" => "Media",
        _ => "Baja"
    };

    public static string PrioridadClase(string p) => p switch
    {
        "HIGH" => "badge-stock-bajo",
        "MEDIUM" => "bg-warning text-dark",
        _ => "badge-stock-ok"
    };

    public static string TipoTexto(string t) => t switch
    {
        "LOW_STOCK" => "Stock bajo",
        "HIGH_DEMAND" => "Alta demanda",
        "LOW_ROTATION" => "Baja rotación",
        "PURCHASE_RECOMMENDATION" => "Recomendación de compra",
        "SALES_TREND" => "Tendencia de ventas",
        _ => "Alerta general"
    };
}
