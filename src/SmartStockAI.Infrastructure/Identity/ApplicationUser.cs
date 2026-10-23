using Microsoft.AspNetCore.Identity;

namespace SmartStockAI.Infrastructure.Identity;

/// <summary>
/// Usuario de la aplicación. Extiende IdentityUser con el vínculo al
/// Business (tenant) al que pertenece, garantizando que cada sesión solo
/// pueda consultar los datos de su propio negocio (sección 12).
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string NombreCompleto { get; set; } = string.Empty;
    public int BusinessId { get; set; }
    public bool Activo { get; set; } = true;
}
