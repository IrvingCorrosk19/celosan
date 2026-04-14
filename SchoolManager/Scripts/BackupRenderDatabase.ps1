# Script PowerShell para hacer backup de la base de datos de Render y restaurarla localmente
# Requiere PostgreSQL 18 instalado en C:\Program Files\PostgreSQL\18\bin

$psqlPath = "C:\Program Files\PostgreSQL\18\bin\psql.exe"
$pgDumpPath = "C:\Program Files\PostgreSQL\18\bin\pg_dump.exe"

# Datos de conexion a Render (PRODUCCION)
$renderHost = "dpg-d7erln5ckfvc73en9obg-a.oregon-postgres.render.com"
$renderPort = "5432"
$renderDatabase = "schoolmanager_daqf"
$renderUsername = "admin"
$renderPassword = "iztY1ZL7WHbu2A5gtMSb1DFMhrK3Lo3r"

# Datos de conexion LOCAL
$localHost = "localhost"
$localPort = "5432"
$localDatabase = "schoolmanagement"
$localUsername = "postgres"
$localPassword = "Panama2020$"

# Ruta para guardar el backup
$backupDir = ".\Backups"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFile = Join-Path $backupDir "render_backup_$timestamp.sql"

Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "   BACKUP DE BASE DE DATOS DE RENDER" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host ""

# Verificar que las herramientas existen
if (-Not (Test-Path $pgDumpPath)) {
    Write-Host "ERROR: pg_dump.exe no encontrado en: $pgDumpPath" -ForegroundColor Red
    exit 1
}

if (-Not (Test-Path $psqlPath)) {
    Write-Host "ERROR: psql.exe no encontrado en: $psqlPath" -ForegroundColor Red
    exit 1
}

# Crear directorio de backups si no existe
if (-Not (Test-Path $backupDir)) {
    New-Item -ItemType Directory -Path $backupDir | Out-Null
    Write-Host "Directorio de backups creado: $backupDir" -ForegroundColor Green
}

Write-Host "Configuracion:" -ForegroundColor Yellow
Write-Host "   Origen (Render): $renderDatabase en $renderHost" -ForegroundColor Gray
Write-Host "   Destino (Local): $localDatabase en $localHost" -ForegroundColor Gray
Write-Host "   Archivo backup: $backupFile" -ForegroundColor Gray
Write-Host ""

# Paso 1: Hacer backup de Render
Write-Host "Paso 1: Creando backup de Render..." -ForegroundColor Yellow
$env:PGPASSWORD = $renderPassword

try {
    # Backup en formato SQL (texto plano, mas facil de restaurar)
    Write-Host "   Creando backup SQL..." -ForegroundColor Gray
    & $pgDumpPath `
        -h $renderHost `
        -p $renderPort `
        -U $renderUsername `
        -d $renderDatabase `
        --no-owner `
        --no-privileges `
        --clean `
        --if-exists `
        --verbose `
        -f $backupFile 2>&1 | Out-Null

    if ($LASTEXITCODE -eq 0) {
        $fileSize = (Get-Item $backupFile).Length / 1MB
        Write-Host "   OK Backup SQL creado: $backupFile ($([math]::Round($fileSize, 2)) MB)" -ForegroundColor Green
    } else {
        Write-Host "   ERROR al crear backup SQL" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "   ERROR al crear backup: $_" -ForegroundColor Red
    exit 1
} finally {
    Remove-Item Env:PGPASSWORD
}

Write-Host ""

# Paso 2: Verificar conexion local
Write-Host "Paso 2: Verificando conexion a base de datos local..." -ForegroundColor Yellow
$env:PGPASSWORD = $localPassword

try {
    $testQuery = "SELECT 1;"
    $result = & $psqlPath -h $localHost -p $localPort -U $localUsername -d postgres -c $testQuery 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   OK Conexion local exitosa" -ForegroundColor Green
    } else {
        Write-Host "   ERROR: No se puede conectar a la base de datos local" -ForegroundColor Red
        Write-Host "   Verifica que PostgreSQL este corriendo y las credenciales sean correctas" -ForegroundColor Yellow
        Remove-Item Env:PGPASSWORD
        exit 1
    }
} catch {
    Write-Host "   ERROR al verificar conexion local: $_" -ForegroundColor Red
    Remove-Item Env:PGPASSWORD
    exit 1
}

Write-Host ""

# Paso 3: Preguntar si quiere restaurar
Write-Host "Paso 3: Deseas restaurar el backup en la base de datos local?" -ForegroundColor Yellow
Write-Host "   ADVERTENCIA: Esto eliminara todos los datos actuales en '$localDatabase'" -ForegroundColor Red
$restore = Read-Host "   Restaurar? (S/N)"

if ($restore -eq "S" -or $restore -eq "s" -or $restore -eq "SI" -or $restore -eq "si") {
    Write-Host ""
    Write-Host "Paso 4: Restaurando backup en base de datos local..." -ForegroundColor Yellow
    
    try {
        # Primero, eliminar la base de datos si existe (con todas las conexiones cerradas)
        Write-Host "   Cerrando conexiones activas..." -ForegroundColor Gray
        $closeConnections = "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$localDatabase' AND pid != pg_backend_pid();"
        & $psqlPath -h $localHost -p $localPort -U $localUsername -d postgres -c $closeConnections 2>&1 | Out-Null
        
        Write-Host "   Eliminando base de datos existente (si existe)..." -ForegroundColor Gray
        $dropDb = "DROP DATABASE IF EXISTS `"$localDatabase`";"
        & $psqlPath -h $localHost -p $localPort -U $localUsername -d postgres -c $dropDb 2>&1 | Out-Null
        
        Write-Host "   Creando nueva base de datos..." -ForegroundColor Gray
        $createDb = "CREATE DATABASE `"$localDatabase`";"
        & $psqlPath -h $localHost -p $localPort -U $localUsername -d postgres -c $createDb 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "   ERROR al crear base de datos" -ForegroundColor Red
            Remove-Item Env:PGPASSWORD
            exit 1
        }
        
        Write-Host "   Restaurando datos desde backup..." -ForegroundColor Gray
        # Restaurar desde el archivo SQL
        Get-Content $backupFile | & $psqlPath -h $localHost -p $localPort -U $localUsername -d $localDatabase 2>&1 | Out-Null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "   OK Backup restaurado exitosamente" -ForegroundColor Green
        } else {
            Write-Host "   ADVERTENCIA: Hubo algunos errores durante la restauracion (puede ser normal si hay objetos que ya existen)" -ForegroundColor Yellow
        }
        
        # Verificar que se restauraron datos
        Write-Host "   Verificando datos restaurados..." -ForegroundColor Gray
        $checkTables = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';"
        $tableCount = & $psqlPath -h $localHost -p $localPort -U $localUsername -d $localDatabase -t -c $checkTables 2>&1
        
        if ($tableCount -match '\d+') {
            $count = $tableCount.Trim()
            Write-Host "   OK Se restauraron $count tablas" -ForegroundColor Green
        }
        
    } catch {
        Write-Host "   ERROR al restaurar: $_" -ForegroundColor Red
        Remove-Item Env:PGPASSWORD
        exit 1
    }
} else {
    Write-Host "   Restauracion cancelada. El backup esta guardado en: $backupFile" -ForegroundColor Yellow
}

Remove-Item Env:PGPASSWORD

Write-Host ""
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "   PROCESO COMPLETADO" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Archivos de backup creados:" -ForegroundColor Yellow
Write-Host "   SQL: $backupFile" -ForegroundColor White
Write-Host ""
