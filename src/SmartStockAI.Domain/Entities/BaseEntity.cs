namespace SmartStockAI.Domain.Entities;

/// <summary>
/// Propiedades comunes de auditoría para todas las entidades del dominio.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaModificacion { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>
/// Entidad perteneciente a un negocio (tenant). Toda entidad que almacene
/// datos operativos de una MYPE debe heredar de aquí para garantizar el
/// aislamiento multi-tenant descrito en el prompt maestro (sección 12).
/// </summary>
public abstract class TenantEntity : BaseEntity
{
    public int BusinessId { get; set; }
    public Business Business { get; set; } = null!;
}
