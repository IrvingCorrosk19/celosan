using Microsoft.EntityFrameworkCore;
using SchoolManager.Mappings;
using SchoolManager.Models;
using AutoMapper;
using SchoolManager.Services.Implementations;
using SchoolManager.Options;
using SchoolManager.Services.Interfaces;
using SchoolManager.Application.Interfaces;
using SchoolManager.Infrastructure.Services;
using SchoolManager.Services;
using SchoolManager.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using BCrypt.Net;
using SchoolManager.Middleware;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SchoolManager.Repositories.Implementations;
using SchoolManager.Repositories.Interfaces;
using SchoolManager.Services.Background;
using SchoolManager.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Render / docs de Cloudinary suelen usar CLOUDINARY_CLOUD_NAME, CLOUDINARY_API_KEY, CLOUDINARY_API_SECRET.
// También vale Cloudinary__CloudName en el entorno. Hay que sobrescribir placeholders de appsettings (TU_… / …AQUI…).
static void ApplyCloudinaryEnvironmentAliases(ConfigurationManager config)
{
    static bool IsPlaceholderValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var t = value.Trim();
        return t.StartsWith("TU_", StringComparison.OrdinalIgnoreCase)
            || t.Contains("AQUI", StringComparison.OrdinalIgnoreCase);
    }

    void MapFromEnv(string configKey, string envKey)
    {
        var fromEnv = Environment.GetEnvironmentVariable(envKey);
        if (string.IsNullOrWhiteSpace(fromEnv)) return;
        var current = config[configKey];
        if (IsPlaceholderValue(current))
            config[configKey] = fromEnv.Trim();
    }

    MapFromEnv("Cloudinary:CloudName", "CLOUDINARY_CLOUD_NAME");
    MapFromEnv("Cloudinary:ApiKey", "CLOUDINARY_API_KEY");
    MapFromEnv("Cloudinary:ApiSecret", "CLOUDINARY_API_SECRET");
    // Mismo criterio si usan nombres .NET en Render (Cloudinary__CloudName, etc.)
    MapFromEnv("Cloudinary:CloudName", "Cloudinary__CloudName");
    MapFromEnv("Cloudinary:ApiKey", "Cloudinary__ApiKey");
    MapFromEnv("Cloudinary:ApiSecret", "Cloudinary__ApiSecret");
}

ApplyCloudinaryEnvironmentAliases(builder.Configuration);

// Render: usar PORT si está definido (producción en Render.com)
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Tabla email_queues (cola de envío de contraseñas por correo). Idempotente.
if (args.Length > 0 && args[0] == "--apply-email-queues-table")
{
    var connStr = PostgresConnectionResolver.Resolve(builder.Configuration);
    if (string.IsNullOrEmpty(connStr)) { Console.WriteLine("Falta conexión: DefaultConnection, ConnectionStrings__DefaultConnection o DATABASE_URL."); Environment.Exit(1); return; }
    var opts = new DbContextOptionsBuilder<SchoolDbContext>().UseNpgsql(connStr).Options;
    using var ctx = new SchoolDbContext(opts);
    await SchoolManager.Scripts.ApplyEmailQueuesTable.RunAsync(ctx);
    return;
}

// Aplicar columna schools.is_active sin arrancar la app (evita usar Schools antes de que exista la columna)
if (args.Length > 0 && args[0] == "--apply-email-jobs")
{
    var connStr = PostgresConnectionResolver.Resolve(builder.Configuration);
    if (string.IsNullOrEmpty(connStr)) { Console.WriteLine("Falta conexión: DefaultConnection, ConnectionStrings__DefaultConnection o DATABASE_URL."); Environment.Exit(1); return; }
    var opts = new DbContextOptionsBuilder<SchoolDbContext>().UseNpgsql(connStr).Options;
    using var ctx = new SchoolDbContext(opts);
    await SchoolManager.Scripts.ApplyEmailJobsAndQueueColumns.RunAsync(ctx);
    Console.WriteLine("✅ email_jobs y columnas de email_queues aplicados. Saliendo...");
    return;
}

