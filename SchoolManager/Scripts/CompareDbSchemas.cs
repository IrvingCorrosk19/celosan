using Npgsql;

namespace SchoolManager.Scripts;

/// <summary>
/// Analiza la estructura de la BD RENDER.
/// Ejecutar: dotnet run -- --compare-db-schemas
/// </summary>
public static class CompareDbSchemas
{
    private const string RenderConnectionString =
        "Host=dpg-d7erln5ckfvc73en9obg-a.oregon-postgres.render.com;Database=schoolmanager_daqf;Username=admin;Password=iztY1ZL7WHbu2A5gtMSb1DFMhrK3Lo3r;Port=5432;SSL Mode=Require;Trust Server Certificate=true";

    public static async Task RunAsync()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("   ANÁLISIS: ESTRUCTURA RENDER");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

        try
        {
            var renderTables = await GetTablesAndColumnsAsync(RenderConnectionString, "RENDER");

            // Tablas en RENDER
            var tablesInRender = renderTables.Keys.OrderBy(t => t).ToList();

            // Salida del análisis
            Console.WriteLine("┌─────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ TABLAS EN RENDER                                      │");
            Console.WriteLine("└─────────────────────────────────────────────────────────┘");
            foreach (var t in tablesInRender)
                Console.WriteLine($"   • {t}");
            Console.WriteLine();

            Console.WriteLine("═════════════════════════════════════════════════════════════");
            Console.WriteLine("   FIN DEL ANÁLISIS (solo lectura, no se modificó nada)");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"   Inner: {ex.InnerException.Message}");
        }
    }

    private static async Task<Dictionary<string, Dictionary<string, string>>> GetTablesAndColumnsAsync(
        string connStr, string label)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        var sql = @"
            SELECT table_name, column_name, data_type
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_catalog = current_database()
            ORDER BY table_name, ordinal_position;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var tbl = r.GetString(0).ToLowerInvariant();
            var col = r.GetString(1).ToLowerInvariant();
            var dt = r.GetString(2);
            if (!result.ContainsKey(tbl))
                result[tbl] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            result[tbl][col] = dt;
        }
        return result;
    }
}
