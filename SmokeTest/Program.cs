using Npgsql;

var connStr = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL");

if (string.IsNullOrWhiteSpace(connStr))
{
    Console.WriteLine("Falta DATABASE_URL o ConnectionStrings__DefaultConnection.");
    return;
}

if (connStr.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
    || connStr.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
{
    var uri = new Uri(connStr);
    var userInfo = uri.UserInfo.Split(':', 2);
    var user = Uri.UnescapeDataString(userInfo[0]);
    var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
    var db = uri.AbsolutePath.TrimStart('/').Split('?')[0];
    var port = uri.Port > 0 ? uri.Port : 5432;
    connStr = $"Host={uri.Host};Port={port};Username={user};Password={pass};Database={db};SSL Mode=Require;Trust Server Certificate=true";
}

await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();
Console.WriteLine("OK: conexión abierta.");