if (args.Length > 0 && args[0] == "--apply-school-is-active")
{
    var connStr = PostgresConnectionResolver.Resolve(builder.Configuration);
    if (string.IsNullOrEmpty(connStr)) { Console.WriteLine("Falta conexión: DefaultConnection, ConnectionStrings__DefaultConnection o DATABASE_URL."); Environment.Exit(1); return; }
    var opts = new DbContextOptionsBuilder<SchoolDbContext>().UseNpgsql(connStr).Options;
    using var ctx = new SchoolDbContext(opts);
    await SchoolManager.Scripts.ApplySchoolIsActive.RunAsync(ctx);
    Console.WriteLine("✅ Columna schools.is_active aplicada y migración registrada. Saliendo...");
    return;
}

// Crear tablas Plan de Trabajo Trimestral (teacher_work_plans, teacher_work_plan_details)
if (args.Length > 0 && args[0] == "--apply-teacher-work-plan-tables")
{
    var connStr = PostgresConnectionResolver.Resolve(builder.Configuration);
    if (string.IsNullOrEmpty(connStr)) { Console.WriteLine("Falta conexión: DefaultConnection, ConnectionStrings__DefaultConnection o DATABASE_URL."); Environment.Exit(1); return; }
    var opts = new DbContextOptionsBuilder<SchoolDbContext>().UseNpgsql(connStr).Options;
    using var ctx = new SchoolDbContext(opts);
    await SchoolManager.Scripts.ApplyTeacherWorkPlanTables.RunAsync(ctx);
    return;
}

// Columnas gobernanza + tabla teacher_work_plan_review_logs (Dirección Académica)
if (args.Length > 0 && args[0] == "--apply-director-work-plan-governance")
{
    var connStr = PostgresConnectionResolver.Resolve(builder.Configuration);
    if (string.IsNullOrEmpty(connStr)) { Console.WriteLine("Falta conexión: DefaultConnection, ConnectionStrings__DefaultConnection o DATABASE_URL."); Environment.Exit(1); return; }
    var opts = new DbContextOptionsBuilder<SchoolDbContext>().UseNpgsql(connStr).Options;
    using var ctx = new SchoolDbContext(opts);
    await SchoolManager.Scripts.ApplyDirectorWorkPlanGovernance.RunAsync(ctx);
    return;
}

// Crear superadmin inicial (superadmin@schoolmanager.com / Admin123!). Usa la conexión configurada.
if (args.Length > 0 && args[0] == "--create-initial-superadmin")
{
    var connStr = PostgresConnectionResolver.Resolve(builder.Configuration);
    if (string.IsNullOrEmpty(connStr)) { Console.WriteLine("Falta conexión: DefaultConnection, ConnectionStrings__DefaultConnection o DATABASE_URL."); Environment.Exit(1); return; }
    var opts = new DbContextOptionsBuilder<SchoolDbContext>().UseNpgsql(connStr).Options;
    using var ctx = new SchoolDbContext(opts);
    await SchoolManager.Scripts.CreateInitialSuperAdminScript.RunAsync(ctx);
    return;
}

// Crear admin local (admin@local.com / Admin123!). Crea escuela si no existe.
if (args.Length > 0 && args[0] == "--create-local-admin")
{
    var connStr = PostgresConnectionResolver.Resolve(builder.Configuration);
    if (string.IsNullOrEmpty(connStr)) { Console.WriteLine("Falta conexión: DefaultConnection, ConnectionStrings__DefaultConnection o DATABASE_URL."); Environment.Exit(1); return; }
    var opts = new DbContextOptionsBuilder<SchoolDbContext>().UseNpgsql(connStr).Options;
    using var ctx = new SchoolDbContext(opts);
    await SchoolManager.Scripts.CreateLocalAdminScript.RunAsync(ctx);
    return;
}

