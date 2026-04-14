using Npgsql;

namespace SchoolManager.Scripts;

/// <summary>
/// Crea en Render la tabla student_payment_access (módulo Club de Padres) si no existe.
/// Ejecutar: dotnet run -- --apply-render-student-payment-access
/// </summary>
public static class ApplyRenderStudentPaymentAccess
{
    private const string RenderConnectionString =
        "Host=dpg-d7erln5ckfvc73en9obg-a.oregon-postgres.render.com;Database=schoolmanager_daqf;Username=admin;Password=iztY1ZL7WHbu2A5gtMSb1DFMhrK3Lo3r;Port=5432;SSL Mode=Require;Trust Server Certificate=true";

    public static async Task RunAsync()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("   CREAR student_payment_access EN RENDER (si no existe)");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════\n");

        await using var conn = new NpgsqlConnection(RenderConnectionString);
        await conn.OpenAsync();
        Console.WriteLine("✅ Conectado a Render.\n");

        // Comprobar si ya existe
        await using (var checkCmd = new NpgsqlCommand(
            "SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'student_payment_access' LIMIT 1", conn))
        {
            var exists = await checkCmd.ExecuteScalarAsync();
            if (exists != null)
            {
                Console.WriteLine("   La tabla student_payment_access ya existe en Render. Nada que hacer.");
                Console.WriteLine("\n═══════════════════════════════════════════════════════════════════");
                return;
            }
        }

        var ddl = @"
CREATE TABLE IF NOT EXISTS student_payment_access (
    id uuid NOT NULL DEFAULT uuid_generate_v4(),
    student_id uuid NOT NULL,
    school_id uuid NOT NULL,
    carnet_status character varying(20) NOT NULL DEFAULT 'Pendiente',
    platform_access_status character varying(20) NOT NULL DEFAULT 'Pendiente',
    carnet_status_updated_at timestamp with time zone NULL,
    platform_status_updated_at timestamp with time zone NULL,
    carnet_updated_by_user_id uuid NULL,
    platform_updated_by_user_id uuid NULL,
    created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at timestamp with time zone NULL,
    CONSTRAINT student_payment_access_pkey PRIMARY KEY (id),
    CONSTRAINT student_payment_access_student_id_fkey FOREIGN KEY (student_id) REFERENCES users (id) ON DELETE RESTRICT,
    CONSTRAINT student_payment_access_school_id_fkey FOREIGN KEY (school_id) REFERENCES schools (id) ON DELETE RESTRICT,
    CONSTRAINT student_payment_access_carnet_updated_by_fkey FOREIGN KEY (carnet_updated_by_user_id) REFERENCES users (id) ON DELETE SET NULL,
    CONSTRAINT student_payment_access_platform_updated_by_fkey FOREIGN KEY (platform_updated_by_user_id) REFERENCES users (id) ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS IX_student_payment_access_student_id ON student_payment_access (student_id);
CREATE INDEX IF NOT EXISTS IX_student_payment_access_school_id ON student_payment_access (school_id);
CREATE INDEX IF NOT EXISTS IX_student_payment_access_carnet_status_school_id ON student_payment_access (carnet_status, school_id);
CREATE UNIQUE INDEX IF NOT EXISTS IX_student_payment_access_student_id_school_id ON student_payment_access (student_id, school_id);
CREATE INDEX IF NOT EXISTS IX_student_payment_access_carnet_updated_by_user_id ON student_payment_access (carnet_updated_by_user_id);
CREATE INDEX IF NOT EXISTS IX_student_payment_access_platform_updated_by_user_id ON student_payment_access (platform_updated_by_user_id);
";

        try
        {
            await using var cmd = new NpgsqlCommand(ddl, conn);
            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine("   ✅ Tabla student_payment_access e índices creados en Render.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("   ❌ Error: " + ex.Message);
            throw;
        }

        Console.WriteLine("\n═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("   FIN");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
    }
}
