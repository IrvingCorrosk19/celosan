using Npgsql;
using SchoolManager.Infrastructure;

namespace SchoolManager.Scripts;

/// <summary>
/// Crea en Render los índices que faltan (homologar con LOCAL).
/// Ejecutar: dotnet run -- --add-render-indexes
/// Usa CREATE INDEX CONCURRENTLY para no bloquear escrituras.
/// </summary>
public static class AddRenderIndexes
{
    private static string ConnectionString => PostgresConnectionResolver.RequireFromAppSettings();

    private static readonly (string Name, string Sql)[] Indexes =
    {
        ("ix_groups_shift_id", "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_groups_shift_id ON groups(shift_id)"),
        ("ix_payment_concepts_created_by", "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_payment_concepts_created_by ON payment_concepts(created_by)"),
        ("ix_payment_concepts_updated_by", "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_payment_concepts_updated_by ON payment_concepts(updated_by)"),
        ("ix_payments_payment_concept_id", "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_payments_payment_concept_id ON payments(payment_concept_id)"),
        ("ix_payments_student_id", "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_payments_student_id ON payments(student_id)"),
        ("ix_shifts_name", "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_shifts_name ON shifts(name)"),
        ("ix_student_assignments_shift_id", "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_student_assignments_shift_id ON student_assignments(shift_id)"),
    };

    public static async Task RunAsync()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("   CREAR ÍNDICES FALTANTES EN RENDER (CONCURRENTLY)");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════\n");

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        Console.WriteLine("✅ Conectado a Render.\n");

        foreach (var (name, sql) in Indexes)
        {
            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();
                Console.WriteLine($"   ✅ {name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ {name}: {ex.Message}");
            }
        }

        Console.WriteLine("\n═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("   FIN");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
    }
}