// Crear tabla student_payment_access en Render (módulo Club de Padres). No arranca la app.
if (args.Length > 0 && args[0] == "--apply-render-student-payment-access")
{
    await SchoolManager.Scripts.ApplyRenderStudentPaymentAccess.RunAsync();
    return;
}

// Homologar BD LOCAL con Render. Solo para desarrollo local.
if (args.Length > 0 && args[0] == "--homologate-local")
{
    Console.WriteLine("═════════════════════════════════════════════════");
    Console.WriteLine("   COMANDO --homologate-local DESACTIVADO");
    Console.WriteLine("═════════════════════════════════════════════════\n");
    return;
}

// Cultura oficial del sistema (estándar corporativo de fechas)
var culture = new CultureInfo("es-PA");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

// Add services to the container (una sola cadena: vistas + JSON camelCase para Ok()/fetch)
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new DateTimeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new NullableDateTimeJsonConverter());
    })
    .AddMvcOptions(options =>
    {
        options.Filters.Add<SchoolManager.Attributes.DateTimeConversionAttribute>();
        options.Filters.Add<SchoolManager.Filters.PlatformAccessGuardFilter>();
    });

// Configurar Antiforgery para aceptar el token desde header (usado por fetch en Schedule y otros módulos AJAX)
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

// Conexión a la base de datos PostgreSQL (appsettings, ConnectionStrings__DefaultConnection o DATABASE_URL en Render)
var npgsqlConnectionString = PostgresConnectionResolver.Resolve(builder.Configuration)
    ?? throw new InvalidOperationException(
        "Falta cadena de base de datos. Configure ConnectionStrings:DefaultConnection, la variable de entorno ConnectionStrings__DefaultConnection o DATABASE_URL (Render PostgreSQL).");
builder.Services.AddDbContext<SchoolDbContext>(options =>
{
    options.UseNpgsql(npgsqlConnectionString);

    // Configurar Entity Framework para manejar DateTime automáticamente
    options.ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.RowLimitingOperationWithoutOrderByWarning));
});

// Registrando todos los servicios con inyección de dependencias
builder.Services.AddScoped<ISchoolService, SchoolService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ISubjectService, SubjectService>();
builder.Services.AddScoped<IGroupService, GroupService>();
builder.Services.AddScoped<ITeacherAssignmentService, TeacherAssignmentService>();
builder.Services.AddScoped<ITrimesterService, TrimesterService>();
builder.Services.AddScoped<IActivityTypeService, ActivityTypeService>();
builder.Services.AddScoped<ITeacherGroupService, TeacherGroupService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddScoped<IStudentActivityScoreService, StudentActivityScoreService>();

builder.Services.AddSingleton<IFileStorage, LocalFileStorage>(); // o tu propio servicio

builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.AddSingleton<IDocumentStorageService, DocumentStorageService>();

builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IDisciplineReportService, DisciplineReportService>();
builder.Services.AddScoped<IOrientationReportService, OrientationReportService>();
builder.Services.AddScoped<ISecuritySettingService, SecuritySettingService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

builder.Services.AddAutoMapper(_ => { }, typeof(AutoMapperProfile).Assembly);
builder.Services.AddScoped<IStudentReportService, StudentReportService>();
builder.Services.AddScoped<IGradeLevelService, GradeLevelService>();
builder.Services.AddScoped<IAcademicAssignmentService, AcademicAssignmentService>();
builder.Services.AddScoped<IStudentAssignmentService, StudentAssignmentService>();
builder.Services.AddScoped<IAreaService, AreaService>();
builder.Services.AddScoped<ISpecialtyService, SpecialtyService>();
builder.Services.AddScoped<IShiftService, ShiftService>();
builder.Services.AddScoped<ISubjectAssignmentService, SubjectAssignmentService>();
builder.Services.AddScoped<IDirectorService, DirectorService>();
builder.Services.AddScoped<ISuperAdminService, SuperAdminService>();
builder.Services.AddScoped<IDateTimeHomologationService, DateTimeHomologationService>();
builder.Services.AddScoped<IEmailConfigurationService, EmailConfigurationService>();
builder.Services.AddScoped<IEmailApiConfigurationService, EmailApiConfigurationService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ICounselorAssignmentService, CounselorAssignmentService>();
builder.Services.AddScoped<IStudentProfileService, StudentProfileService>();
builder.Services.AddScoped<IMessagingService, MessagingService>();
builder.Services.AddScoped<IAprobadosReprobadosService, AprobadosReprobadosService>();
builder.Services.AddScoped<IPrematriculationPeriodService, PrematriculationPeriodService>();
builder.Services.AddScoped<IPrematriculationService, PrematriculationService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IPaymentConceptService, PaymentConceptService>();
builder.Services.AddScoped<IAcademicYearService, AcademicYearService>();
builder.Services.AddScoped<IScheduleService, ScheduleService>();
builder.Services.AddScoped<IScheduleConfigurationService, ScheduleConfigurationService>();
builder.Services.Configure<SchoolManager.Services.Security.QrSecurityOptions>(
    builder.Configuration.GetSection(SchoolManager.Services.Security.QrSecurityOptions.SectionName));
