# Homologa estructura de carga curricular hacia DefaultConnection (Render).
# No imprime contraseñas. No copia horas locales.

$ErrorActionPreference = "Stop"
$psql = "C:\Program Files\PostgreSQL\18\bin\psql.exe"
$sql = Join-Path $PSScriptRoot "postgres\apply_homologate_curriculum_load_to_render.sql"
. (Join-Path $PSScriptRoot "Resolve-DefaultConnection.ps1")

$map = Get-DefaultConnectionMap
$hostName = $map['Host']
$db = $map['Database']
$user = $map['Username']
$port = $map['Port']

if ($hostName -notmatch 'oregon-postgres\.render\.com$') {
    throw "DefaultConnection no apunta a Render. Homologacion abortada."
}
if ($db -ne 'schoolmanager_daqf') {
    throw "DefaultConnection no es schoolmanager_daqf."
}

Write-Output "TARGET_HOST_SUFFIX=$($hostName.Split('.')[-3..-1] -join '.')"
Write-Output "TARGET_DB=$db"
Write-Output "TARGET_USER=$user"

if ($map['SSL Mode'] -match 'Require') { $env:PGSSLMODE = 'require' }
$env:PGPASSWORD = $map['Password']

$beforeSql = Join-Path $PSScriptRoot "postgres\_tmp_homologate_check_before.sql"
$afterSql = Join-Path $PSScriptRoot "postgres\_tmp_homologate_check_after.sql"
$before = & $psql -h $hostName -p $port -U $user -d $db -P pager=off -t -A -f $beforeSql
if ($LASTEXITCODE -ne 0) { throw "Fallo la lectura previa." }
Write-Output "BEFORE=$($before.Trim())"

& $psql -h $hostName -p $port -U $user -d $db -v ON_ERROR_STOP=1 -f $sql
Write-Output "APPLY_EXIT=$LASTEXITCODE"
if ($LASTEXITCODE -ne 0) { throw "La homologacion fallo. Transaccion revertida si no hubo COMMIT." }

$after = & $psql -h $hostName -p $port -U $user -d $db -P pager=off -t -A -f $afterSql
if ($LASTEXITCODE -ne 0) { throw "Fallo la lectura posterior." }
Write-Output "AFTER=$($after.Trim())"

Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
Remove-Item Env:PGSSLMODE -ErrorAction SilentlyContinue
Write-Output "DONE=yes"
