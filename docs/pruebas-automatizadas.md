# Pruebas automatizadas - SmartStock AI

Framework: xUnit sobre .NET 10. Comando: `dotnet test` (desde `src/`).

## Estrategia

| Nivel | Qué prueba | Cómo |
|---|---|---|
| Unitarias de dominio | Reglas de las entidades (`TieneStockBajo`, subtotales) y utilidades (`Result`, `PagedResult`) | Objetos puros |
| Unitarias de servicios | Lógica de negocio de Application (productos, inventario, ventas, compras, dashboard, reportes, recomendaciones) | Repositorios en memoria (`FakeRepository<T>`) que ejecutan los predicados reales, por lo que el filtro por `BusinessId` se prueba de verdad |
| Unitarias de IA | Validación de la respuesta del LLM y cliente HTTP del proveedor | `HttpMessageHandler` simulado: no se llama a internet ni se gasta la API |
| Integración (EF Core) | Mapeo real, relaciones, índice único (`BusinessId`+`Sku`), transacciones y flujo compra -> venta -> anulación | `ApplicationDbContext` real sobre SQLite en memoria |

## Trazabilidad con los casos obligatorios

| Caso requerido | Dónde se prueba |
|---|---|
| Crear producto | `ProductoServiceTests` (validaciones, SKU único por negocio, movimiento de stock inicial) |
| Registrar compra | `CompraServiceTests` (totales, proveedor/producto válidos, costo y cantidades) |
| Actualizar inventario | `CompraServiceTests`, `InventarioServiceTests` (entradas, salidas, ajustes, bitácora de movimientos) |
| Registrar venta | `VentaServiceTests` (totales, descuento, precio tomado del producto) |
| Reducir inventario | `VentaServiceTests` (reduce stock, rechaza sobreventa, atomicidad con varias líneas, anulación restaura) |
| Detectar stock bajo | `ProductoDomainTests`, `DashboardServiceTests` (alertas SinStock/StockBajo), `RecomendacionServiceTests` (reglas deterministas) |
| Generar recomendación | `RecomendacionServiceTests`, `AIResponseParserTests`, `ChatCompletionsAIServiceTests` |
| Usuarios y roles | `UsuarioServiceTests` (no se puede cambiar el propio rol, siempre queda un administrador activo, rol inválido, correo repetido), `AppRolesTests` |
| Venta sin stock configurable | `VentaConfiguracionTests`, `NegocioServiceTests` |
| Reportes nuevos y filtros | `ReporteNuevosTests` (más vendidos, movimientos, filtros por categoría/proveedor) |
| Aislamiento entre negocios | Pruebas "OtroNegocio" en todos los servicios + `FlujoCompletoIntegrationTests.AislamientoEntreNegocios...` + `EfRepositoryTests` |
| Persistencia con Entity Framework Core | `EfRepositoryTests`, `FlujoCompletoIntegrationTests` |
| Manejo de errores de la IA | `ChatCompletionsAIServiceTests` (HTTP 401/429/500, red caída, respuesta inválida: el sistema sigue con reglas) |
| Minimización de datos enviados a la IA | `ChatCompletionsAIServiceTests.MinimizacionDeDatos...` fija las únicas propiedades que viajan al proveedor |

## Cómo generar la evidencia

```bash
cd src
dotnet test --logger "console;verbosity=normal"          # lista cada prueba con su resultado (captura de pantalla)
dotnet test --logger "trx;LogFileName=resultados.trx"    # archivo de resultados para adjuntar
```

Adjunta en la entrega: captura de la salida con todas las pruebas en verde, y el archivo `.trx`.

## Pruebas manuales (complementarias)

Para los casos que requieren la interfaz o Docker, registrar en una tabla: caso de uso, pasos, resultado esperado,
resultado obtenido y captura. Incluir al menos: login/registro, CRUD de productos, venta con stock insuficiente (error esperado),
compra que aumenta stock, pantalla de IA con y sin clave, y ejecución completa con `docker compose up`.

## Matriz de permisos por rol (prueba manual / evidencia)

Iniciar sesión con cada usuario demo (`administrador@`, `vendedor@`, `inventario@smartstock.ai`) y comprobar:

| Módulo | Administrador | Vendedor | Inventario |
|---|---|---|---|
| Dashboard, Configuración (mi cuenta) | Sí | Sí | Sí |
| Productos (consultar) | Sí | Sí | Sí |
| Productos (crear/editar/eliminar), Categorías, Proveedores, Inventario, Compras | Sí | No (403) | Sí |
| Ventas | Sí | Sí | No (403) |
| Reportes de inventario, compras, movimientos, baja rotación | Sí | No | Sí |
| Reportes de ventas, más vendidos, ganancias | Sí | No | No |
| Recomendaciones de IA | Sí | No | Sí |
| Usuarios y configuración del negocio | Sí | No | No |