builder.Services.Configure<StudentIdCardPdfPrintOptions>(
    builder.Configuration.GetSection(StudentIdCardPdfPrintOptions.SectionName));
builder.Services.Configure<StudentIdCardOptions>(
    builder.Configuration.GetSection(StudentIdCardOptions.SectionName));
builder.Services.AddSingleton<SchoolManager.Services.Security.IQrSignatureService, SchoolManager.Services.Security.QrSignatureService>();

// SEG-2: Rate limiting para el endpoint público de escaneo QR.
// Límite por IP: 60 peticiones/minuto en ventana fija.
// Previene brute force de tokens, enumeración masiva y DoS de scan_logs.
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("ScanApiPolicy", limiter =>
    {
        limiter.PermitLimit = 60;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 0; // sin cola — rechazar inmediatamente cuando se supera el límite
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
builder.Services.AddScoped<IStudentIdCardService, StudentIdCardService>();
builder.Services.AddScoped<IStudentIdCardImageService, StudentIdCardImageService>();
builder.Services.AddScoped<IStudentIdCardPdfService, StudentIdCardPdfService>();
builder.Services.AddScoped<IStudentIdCardHtmlCaptureService, StudentIdCardHtmlCaptureService>();
builder.Services.AddScoped<ITeacherWorkPlanService, TeacherWorkPlanService>();
builder.Services.AddScoped<ITeacherWorkPlanPdfService, TeacherWorkPlanPdfService>();
builder.Services.AddScoped<IDirectorWorkPlanService, DirectorWorkPlanService>();
builder.Services.AddScoped<IUserPasswordManagementService, UserPasswordManagementService>();
builder.Services.AddScoped<IBulkPasswordEmailService, BulkPasswordEmailService>();
builder.Services.AddScoped<IEmailQueueRepository, EmailQueueRepository>();
builder.Services.AddScoped<IEmailQueueService, EmailQueueService>();
builder.Services.AddScoped<IEmailSender, ResendEmailSender>();
builder.Services.AddScoped<IEmailJobService, EmailJobService>();
builder.Services.AddHostedService<EmailQueueWorker>();
// Módulo Club de Padres (pagos carnet y plataforma)
builder.Services.AddScoped<IClubParentsPaymentService, ClubParentsPaymentService>();
builder.Services.AddScoped<IQlServicesCarnetService, QlServicesCarnetService>();
builder.Services.AddScoped<IPlatformAccessGuardService, PlatformAccessGuardService>();
builder.Services.AddScoped<SchoolManager.Filters.PlatformAccessGuardFilter>();

// HttpClient (p. ej. descarga de fotos en Cloudinary para PDFs)
builder.Services.AddHttpClient();

builder.Services.Configure<UserPhotoCacheOptions>(
    builder.Configuration.GetSection(UserPhotoCacheOptions.SectionName));
var userPhotoCacheBootstrap = builder.Configuration.GetSection(UserPhotoCacheOptions.SectionName)
    .Get<UserPhotoCacheOptions>() ?? new UserPhotoCacheOptions();
builder.Services.AddMemoryCache(o =>
{
    o.SizeLimit = Math.Clamp(userPhotoCacheBootstrap.MemoryCacheSizeLimitBytes, 16 * 1024 * 1024, 512 * 1024 * 1024);
});
builder.Services.AddSingleton<IHttpBytesDownloadCache, HttpBytesDownloadCache>();

// Cloudinary: credenciales reales en producción (variables de entorno / Render) para que las fotos sobrevivan al deploy
builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();

// Fotos de usuario: solo Cloudinary (LocalFileStorageService; sin copia en disco al subir).
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IUserPhotoService, UserPhotoService>();

// Agregar servicios de autenticación
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.SlidingExpiration = true;
    });

