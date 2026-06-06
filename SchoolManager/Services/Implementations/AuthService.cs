using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using BCrypt.Net;

namespace SchoolManager.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly IUserService _userService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly SchoolDbContext _context;

        public AuthService(IUserService userService, IHttpContextAccessor httpContextAccessor, SchoolDbContext context)
        {
            _userService = userService;
            _httpContextAccessor = httpContextAccessor;
            _context = context;
        }

        public async Task<(bool success, string message, User? user)> LoginAsync(string email, string password)
        {
            var user = await _userService.GetByEmailAsync(email);
            
            if (user == null)
            {
                return (false, "Usuario o contraseña incorrecta", null);
            }

            bool passwordValid = false;

            // Verificar si la contraseña está hasheada
            if (IsPasswordHashed(user.PasswordHash))
            {
                // La contraseña está hasheada, usar BCrypt.Verify
                passwordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            }
            else
            {
                // La contraseña no está hasheada, comparar directamente
                passwordValid = password == user.PasswordHash;
                
                // Si la contraseña es correcta, hashearla y actualizarla
                if (passwordValid)
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                    await _userService.UpdateAsync(user);
                }
            }

            if (!passwordValid)
            {
                return (false, "Usuario o contraseña incorrecta", null);
            }

            if (user.Status?.ToLower() != "active")
            {
                return (false, "Usuario inactivo", null);
            }

            if (user.SchoolId.HasValue)
            {
                var school = await _context.Schools
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.Id == user.SchoolId.Value);
                if (school != null && !school.IsActive)
                {
                    return (false, "La institución se encuentra inactiva. Contacte al administrador.", null);
                }
            }

            // Actualizar último login
            user.LastLogin = DateTime.UtcNow;
            await _userService.UpdateAsync(user);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Role, user.Role)
            };

            if (user.SchoolId.HasValue)
                claims.Add(new Claim(TenantClaimTypes.SchoolId, user.SchoolId.Value.ToString()));

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
            };

            await _httpContextAccessor.HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            return (true, "Login exitoso", user);
        }

        public async Task LogoutAsync()
        {
            await _httpContextAccessor.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            return _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
        }

        public async Task<User?> GetCurrentUserAsync()
        {
            if (!await IsAuthenticatedAsync())
                return null;

            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return null;

            return await _userService.GetByIdWithRelationsAsync(Guid.Parse(userIdClaim.Value));
        }

        public bool IsPasswordHashed(string passwordHash)
        {
            // Verificar si la contraseña está hasheada con BCrypt
            // Los hashes de BCrypt comienzan con $2a$, $2b$, $2x$, $2y$ o $2$
            return !string.IsNullOrEmpty(passwordHash) && 
                   (passwordHash.StartsWith("$2a$") || 
                    passwordHash.StartsWith("$2b$") || 
                    passwordHash.StartsWith("$2x$") || 
                    passwordHash.StartsWith("$2y$") || 
                    passwordHash.StartsWith("$2$"));
        }
    }
} 