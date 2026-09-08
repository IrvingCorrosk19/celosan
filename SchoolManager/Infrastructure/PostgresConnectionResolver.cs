using System.Text;
using Microsoft.Extensions.Configuration;

namespace SchoolManager.Infrastructure;

/// <summary>
/// Resuelve la cadena Npgsql: appsettings, variable ConnectionStrings__DefaultConnection o DATABASE_URL (Render).
/// </summary>
public static class PostgresConnectionResolver
{
    public static string? Resolve(IConfiguration configuration)
    {
        var fromConfig = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(fromConfig))
            return fromConfig;

        var fromEnv = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(databaseUrl))
            return ConvertDatabaseUrlToNpgsql(databaseUrl);

        return null;
    }

    public static string RequireFromAppSettings()
    {
        var root = FindAppSettingsDirectory();
        var builder = new ConfigurationBuilder()
            .SetBasePath(root)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables();
        var resolved = Resolve(builder.Build());
        if (string.IsNullOrWhiteSpace(resolved))
            throw new InvalidOperationException("Falta ConnectionStrings:DefaultConnection.");
        return resolved;
    }

    public static (string Host, string Database, string Username) Describe(string connectionString)
    {
        var map = Parse(connectionString);
        map.TryGetValue("Host", out var host);
        map.TryGetValue("Database", out var database);
        if (!map.TryGetValue("Username", out var username))
            map.TryGetValue("User ID", out username);
        return (host ?? "", database ?? "", username ?? "");
    }

    public static Dictionary<string, string> Parse(string connectionString)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in connectionString.Split(';'))
        {
            var t = segment.Trim();
            if (t.Length == 0)
                continue;
            var eq = t.IndexOf('=');
            if (eq < 1)
                continue;
            map[t[..eq].Trim()] = t[(eq + 1)..].Trim();
        }
        return map;
    }

    private static string FindAppSettingsDirectory()
    {
        var candidates = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            Path.Combine(Directory.GetCurrentDirectory(), "SchoolManager")
        };
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate)
                && File.Exists(Path.Combine(candidate, "appsettings.json")))
                return candidate;
        }

        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "appsettings.json")))
                return dir.FullName;
            var nested = Path.Combine(dir.FullName, "SchoolManager", "appsettings.json");
            if (File.Exists(nested))
                return Path.Combine(dir.FullName, "SchoolManager");
            dir = dir.Parent;
        }

        throw new InvalidOperationException("No se encontró appsettings.json para DefaultConnection.");
    }

    public static string ConvertDatabaseUrlToNpgsql(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var path = uri.AbsolutePath.TrimStart('/');
        var db = path.Split('?')[0];
        if (string.IsNullOrEmpty(db))
            db = "postgres";
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432;

        var sb = new StringBuilder();
        sb.Append($"Host={host};Port={port};Username={user};Password={pass};Database={db};");

        var query = uri.Query ?? "";
        var needsSsl = query.Contains("sslmode=require", StringComparison.OrdinalIgnoreCase)
                       || query.Contains("sslmode=verify-full", StringComparison.OrdinalIgnoreCase)
                       || query.Contains("sslmode=verify-ca", StringComparison.OrdinalIgnoreCase);

        if (!needsSsl && !IsLocalHost(host))
            needsSsl = true;

        if (needsSsl)
            sb.Append("SSL Mode=Require;Trust Server Certificate=true;");

        return sb.ToString();
    }

    private static bool IsLocalHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
}
