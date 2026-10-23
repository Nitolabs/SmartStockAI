# Base de datos - SmartStock AI

Motor: SQL Server (contenedor Docker en local, Azure SQL Database en producción).
Acceso a datos: Entity Framework Core con migraciones (código en `src/SmartStockAI.Infrastructure/Data`).

## Contenido de esta carpeta

| Archivo | Descripción |
|---|---|
| `schema.sql` | Script idempotente con todas las migraciones (generado con EF Core, ver abajo) |

## Regenerar `schema.sql`

Desde `src/` (requiere `dotnet tool install --global dotnet-ef`):

```bash
dotnet ef migrations script -i \
  -p SmartStockAI.Infrastructure -s SmartStockAI.Web \
  -o ../database/schema.sql
```

Vuelve a generarlo y haz commit cada vez que agregues una migración.

## Cómo se crea la base

La aplicación aplica las migraciones al iniciar (`Database.MigrateAsync()`),
así que con `docker compose up` la base `SmartStockAI` se crea sola.
`schema.sql` sirve para revisar el esquema o crearlo a mano en Azure SQL.

## Datos iniciales (seed)

Al primer arranque se crea un negocio demo, roles (Administrador, Vendedor, Inventario),
un usuario administrador, y categorías, proveedores y productos de ejemplo.
Cambia la contraseña del administrador antes de usar datos reales.

## Modelo (resumen)

`Business` (tenant) 1—N `Categoria`, `Proveedor`, `Producto`, `Venta`, `Compra`, `MovimientoInventario`.
`Venta` 1—N `VentaDetalle` · `Compra` 1—N `CompraDetalle` · `Producto` N—1 `Categoria` · `Producto` N—0..1 `Proveedor`.
Toda entidad operativa lleva `BusinessId` (aislamiento entre negocios).
