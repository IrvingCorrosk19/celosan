using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SchoolManager.Infrastructure;

namespace SchoolManager.Models;

/// <summary>
/// Evita arrancar Program.cs (y sus scripts Ensure*) al generar migraciones.
/// Usa ConnectionStrings:DefaultConnection.
/// </summary>
public class SchoolDbContextFactory : IDesignTimeDbContextFactory<SchoolDbContext>
{
    public SchoolDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SchoolDbContext>();
        optionsBuilder.UseNpgsql(PostgresConnectionResolver.RequireFromAppSettings());
        return new SchoolDbContext(optionsBuilder.Options);
    }
}
