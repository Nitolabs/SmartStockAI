namespace SmartStockAI.Infrastructure.Services;

/// <summary>Sección "Email" de la configuración. Si Host o From están vacíos, no se envían correos.</summary>
public class AppEmailOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
}
