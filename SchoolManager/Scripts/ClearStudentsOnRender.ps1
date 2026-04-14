<# 
 Script para eliminar estudiantes y sus asignaciones en la BD de Render.
 Usa la misma configuración de conexión que TestRenderConnection.ps1.
#>

$psqlCandidates = @(
    "C:\Program Files\PostgreSQL\18\bin\psql.exe",
    "C:\Program Files\PostgreSQL\16\bin\psql.exe"
)

$psqlPath = $null
foreach ($path in $psqlCandidates) {
    if (Test-Path $path) {
        $psqlPath = $path
        break
    }
}

if (-not $psqlPath) {
    Write-Host "❌ ERROR: psql.exe no encontrado en las rutas conocidas:" -ForegroundColor Red
    $psqlCandidates | ForEach-Object { Write-Host "   - $_" -ForegroundColor Yellow }
    exit 1
}

# Datos de conexión a Render (mismos que en TestRenderConnection.ps1 / SchoolDbContext)
$dbHost = "dpg-d7erln5ckfvc73en9obg-a.oregon-postgres.render.com"
$port = "5432"
$database = "schoolmanager_daqf"
$username = "admin"
$password = "iztY1ZL7WHbu2A5gtMSb1DFMhrK3Lo3r"

$env:PGPASSWORD = $password

Write-Host "═══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   ELIMINAR ESTUDIANTES Y SUS ASIGNACIONES (RENDER)" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "psql: $psqlPath" -ForegroundColor Gray
Write-Host "DB:   $database@$dbHost" -ForegroundColor Gray
Write-Host ""

# 1) Eliminar asignaciones de estudiantes
$deleteAssignments = @"
DELETE FROM student_assignments
WHERE student_id IN (
    SELECT id FROM users WHERE role IN ('student','estudiante')
);
"@

Write-Host "🧹 Eliminando asignaciones de estudiantes (student_assignments)..." -ForegroundColor Yellow
& $psqlPath -h $dbHost -p $port -U $username -d $database -c $deleteAssignments
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Error al eliminar de student_assignments. Abortando." -ForegroundColor Red
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    exit 1
}

# 2) Eliminar scores de actividades de estudiantes (si existen)
$deleteActivityScores = @"
DELETE FROM student_activity_scores
WHERE student_id IN (
    SELECT id FROM users WHERE role IN ('student','estudiante')
);
"@

Write-Host "🧹 Eliminando student_activity_scores (si existen)..." -ForegroundColor Yellow
& $psqlPath -h $dbHost -p $port -U $username -d $database -c $deleteActivityScores
if ($LASTEXITCODE -ne 0) {
    Write-Host "⚠️  No se pudieron eliminar student_activity_scores (puede que la tabla no exista o no tenga datos)." -ForegroundColor Yellow
}

# 3) Eliminar tokens QR e ID cards de estudiantes (en cascada o directo)
$deleteQrTokens = @"
DELETE FROM student_qr_tokens
WHERE student_id IN (
    SELECT id FROM users WHERE role IN ('student','estudiante')
);
"@

Write-Host "🧹 Eliminando student_qr_tokens (si existen)..." -ForegroundColor Yellow
& $psqlPath -h $dbHost -p $port -U $username -d $database -c $deleteQrTokens

$deleteIdCards = @"
DELETE FROM student_id_cards
WHERE student_id IN (
    SELECT id FROM users WHERE role IN ('student','estudiante')
);
"@

Write-Host "🧹 Eliminando student_id_cards (si existen)..." -ForegroundColor Yellow
& $psqlPath -h $dbHost -p $port -U $username -d $database -c $deleteIdCards

# 4) Eliminar otras entidades que referencian al estudiante (attendance, reports, prematriculations, scan_logs)
$deleteAttendance = @"
DELETE FROM attendance
WHERE student_id IN (
    SELECT id FROM users WHERE role IN ('student','estudiante')
);
"@

Write-Host "🧹 Eliminando registros de attendance..." -ForegroundColor Yellow
& $psqlPath -h $dbHost -p $port -U $username -d $database -c $deleteAttendance

$deleteDiscipline = @"
DELETE FROM discipline_reports
WHERE student_id IN (
    SELECT id FROM users WHERE role IN ('student','estudiante')
);
"@

Write-Host "🧹 Eliminando registros de discipline_reports..." -ForegroundColor Yellow
& $psqlPath -h $dbHost -p $port -U $username -d $database -c $deleteDiscipline

$deleteOrientation = @"
DELETE FROM orientation_reports
WHERE student_id IN (
    SELECT id FROM users WHERE role IN ('student','estudiante')
);
"@

Write-Host "🧹 Eliminando registros de orientation_reports..." -ForegroundColor Yellow
& $psqlPath -h $dbHost -p $port -U $username -d $database -c $deleteOrientation

$deletePrematriculations = @"
DELETE FROM prematriculations
WHERE student_id IN (
    SELECT id FROM users WHERE role IN ('student','estudiante')
);
"@

Write-Host "🧹 Eliminando registros de prematriculations..." -ForegroundColor Yellow
& $psqlPath -h $dbHost -p $port -U $username -d $database -c $deletePrematriculations

$deleteScanLogs = @"
DELETE FROM scan_logs
WHERE student_id IN (
    SELECT id FROM users WHERE role IN ('student','estudiante')
);
"@

Write-Host "🧹 Eliminando registros de scan_logs..." -ForegroundColor Yellow
& $psqlPath -h $dbHost -p $port -U $username -d $database -c $deleteScanLogs

# 5) Finalmente, eliminar los usuarios con rol estudiante
$deleteUsers = @"
DELETE FROM users
WHERE role IN ('student','estudiante');
"@

Write-Host "🧹 Eliminando usuarios con rol student/estudiante..." -ForegroundColor Yellow
& $psqlPath -h $dbHost -p $port -U $username -d $database -c $deleteUsers
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Error al eliminar usuarios con rol student/estudiante." -ForegroundColor Red
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    exit 1
}

Write-Host ""
Write-Host "✅ Estudiantes y sus asignaciones eliminados en Render (en lo posible)." -ForegroundColor Green

Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue

