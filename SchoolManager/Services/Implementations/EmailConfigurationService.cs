using Microsoft.EntityFrameworkCore;
using SchoolManager.Dtos;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using System.Net.Mail;
using System.Net;
using Microsoft.Extensions.Logging;

namespace SchoolManager.Services.Implementations
{
    public class EmailConfigurationService : IEmailConfigurationService
    {
        private readonly SchoolDbContext _context;
        private readonly ITenantContext _tenantContext;
        private readonly ILogger<EmailConfigurationService> _logger;

        public EmailConfigurationService(
            SchoolDbContext context,
            ITenantContext tenantContext,
            ILogger<EmailConfigurationService> logger)
        {
            _context = context;
            _tenantContext = tenantContext;
            _logger = logger;
        }

        public async Task<List<EmailConfigurationDto>> GetAllAsync()
        {
            var query = _context.EmailConfigurations
                .Include(ec => ec.School)
                .AsQueryable();

            if (!_tenantContext.BypassTenantFilter && _tenantContext.SchoolId.HasValue)
                query = query.Where(ec => ec.SchoolId == _tenantContext.SchoolId.Value);

            var configurations = await query
                .OrderBy(ec => ec.SchoolId)
                .ThenBy(ec => ec.CreatedAt)
                .ToListAsync();

            return configurations.Select(MapToDto).ToList();
        }

        public async Task<EmailConfigurationDto?> GetByIdAsync(Guid id)
        {
            var configuration = await _context.EmailConfigurations
                .Include(ec => ec.School)
                .FirstOrDefaultAsync(ec => ec.Id == id);

            if (configuration == null)
                return null;

            if (!SchoolTenantHelper.CanAccessResource(configuration.SchoolId, _tenantContext))
                return null;

            return MapToDto(configuration);
        }

        public async Task<EmailConfigurationDto?> GetBySchoolIdAsync(Guid schoolId)
        {
            try
            {
                _logger.LogInformation("Buscando configuración de email para SchoolId: {SchoolId}", schoolId);
                
                var configuration = await _context.EmailConfigurations
                    .Include(ec => ec.School)
                    .FirstOrDefaultAsync(ec => ec.SchoolId == schoolId);

                if (configuration != null)
                {
                    _logger.LogInformation("Configuración encontrada: ID={ConfigId}, SMTP={SmtpServer}, Puerto={SmtpPort}", 
                        configuration.Id, configuration.SmtpServer, configuration.SmtpPort);
                }
                else
                {
                    _logger.LogInformation("No se encontró configuración de email para SchoolId: {SchoolId}", schoolId);
                }

                return configuration != null ? MapToDto(configuration) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar configuración de email para SchoolId: {SchoolId}", schoolId);
                throw;
            }
        }

        public async Task<EmailConfigurationDto?> GetActiveBySchoolIdAsync(Guid schoolId)
        {
            var configuration = await _context.EmailConfigurations
                .Include(ec => ec.School)
                .FirstOrDefaultAsync(ec => ec.SchoolId == schoolId && ec.IsActive);

            return configuration != null ? MapToDto(configuration) : null;
        }

