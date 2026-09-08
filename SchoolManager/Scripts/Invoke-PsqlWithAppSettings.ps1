# Ejecuta psql usando la cadena de DefaultConnection desde appsettings.
# Orden: appsettings.Development.json (local, no versionado) -> appsettings.json.
# Ejemplo:
#   .\Invoke-PsqlWithAppSettings.ps1 -f .\backfill_activities_school_trimester.sql
#   .\Invoke-PsqlWithAppSettings.ps1 -c "SELECT COUNT(*) FROM activities;"

param(
    [string] $PsqlPath = "C:\Program Files\PostgreSQL\18\bin\psql.exe",
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $PsqlArgs
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Resolve-DefaultConnection.ps1")
$map = Get-DefaultConnectionMap

$hostName = $map['Host']
$db = $map['Database']
$user = $map['Username']
$pass = $map['Password']
$port = if ($map['Port']) { $map['Port'] } else { '5432' }

if (-not $hostName -or -not $db -or -not $user) {
    throw "La cadena de conexión debe incluir al menos Host, Database y Username."
}

$ssl = $map['SSL Mode']
if ($ssl -match 'Require|require') {
    $env:PGSSLMODE = 'require'
}

if ($pass) {
    $env:PGPASSWORD = $pass
}

if (-not (Test-Path $PsqlPath)) {
    throw "No existe psql en: $PsqlPath. Ajusta -PsqlPath o instala PostgreSQL 18."
}

& $PsqlPath -h $hostName -p $port -U $user -d $db @PsqlArgs
exit $LASTEXITCODE
