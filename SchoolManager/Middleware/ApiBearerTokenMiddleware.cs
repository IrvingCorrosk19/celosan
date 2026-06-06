using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Helpers;
using SchoolManager.Models;

namespace SchoolManager.Middleware;

/// <summary>
/// Establece el usuario en el contexto cuando la app móvil envía el token devuelto por POST /api/auth/login
/// en el header Authorization: Bearer &lt;token&gt;. El token es base64(userId:email:timestamp).
/// Permite que DisciplineReport y otras APIs usen CurrentUserService cuando se llama desde el APK.
/// </summary>
public class ApiBearerTokenMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly TimeSpan TokenMaxAge = TimeSpan.FromHours(24);

    public ApiBearerTokenMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, SchoolDbContext db)
    {
        if (context.User?.Identity?.IsAuthenticated != true &&
            context.Request.Headers.Authorization.FirstOrDefault() is string auth &&
            auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = auth["Bearer ".Length..].Trim();
            if (!string.IsNullOrEmpty(token))
            {
                try
                {
                    var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(token));
                    var parts = decoded.Split(':', 3, StringSplitOptions.None);
                    if (parts.Length >= 3 &&
                        Guid.TryParse(parts[0], out var userId) &&
                        DateTime.TryParseExact(parts[2], "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var tokenTime))
                    {
                        var age = DateTime.UtcNow - tokenTime.ToUniversalTime();
                        if (age >= TimeSpan.Zero && age <= TokenMaxAge)
                        {
                            var user = await db.Users.AsNoTracking()
                                .Where(u => u.Id == userId)
                                .Select(u => new { u.Id, u.Email, u.Name, u.LastName, u.Role, u.SchoolId })
                                .FirstOrDefaultAsync(context.RequestAborted);

                            if (user != null)
                            {
                                var claims = new List<Claim>
                                {
                                    new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                                    new(ClaimTypes.Email, user.Email ?? ""),
                                    new(ClaimTypes.Name, $"{user.Name} {user.LastName}".Trim()),
                                    new(ClaimTypes.Role, user.Role ?? "")
                                };
                                if (user.SchoolId.HasValue)
                                    claims.Add(new Claim(TenantClaimTypes.SchoolId, user.SchoolId.Value.ToString()));
                                var identity = new ClaimsIdentity(claims, "ApiBearer");
                                context.User = new ClaimsPrincipal(identity);
                            }
                        }
                    }
                }
                catch
                {
                    // Token inválido o expirado: seguir sin autenticar
                }
            }
        }

        await _next(context);
    }
}