        public async Task<EmailConfigurationDto> CreateAsync(EmailConfigurationCreateDto createDto)
        {
            try
            {
                _logger.LogInformation("Creando nueva configuración de email para SchoolId: {SchoolId}", createDto.SchoolId);
                
                var configuration = new EmailConfiguration
                {
                    Id = Guid.NewGuid(),
                    SchoolId = createDto.SchoolId,
                    SmtpServer = createDto.SmtpServer,
                    SmtpPort = createDto.SmtpPort,
                    SmtpUsername = createDto.SmtpUsername,
                    SmtpPassword = createDto.SmtpPassword,
                    SmtpUseSsl = createDto.SmtpUseSsl,
                    SmtpUseTls = createDto.SmtpUseTls,
                    FromEmail = createDto.FromEmail,
                    FromName = createDto.FromName,
                    IsActive = createDto.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _logger.LogInformation("Agregando configuración a la base de datos: ID={ConfigId}, SMTP={SmtpServer}", 
                    configuration.Id, configuration.SmtpServer);
                
                _context.EmailConfigurations.Add(configuration);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Configuración guardada exitosamente en la base de datos");

                var result = await GetByIdAsync(configuration.Id);
                if (result == null)
                {
                    _logger.LogError("Error: No se pudo recuperar la configuración recién creada con ID: {ConfigId}", configuration.Id);
                    throw new InvalidOperationException("Error al crear la configuración de email");
                }
                
                _logger.LogInformation("Configuración creada exitosamente: ID={ConfigId}", result.Id);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear configuración de email para SchoolId: {SchoolId}", createDto.SchoolId);
                throw;
            }
        }

        public async Task<EmailConfigurationDto> UpdateAsync(EmailConfigurationUpdateDto updateDto)
        {
            var configuration = await _context.EmailConfigurations
                .FirstOrDefaultAsync(ec => ec.Id == updateDto.Id);

            if (configuration == null)
                throw new ArgumentException("Configuración de email no encontrada");

            if (!SchoolTenantHelper.CanAccessResource(configuration.SchoolId, _tenantContext))
                throw new UnauthorizedAccessException("No tiene permisos para modificar la configuración de otra escuela.");

            configuration.SmtpServer = updateDto.SmtpServer;
            configuration.SmtpPort = updateDto.SmtpPort;
            configuration.SmtpUsername = updateDto.SmtpUsername;
            configuration.SmtpPassword = updateDto.SmtpPassword;
            configuration.SmtpUseSsl = updateDto.SmtpUseSsl;
            configuration.SmtpUseTls = updateDto.SmtpUseTls;
            configuration.FromEmail = updateDto.FromEmail;
            configuration.FromName = updateDto.FromName;
            configuration.IsActive = updateDto.IsActive;
            configuration.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return await GetByIdAsync(configuration.Id) ?? throw new InvalidOperationException("Error al actualizar la configuración de email");
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var configuration = await _context.EmailConfigurations
                .FirstOrDefaultAsync(ec => ec.Id == id);

            if (configuration == null)
                return false;

            if (!SchoolTenantHelper.CanAccessResource(configuration.SchoolId, _tenantContext))
                throw new UnauthorizedAccessException("No tiene permisos para eliminar la configuración de otra escuela.");

            _context.EmailConfigurations.Remove(configuration);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> TestConnectionAsync(Guid id)
        {
            var configuration = await _context.EmailConfigurations
                .FirstOrDefaultAsync(ec => ec.Id == id);

            if (configuration == null)
                return false;

            if (!SchoolTenantHelper.CanAccessResource(configuration.SchoolId, _tenantContext))
                return false;

            return await TestSmtpConnection(configuration);
        }

        public async Task<bool> TestConnectionBySchoolIdAsync(Guid schoolId)
        {
            var configuration = await _context.EmailConfigurations
                .FirstOrDefaultAsync(ec => ec.SchoolId == schoolId && ec.IsActive);

            if (configuration == null)
                return false;

            return await TestSmtpConnection(configuration);
        }

        private async Task<bool> TestSmtpConnection(EmailConfiguration configuration)
        {
            try
            {
                _logger.LogInformation("Iniciando prueba de conexión SMTP");
                _logger.LogInformation("Configuración: Servidor={SmtpServer}, Puerto={SmtpPort}, Usuario={SmtpUsername}, SSL={SmtpUseSsl}, TLS={SmtpUseTls}", 
                    configuration.SmtpServer, configuration.SmtpPort, configuration.SmtpUsername, configuration.SmtpUseSsl, configuration.SmtpUseTls);
                
                // Limpiar credenciales de espacios ocultos
                var cleanUsername = configuration.SmtpUsername?.Trim() ?? string.Empty;
                var cleanPassword = configuration.SmtpPassword?.Trim() ?? string.Empty;
                
                _logger.LogInformation("Credenciales limpias - Usuario: '{Username}' (longitud: {UserLength}), Contraseña: '{Password}' (longitud: {PassLength})", 
                    cleanUsername, cleanUsername.Length, 
                    string.IsNullOrEmpty(cleanPassword) ? "[VACÍA]" : "[OCULTA]", cleanPassword.Length);
                
                using var client = new SmtpClient(configuration.SmtpServer, configuration.SmtpPort);
                
                // Para Gmail con puerto 587, necesitamos SSL habilitado para STARTTLS
                // Si es Gmail y puerto 587, forzar SSL a true
                bool enableSsl = configuration.SmtpUseSsl;
                if (configuration.SmtpServer.ToLower().Contains("gmail") && configuration.SmtpPort == 587)
                {
                    enableSsl = true;
                    _logger.LogInformation("Detectado Gmail con puerto 587, forzando SSL a true para STARTTLS");
                }
                
                client.EnableSsl = enableSsl;
                client.UseDefaultCredentials = false; // CRÍTICO: debe ser false para Gmail
                client.Credentials = new NetworkCredential(cleanUsername, cleanPassword);
                
                _logger.LogInformation("Cliente SMTP configurado: SSL={EnableSsl}, UseDefaultCredentials={UseDefaultCredentials}, Credentials configuradas", 
                    client.EnableSsl, client.UseDefaultCredentials);

                _logger.LogInformation("Configurando cliente SMTP: SSL={EnableSsl}, Credentials configuradas", client.EnableSsl);

                // Crear un mensaje de prueba
                using var message = new MailMessage();
                message.From = new MailAddress(cleanUsername, configuration.FromName); // Usar el usuario limpio como From
                message.To.Add(cleanUsername); // Enviar a sí mismo para prueba
                message.Subject = "Prueba de configuración SMTP";
                message.Body = "Esta es una prueba de configuración SMTP. Si recibes este mensaje, la configuración es correcta.";
                message.IsBodyHtml = false;

                _logger.LogInformation("Enviando mensaje de prueba desde {FromEmail} a {ToEmail}", cleanUsername, cleanUsername);
                await client.SendMailAsync(message);
                _logger.LogInformation("Mensaje enviado exitosamente");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en prueba de conexión SMTP: {ErrorMessage}", ex.Message);
                _logger.LogError("Detalles del error: {ExceptionType} - {StackTrace}", ex.GetType().Name, ex.StackTrace);
                return false;
            }
        }

        private static EmailConfigurationDto MapToDto(EmailConfiguration configuration)
        {
            return new EmailConfigurationDto
            {
                Id = configuration.Id,
                SchoolId = configuration.SchoolId,
                SmtpServer = configuration.SmtpServer,
                SmtpPort = configuration.SmtpPort,
                SmtpUsername = configuration.SmtpUsername,
                SmtpPassword = configuration.SmtpPassword,
                SmtpUseSsl = configuration.SmtpUseSsl,
                SmtpUseTls = configuration.SmtpUseTls,
                FromEmail = configuration.FromEmail,
                FromName = configuration.FromName,
                IsActive = configuration.IsActive,
                CreatedAt = configuration.CreatedAt,
                UpdatedAt = configuration.UpdatedAt
            };
        }
    }
}
