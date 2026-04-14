<#!
  Crea un usuario admin de colegio en PostgreSQL (misma lógica que la app: role=admin, BCrypt).

  Lee ConnectionStrings:DefaultConnection desde appsettings.Development.json o appsettings.json
  en la raíz del proyecto SchoolManager (sin pasar contraseña de BD por parámetro).

  Requisitos: .NET 8 SDK, psql en PgBin (por defecto PostgreSQL 18).

  Ejemplo:
    .\create-school-admin.ps1 -Email "nuevo.admin@colegio.edu" -PlainPassword "Cambiar123!"
    .\create-school-admin.ps1 -Email "a@b.com" -PlainPassword "x" -SchoolId "6e42399f-6f17-4585-b92e-fa4fff02cb65"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Email,

    [Parameter(Mandatory = $true)]
    [string] $PlainPassword,

    [string] $FirstName = "Administrador",
    [string] $LastName = "Auxiliar",

    # Si vacío, usa el primer colegio que tenga admin_id (como admin principal).
    [string] $SchoolId = "",

    [string] $PgBin = "C:\Program Files\PostgreSQL\18\bin"
)

$ErrorActionPreference = "Stop"

function Parse-DbConnectionString {
    param([string] $Raw)
    $map = @{}
    foreach ($part in $Raw -split ";") {
        $part = $part.Trim()
        if (-not $part) { continue }
        $eq = $part.IndexOf("=")
        if ($eq -lt 1) { continue }
        $key = $part.Substring(0, $eq).Trim()
        $val = $part.Substring($eq + 1).Trim()
        $map[$key] = $val
    }
    return $map
}

$scriptDir = $PSScriptRoot
$projectRoot = (Resolve-Path (Join-Path $scriptDir "..\..")).Path

$devPath = Join-Path $projectRoot "appsettings.Development.json"
$basePath = Join-Path $projectRoot "appsettings.json"
$conn = $null
if (Test-Path $devPath) {
    $j = Get-Content $devPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $conn = $j.ConnectionStrings.DefaultConnection
}
if (-not $conn -and (Test-Path $basePath)) {
    $j = Get-Content $basePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $conn = $j.ConnectionStrings.DefaultConnection
}
if (-not $conn) {
    throw "No se encontró ConnectionStrings:DefaultConnection en appsettings.Development.json ni appsettings.json bajo $projectRoot"
}

$db = Parse-DbConnectionString $conn
$host = $db["Host"]
$database = $db["Database"]
$user = $db["Username"]
$password = $db["Password"]
$port = if ($db["Port"]) { $db["Port"] } else { "5432" }

if (-not $host -or -not $database -or -not $user -or -not $password) {
    throw "Cadena de conexión incompleta (Host, Database, Username, Password)."
}

$env:PGPASSWORD = $password
if (($db["SSL Mode"] -match "Require") -or ($conn -match "SSL Mode=Require")) {
    $env:PGSSLMODE = "require"
} else {
    Remove-Item Env:PGSSLMODE -ErrorAction SilentlyContinue
}

$psql = Join-Path $PgBin "psql.exe"
if (-not (Test-Path $psql)) {
    throw "No existe psql.exe en: $PgBin"
}

$hashCliDir = Join-Path $scriptDir "BcryptHashCli"
$hash = (& dotnet run --project $hashCliDir --configuration Release --verbosity quiet -- $PlainPassword | Out-String).Trim()
if (-not $hash -or $hash.Length -lt 50) {
    throw "No se pudo generar el hash BCrypt (¿dotnet SDK instalado?)."
}

if (-not $SchoolId) {
    $SchoolId = & $psql -h $host -p $port -U $user -d $database -t -A -c "SELECT id::text FROM public.schools WHERE admin_id IS NOT NULL ORDER BY name LIMIT 1;"
    $SchoolId = $SchoolId.Trim()
}
if (-not $SchoolId) {
    throw "No hay SchoolId y no se encontró ningún colegio con admin_id. Pasa -SchoolId explícitamente."
}

function Sql-Literal([string] $s) {
    return $s.Replace("'", "''")
}

$sql = @"
INSERT INTO public.users (school_id, name, last_name, email, password_hash, role, status, created_at)
VALUES (
  '$(Sql-Literal $SchoolId)'::uuid,
  '$(Sql-Literal $FirstName)',
  '$(Sql-Literal $LastName)',
  '$(Sql-Literal $Email)',
  '$(Sql-Literal $hash)',
  'admin',
  'active',
  NOW()
)
RETURNING id, email, role, school_id;
"@

$tmp = [System.IO.Path]::GetTempFileName() + ".sql"
try {
    [System.IO.File]::WriteAllText($tmp, $sql, [System.Text.UTF8Encoding]::new($false))
    & $psql -h $host -p $port -U $user -d $database -v ON_ERROR_STOP=1 -f $tmp
}
finally {
    Remove-Item $tmp -ErrorAction SilentlyContinue
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:PGSSLMODE -ErrorAction SilentlyContinue
}

Write-Host "Listo. Inicia sesión con el email y la contraseña que indicaste."
