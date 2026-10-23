namespace SmartStockAI.Application.Common;

/// <summary>
/// Roles de la aplicación. Las combinaciones son constantes para poder usarlas en
/// [Authorize(Roles = ...)], que exige valores constantes en tiempo de compilación.
/// </summary>
public static class AppRoles
{
    public const string Administrador = "Administrador";
    public const string Vendedor = "Vendedor";
    public const string Inventario = "Inventario";

    public const string AdministradorOInventario = Administrador + "," + Inventario;
    public const string AdministradorOVendedor = Administrador + "," + Vendedor;

    public static readonly string[] Todos = { Administrador, Vendedor, Inventario };

    public static bool EsValido(string? rol) => rol is not null && Todos.Contains(rol);
}