// Agregar configuración de autorización
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdmin", policy => policy.RequireRole("SuperAdmin"));
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("Teacher", policy => policy.RequireRole("Teacher"));
    options.AddPolicy("Student", policy => policy.RequireRole("Student"));
    options.AddPolicy("Parent", policy => policy.RequireRole("Parent", "Acudiente"));
    options.AddPolicy("Accounting", policy => policy.RequireRole("Contabilidad", "Admin", "SuperAdmin"));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<ITimeZoneService, TimeZoneService>();

var app = builder.Build();

// Asegurar que existan las tablas del módulo de carnets (por si la migración no se aplicó)
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var cloudinary = scope.ServiceProvider.GetRequiredService<ICloudinaryService>();
    if (!cloudinary.IsConfigured)
    {
        // No detenemos el arranque; sin credenciales válidas SaveUserPhotoAsync fallará (solo Cloudinary).
        logger.LogCritical(
            "Cloudinary no está configurado o las credenciales son placeholders. " +
            "Defina CLOUDINARY_CLOUD_NAME, CLOUDINARY_API_KEY y CLOUDINARY_API_SECRET " +
            "(o Cloudinary__CloudName, Cloudinary__ApiKey, Cloudinary__ApiSecret). " +
            "Sin eso, la subida de fotos de usuario fallará.");
    }

    var db = scope.ServiceProvider.GetRequiredService<SchoolDbContext>();
    await SchoolManager.Scripts.EnsureIdCardTables.EnsureAsync(db);
    await SchoolManager.Scripts.EnsureUsersRoleCheck.EnsureAsync(db);
    await SchoolManager.Scripts.EnsureStudentPaymentAccessTable.EnsureAsync(db);
    await SchoolManager.Scripts.EnsureScheduleTables.EnsureAsync(db);
    await SchoolManager.Scripts.EnsureSchoolScheduleConfigurationTable.EnsureAsync(db);
    await SchoolManager.Scripts.ApplyEmailJobsAndQueueColumns.RunAsync(db);
    await SchoolManager.Scripts.VerifyAcademicYearsInDb.RunAsync(db, logger);

    // Garantizar que cada escuela tenga al menos un año académico (evitar mensaje "No hay años académicos configurados")
    var academicYearService = scope.ServiceProvider.GetRequiredService<IAcademicYearService>();
    var schools = await db.Schools.Select(s => s.Id).ToListAsync();
    foreach (var schoolId in schools)
    {
        try
        {
            await academicYearService.EnsureDefaultAcademicYearForSchoolAsync(schoolId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo asegurar año académico para la escuela {SchoolId}.", schoolId);
        }
    }

    // Garantizar que cada escuela tenga bloques horarios por defecto (8 bloques de 35 min desde 07:00) si no tiene ninguno
    try
    {
        foreach (var schoolId in schools)
        {
            await SchoolManager.Scripts.EnsureDefaultTimeSlots.EnsureForSchoolAsync(db, schoolId);
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "No se pudo asegurar bloques horarios por defecto (tabla time_slots puede no existir aún).");
    }
}

