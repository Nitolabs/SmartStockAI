namespace SmartStockAI.Application.Interfaces;

/// <summary>Envío de correos (recuperación de contraseña). Si no hay SMTP configurado, la implementación solo registra en el log.</summary>
public interface IAppEmailSender
{
    bool IsConfigured { get; }

    /// <summary>Devuelve true si el correo se entregó al servidor SMTP.</summary>
    Task<bool> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
