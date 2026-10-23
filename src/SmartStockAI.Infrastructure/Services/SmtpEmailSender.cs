using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartStockAI.Application.Interfaces;

namespace SmartStockAI.Infrastructure.Services;

/// <summary>
/// Envío de correo por SMTP. Si no está configurado devuelve false (y lo registra) en lugar de fallar,
/// para que la recuperación de contraseña degrade con elegancia.
/// </summary>
public class SmtpEmailSender : IAppEmailSender
{
    private readonly AppEmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<AppEmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Host) && !string.IsNullOrWhiteSpace(_options.From);

    public async Task<bool> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            _logger.LogInformation("SMTP no configurado: no se envió el correo '{Subject}'.", subject);
            return false;
        }

        try
        {
            using var client = new SmtpClient(_options.Host, _options.Port) { EnableSsl = _options.EnableSsl };
            if (!string.IsNullOrWhiteSpace(_options.User))
                client.Credentials = new NetworkCredential(_options.User, _options.Password);

            using var message = new MailMessage(_options.From, to, subject, htmlBody) { IsBodyHtml = true };
            await client.SendMailAsync(message, ct);
            return true;
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            _logger.LogError(ex, "No se pudo enviar el correo '{Subject}'.", subject);
            return false;
        }
    }
}
