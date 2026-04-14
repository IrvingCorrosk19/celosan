# Migraciones y homologación Local vs Render

## Resumen (verificación 2025-03-15)

- **Migraciones pendientes:** En ambas bases (local y Render) el esquema ya estaba aplicado por scripts previos, pero `__EFMigrationsHistory` solo tenía registrada `20260217000736_AddSchoolIsActive`. Por eso `dotnet ef database update` fallaba (tablas como `area` ya existen).
- **Solución:** Sincronizar la tabla `__EFMigrationsHistory` insertando los IDs de todas las migraciones del proyecto, sin volver a ejecutar el SQL de cada migración.
- **Estructura Local vs Render:** La comparación con `--compare-db-schemas` indicó que **las dos bases están homologadas**: mismas tablas, columnas, índices y constraints check.

## Cómo sincronizar el historial de migraciones

### Opción A: Script SQL (recomendado si la app está en ejecución)

Ejecutar el script en **ambas** bases (local y Render):

```bash
# Local (ajusta usuario/contraseña si es necesario)
psql "Host=localhost;Database=schoolmanagement;Username=postgres;Password=Panama2020$" -f Scripts/SyncEfMigrationsHistory.sql

# Render (usa la cadena de conexión de Render)
psql "Host=dpg-d7erln5ckfvc73en9obg-a.oregon-postgres.render.com;Database=schoolmanager_daqf;Username=admin;Password=...;Port=5432;SSL Mode=Require" -f Scripts/SyncEfMigrationsHistory.sql
```

O desde pgAdmin/DBeaver: abrir `Scripts/SyncEfMigrationsHistory.sql` y ejecutarlo contra cada base.

### Opción B: Comandos de la aplicación

Con la aplicación **detenida** (para poder compilar):

```bash
# Sincronizar solo la base configurada (según entorno)
dotnet run -- --sync-ef-migrations-history

# Sincronizar LOCAL y RENDER en un solo paso
dotnet run -- --sync-ef-migrations-both
```

Para usar la base local con `--sync-ef-migrations-history`, ejecutar con entorno Development (usa `appsettings.Development.json`). Para Render, ejecutar sin Development (usa `appsettings.json`).

Después de sincronizar, comprobar:

```bash
# Contra local
$env:ASPNETCORE_ENVIRONMENT='Development'; dotnet ef migrations list

# Contra Render (quitar entorno Development)
$env:ASPNETCORE_ENVIRONMENT=''; dotnet ef migrations list
```

No debería aparecer ninguna migración en estado "Pending".

## Cómo validar que Local y Render están homologadas

```bash
dotnet run -- --compare-db-schemas
```

El script compara:

1. Tablas solo en Local (faltarían en Render)
2. Tablas solo en Render (legacy o desusadas)
3. Columnas en Local que no están en Render
4. Índices en Local que no están en Render
5. Constraints CHECK en Local que no están en Render

Si todo está homologado, verás "Ninguna" / "Ninguno" en cada sección.

## Archivos relacionados

| Archivo | Descripción |
|---------|-------------|
| `Scripts/SyncEfMigrationsHistory.cs` | Lógica para insertar filas en `__EFMigrationsHistory` (usado por `--sync-ef-migrations-*`). |
| `Scripts/SyncEfMigrationsHistory.sql` | Script SQL idempotente para ejecutar en cualquier cliente (psql, pgAdmin, etc.). |
| `Scripts/CompareDbSchemas.cs` | Comparación de estructura entre Local y Render. |
