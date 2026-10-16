using Microsoft.Extensions.Configuration;
using SchoolManager.Infrastructure;
using Xunit;

namespace SchoolManager.Tests;

public class PostgresConnectionResolverTests
{
    [Fact]
    public void Resolve_uses_connection_string_from_configuration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=from-config;Database=db;Username=u;Password=p"
            })
            .Build();

        var resolved = PostgresConnectionResolver.Resolve(config);
        Assert.Contains("Host=from-config", resolved);
        Assert.Contains("Database=db", resolved);
    }

    [Fact]
    public void Resolve_skips_empty_appsettings_and_uses_database_url()
    {
        var previous = Environment.GetEnvironmentVariable("DATABASE_URL");
        Environment.SetEnvironmentVariable(
            "DATABASE_URL",
            "postgresql://admin:secret@dpg-example.oregon-postgres.render.com/schoolmanager_daqf");
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = ""
                })
                .Build();

            var resolved = PostgresConnectionResolver.Resolve(config);
            Assert.NotNull(resolved);
            Assert.Contains("Host=dpg-example.oregon-postgres.render.com", resolved);
            Assert.Contains("Database=schoolmanager_daqf", resolved);
            Assert.Contains("Username=admin", resolved);
            Assert.Contains("SSL Mode=Require", resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DATABASE_URL", previous);
        }
    }

    [Fact]
    public void Resolve_uses_connectionstrings_environment_variable()
    {
        var previous = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            "Host=render-host;Database=db;Username=u;Password=p;SSL Mode=Require;Trust Server Certificate=true");
        try
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var resolved = PostgresConnectionResolver.Resolve(config);
            Assert.NotNull(resolved);
            Assert.Contains("Host=render-host", resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", previous);
        }
    }

    [Fact]
    public void Normalize_converts_postgres_url_in_connection_string()
    {
        var npgsql = PostgresConnectionResolver.Normalize(
            "postgres://admin:secret@dpg-internal/schoolmanager_daqf");
        Assert.NotNull(npgsql);
        Assert.Contains("Host=dpg-internal", npgsql);
        Assert.Contains("Port=5432", npgsql);
        Assert.Contains("Database=schoolmanager_daqf", npgsql);
        Assert.Contains("SSL Mode=Require", npgsql);
    }
}
