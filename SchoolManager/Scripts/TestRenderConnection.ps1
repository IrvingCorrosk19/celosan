# Script PowerShell para probar la conexión a Render usando psql
# Ruta del cliente PostgreSQL
$psqlPath = "C:\Program Files\PostgreSQL\18\bin\psql.exe"

# Datos de conexión a Render
$host = "dpg-d7erln5ckfvc73en9obg-a.oregon-postgres.render.com"
$port = "5432"
$database = "schoolmanager_daqf"
$username = "admin"
$password = "iztY1ZL7WHbu2A5gtMSb1DFMhrK3Lo3r"

# Variable de entorno para la contraseña (psql la puede leer desde PGPASSWORD)
$env:PGPASSWORD = $password

Write-Host "═══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   VERIFICACIÓN DE CONEXIÓN A RENDER (PRODUCCIÓN)" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Verificar que psql existe
if (-Not (Test-Path $psqlPath)) {
    Write-Host "❌ ERROR: psql.exe no encontrado en: $psqlPath" -ForegroundColor Red
    Write-Host "   Verifica que PostgreSQL 18 esté instalado" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ psql encontrado en: $psqlPath" -ForegroundColor Green
Write-Host ""

# Construir cadena de conexión
$connectionString = "-h $host -p $port -U $username -d $database -c"

Write-Host "🔍 Intentando conectar a Render..." -ForegroundColor Yellow
Write-Host "   Host: $host" -ForegroundColor Gray
Write-Host "   Database: $database" -ForegroundColor Gray
Write-Host "   User: $username" -ForegroundColor Gray
Write-Host ""

# Comando para probar la conexión y obtener información
$testQuery = @"
SELECT 
    version() as postgres_version,
    current_database() as current_database,
    current_user as current_user,
    now() as server_time;
"@

Write-Host "📊 Ejecutando consulta de prueba..." -ForegroundColor Yellow
try {
    $result = & $psqlPath -h $host -p $port -U $username -d $database -c $testQuery 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host $result -ForegroundColor Green
        Write-Host ""
        Write-Host "✅ Conexión exitosa a Render!" -ForegroundColor Green
    } else {
        Write-Host "❌ Error al conectar:" -ForegroundColor Red
        Write-Host $result -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ Error al ejecutar psql: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   VERIFICANDO ESTADO DE TABLAS Y COLUMNAS" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Verificar tablas clave
$tablesToCheck = @(
    "academic_years",
    "prematriculation_histories",
    "student_assignments",
    "student_activity_scores",
    "trimesters"
)

foreach ($table in $tablesToCheck) {
    $checkTableQuery = "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '$table');"
    try {
        $exists = & $psqlPath -h $host -p $port -U $username -d $database -t -c $checkTableQuery 2>&1
        
        if ($exists -match "t|true|1") {
            Write-Host "   ✅ Tabla '$table' existe" -ForegroundColor Green
        } else {
            Write-Host "   ❌ Tabla '$table' NO existe" -ForegroundColor Red
        }
    } catch {
        Write-Host "   ⚠️  No se pudo verificar tabla '$table': $_" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   VERIFICANDO COLUMNAS ESPECÍFICAS" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Verificar columnas específicas
$columnsToCheck = @(
    @{Table = "student_assignments"; Column = "academic_year_id"},
    @{Table = "student_assignments"; Column = "is_active"},
    @{Table = "student_activity_scores"; Column = "academic_year_id"},
    @{Table = "trimesters"; Column = "academic_year_id"},
    @{Table = "prematriculation_periods"; Column = "required_amount"}
)

foreach ($item in $columnsToCheck) {
    $table = $item.Table
    $column = $item.Column
    $checkColumnQuery = "SELECT EXISTS (SELECT FROM information_schema.columns WHERE table_schema = 'public' AND table_name = '$table' AND column_name = '$column');"
    
    try {
        $exists = & $psqlPath -h $host -p $port -U $username -d $database -t -c $checkColumnQuery 2>&1
        
        if ($exists -match "t|true|1") {
            Write-Host "   ✅ $table.$column existe" -ForegroundColor Green
        } else {
            Write-Host "   ❌ $table.$column NO existe" -ForegroundColor Red
        }
    } catch {
        Write-Host "   ⚠️  No se pudo verificar $table.$column: $_" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   RESUMEN" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "✅ Conexión verificada exitosamente" -ForegroundColor Green
Write-Host ""
Write-Host "📝 Próximos pasos:" -ForegroundColor Yellow
Write-Host "   1. Ejecuta: dotnet run -- --test-render" -ForegroundColor White
Write-Host "   2. O ejecuta: dotnet run -- --apply-render-all" -ForegroundColor White
Write-Host ""

# Limpiar variable de entorno
Remove-Item Env:PGPASSWORD

