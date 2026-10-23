using SmartStockAI.Application.Common;

namespace SmartStockAI.Infrastructure.Identity;

/// <summary>Catálogo de roles. Los valores viven en Application (AppRoles) para usarlos también en [Authorize] y reglas de negocio.</summary>
public static class Roles
{
    public const string Administrador = AppRoles.Administrador;
    public const string Vendedor = AppRoles.Vendedor;
    public const string Inventario = AppRoles.Inventario;

    public static readonly string[] Todos = AppRoles.Todos;
}