// Script temporal para aplicar cambios a la base de datos
// Ejecutar con: 
//   --apply-db-changes: Aplica cambios locales
//   --apply-school-is-active: Añade columna schools.is_active (Soft Delete) y registra migración
//   --apply-academic-year: Aplica cambios de año académico locales
//   --test-render: Prueba conexión a Render
//   --apply-render-all: Aplica todas las migraciones a Render
//   --apply-render-prematriculation: Aplica solo prematriculación a Render
//   --apply-render-academic-year: Aplica solo año académico a Render
if (args.Length > 0)
{
    if (args[0] == "--test-render")
    {
        await SchoolManager.Scripts.TestRenderConnection.RunAsync();
        return;
    }
    else if (args[0] == "--apply-render-all")
    {
        await SchoolManager.Scripts.ApplyRenderMigrations.ApplyAllMigrationsAsync();
        return;
    }
    else if (args[0] == "--apply-render-prematriculation")
    {
        await SchoolManager.Scripts.ApplyRenderMigrations.ApplyPrematriculationOnlyAsync();
        return;
    }
    else if (args[0] == "--apply-render-academic-year")
    {
        await SchoolManager.Scripts.ApplyRenderMigrations.ApplyAcademicYearOnlyAsync();
        return;
    }
    else if (args[0] == "--compare-db-schemas")
    {
        await SchoolManager.Scripts.CompareDbSchemas.RunAsync();
        return;
    }
    else if (args[0] == "--sync-ef-migrations-history")
    {
        var connStr = PostgresConnectionResolver.Resolve(builder.Configuration);
        if (string.IsNullOrEmpty(connStr)) { Console.WriteLine("Falta conexión: DefaultConnection, ConnectionStrings__DefaultConnection o DATABASE_URL."); return; }
        var label = builder.Environment.IsDevelopment() ? "LOCAL" : "RENDER";
        Console.WriteLine($"Sincronizando __EFMigrationsHistory en {label}...\n");
        await SchoolManager.Scripts.SyncEfMigrationsHistory.RunAsync(connStr, label);
        Console.WriteLine("\n✅ Listo. Comprueba con: dotnet ef migrations list");
        return;
    }
    else if (args[0] == "--sync-ef-migrations-both")
    {
        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine("   COMANDO --sync-ef-migrations-both DESACTIVADO");
        Console.WriteLine("═══════════════════════════════════════════════\n");
        return;
    }
    else if (args[0] == "--list-local-tables")
    {
        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine("   COMANDO --list-local-tables DESACTIVADO");
        Console.WriteLine("═════════════════════════════════════════════════\n");
        return;
    }
    else if (args[0] == "--add-render-indexes")
    {
        await SchoolManager.Scripts.AddRenderIndexes.RunAsync();
        return;
    }
    // Comandos locales (usando la conexión del appsettings.json)
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<SchoolDbContext>();
    
    if (args[0] == "--apply-db-changes")
    {
        await SchoolManager.Scripts.ApplyDatabaseChanges.ApplyPrematriculationChangesAsync(context);
        Console.WriteLine("✅ Cambios de prematriculación aplicados. Saliendo...");
        return;
    }
    else if (args[0] == "--apply-academic-year")
    {
        await SchoolManager.Scripts.ApplyAcademicYearChanges.ApplyAsync(context);
        Console.WriteLine("✅ Cambios de año académico aplicados. Saliendo...");
        return;
    }
    else if (args[0] == "--check-users")
    {
        await SchoolManager.Scripts.CheckUsers.RunAsync(context);
        return;
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();

app.UseRouting();

// SEG-2: Rate limiter para endpoints con [EnableRateLimiting] (ej: /api/scan)
app.UseRateLimiter();

// Agregar middleware global para DateTime
app.UseMiddleware<DateTimeMiddleware>();

app.UseAuthentication();
app.UseMiddleware<SchoolManager.Middleware.ApiBearerTokenMiddleware>();
app.UseAuthorization();

// Usar el método de extensión para el middleware
// app.UseSessionValidation();

app.MapControllers(); // Rutas por atributos (ej. StudentIdCard/ui)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();
