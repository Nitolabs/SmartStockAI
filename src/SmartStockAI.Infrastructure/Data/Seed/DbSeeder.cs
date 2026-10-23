using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartStockAI.Domain.Entities;
using SmartStockAI.Infrastructure.Identity;

namespace SmartStockAI.Infrastructure.Data.Seed;

/// <summary>
/// Aplica las migraciones, crea los roles y, si la base está vacía, un negocio demo con un usuario por rol
/// y datos de ejemplo (categorías, proveedores, productos). Se ejecuta al iniciar la aplicación.
/// </summary>
public static class DbSeeder
{
    private const string PasswordDemo = "SmartStock2026!";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        // Con migraciones generadas se aplican; si todavía no existen (primer arranque del proyecto) se crea el esquema.
        // Importante: genera las migraciones ANTES del primer arranque (ver PRIMEROS-PASOS.md) para no mezclar ambos caminos.
        if (context.Database.GetMigrations().Any())
            await context.Database.MigrateAsync();
        else
            await context.Database.EnsureCreatedAsync();

        foreach (var rol in Roles.Todos)
        {
            if (!await roleManager.RoleExistsAsync(rol))
                await roleManager.CreateAsync(new IdentityRole(rol));
        }

        if (context.Businesses.Any())
        {
            logger.LogInformation("Seed omitido: ya existen datos.");
            return;
        }

        var business = new Business
        {
            Nombre = "Minisúper Don Bosco",
            Rubro = "Minisúper / Conveniencia",
            Direccion = "San Salvador, El Salvador",
            Telefono = "2222-2222",
            Email = "contacto@minisuperdonbosco.com"
        };
        context.Businesses.Add(business);
        await context.SaveChangesAsync();

        // Un usuario demo por rol, para poder demostrar los permisos de cada uno.
        await CrearUsuarioAsync(userManager, logger, business.Id, "administrador@smartstock.ai", "Administrador SmartStock", Roles.Administrador);
        await CrearUsuarioAsync(userManager, logger, business.Id, "vendedor@smartstock.ai", "Vendedor Demo", Roles.Vendedor);
        await CrearUsuarioAsync(userManager, logger, business.Id, "inventario@smartstock.ai", "Encargado de Inventario", Roles.Inventario);

        var categoriaAbarrotes = new Categoria { BusinessId = business.Id, Nombre = "Abarrotes", Descripcion = "Productos de consumo básico" };
        var categoriaLimpieza = new Categoria { BusinessId = business.Id, Nombre = "Limpieza", Descripcion = "Artículos de limpieza del hogar" };
        var categoriaBebidas = new Categoria { BusinessId = business.Id, Nombre = "Bebidas", Descripcion = "Bebidas embotelladas y enlatadas" };
        context.Categorias.AddRange(categoriaAbarrotes, categoriaLimpieza, categoriaBebidas);
        await context.SaveChangesAsync();

        var proveedor1 = new Proveedor { BusinessId = business.Id, Nombre = "Distribuidora La Constancia", Contacto = "Carlos Pérez", Telefono = "7777-1111", Email = "ventas@laconstancia.com" };
        var proveedor2 = new Proveedor { BusinessId = business.Id, Nombre = "Comercial El Rosario", Contacto = "Ana Gómez", Telefono = "7777-2222", Email = "contacto@elrosario.com" };
        context.Proveedores.AddRange(proveedor1, proveedor2);
        await context.SaveChangesAsync();

        var productos = new List<Producto>
        {
            new() { BusinessId = business.Id, Nombre = "Arroz Excelencia 1lb", Sku = "ABR-001", CategoriaId = categoriaAbarrotes.Id, ProveedorId = proveedor1.Id, PrecioCompra = 0.60m, PrecioVenta = 0.95m, StockActual = 120, StockMinimo = 30 },
            new() { BusinessId = business.Id, Nombre = "Frijol Rojo 1lb", Sku = "ABR-002", CategoriaId = categoriaAbarrotes.Id, ProveedorId = proveedor1.Id, PrecioCompra = 0.75m, PrecioVenta = 1.10m, StockActual = 8, StockMinimo = 20 },
            new() { BusinessId = business.Id, Nombre = "Azúcar Blanca 1lb", Sku = "ABR-003", CategoriaId = categoriaAbarrotes.Id, ProveedorId = proveedor2.Id, PrecioCompra = 0.55m, PrecioVenta = 0.85m, StockActual = 60, StockMinimo = 15 },
            new() { BusinessId = business.Id, Nombre = "Jabón para Ropa 500g", Sku = "LIM-001", CategoriaId = categoriaLimpieza.Id, ProveedorId = proveedor2.Id, PrecioCompra = 1.20m, PrecioVenta = 1.75m, StockActual = 4, StockMinimo = 10 },
            new() { BusinessId = business.Id, Nombre = "Cloro 1L", Sku = "LIM-002", CategoriaId = categoriaLimpieza.Id, ProveedorId = proveedor2.Id, PrecioCompra = 1.00m, PrecioVenta = 1.50m, StockActual = 25, StockMinimo = 10 },
            new() { BusinessId = business.Id, Nombre = "Gaseosa Cola 1.5L", Sku = "BEB-001", CategoriaId = categoriaBebidas.Id, ProveedorId = proveedor1.Id, PrecioCompra = 0.90m, PrecioVenta = 1.40m, StockActual = 45, StockMinimo = 12 },
            new() { BusinessId = business.Id, Nombre = "Agua Embotellada 600ml", Sku = "BEB-002", CategoriaId = categoriaBebidas.Id, ProveedorId = proveedor1.Id, PrecioCompra = 0.25m, PrecioVenta = 0.50m, StockActual = 100, StockMinimo = 25 },
        };
        context.Productos.AddRange(productos);
        await context.SaveChangesAsync();

        logger.LogInformation("Seed inicial completado para el negocio '{Business}'.", business.Nombre);
    }

    private static async Task CrearUsuarioAsync(
        UserManager<ApplicationUser> userManager, ILogger logger, int businessId, string email, string nombre, string rol)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            NombreCompleto = nombre,
            BusinessId = businessId
        };

        var resultado = await userManager.CreateAsync(user, PasswordDemo);
        if (!resultado.Succeeded)
        {
            logger.LogWarning("No se pudo crear el usuario semilla {Email}: {Errores}",
                email, string.Join(", ", resultado.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(user, rol);
    }
}
