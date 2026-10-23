namespace SmartStockAI.Infrastructure.AI;

/// <summary>
/// Configuración de la sección "AI". Los secretos (ApiKey) NUNCA van en appsettings versionados:
/// usar user-secrets en desarrollo y variables de entorno / Key Vault en producción.
/// </summary>
public class AIOptions
{
    /// <summary>"OpenAI" o "AzureOpenAI".</summary>
    public string Provider { get; set; } = "OpenAI";

    /// <summary>OpenAI: nombre del modelo. Azure OpenAI: nombre del deployment.</summary>
    public string Model { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Solo Azure OpenAI, p. ej. https://mi-recurso.openai.azure.com</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Solo Azure OpenAI. Tomar el valor vigente de la documentación de Azure.</summary>
    public string ApiVersion { get; set; } = string.Empty;

    public bool IsAzure => string.Equals(Provider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(Model)
        && (!IsAzure || (!string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(ApiVersion)));
}
