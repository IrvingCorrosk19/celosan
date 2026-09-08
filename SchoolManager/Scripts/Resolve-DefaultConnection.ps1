# Resuelve ConnectionStrings:DefaultConnection desde appsettings (sin imprimir contraseña).
function Get-DefaultConnectionMap {
    $schoolDir = Split-Path $PSScriptRoot -Parent
    if ((Split-Path $PSScriptRoot -Leaf) -eq 'postgres') {
        $schoolDir = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    }
    $devJson = Join-Path $schoolDir "appsettings.Development.json"
    $mainJson = Join-Path $schoolDir "appsettings.json"
    $configPath = if (Test-Path $devJson) { $devJson } else { $mainJson }
    if (-not (Test-Path $configPath)) {
        throw "No se encontro appsettings en $schoolDir"
    }

    $cs = $null
    foreach ($line in (Get-Content $configPath -Encoding UTF8)) {
        if ($line -match '^\s*"DefaultConnection"\s*:\s*"([^"]+)"') {
            $cs = $Matches[1]
        }
    }
    if ([string]::IsNullOrWhiteSpace($cs)) {
        throw "ConnectionStrings:DefaultConnection esta vacio en $configPath"
    }

    $map = @{}
    foreach ($segment in ($cs -split ';')) {
        $t = $segment.Trim()
        if (-not $t) { continue }
        $eq = $t.IndexOf('=')
        if ($eq -lt 1) { continue }
        $map[$t.Substring(0, $eq).Trim()] = $t.Substring($eq + 1).Trim()
    }
    if (-not $map['Host'] -or -not $map['Database'] -or -not $map['Username']) {
        throw "DefaultConnection debe incluir Host, Database y Username."
    }
    if (-not $map['Port']) { $map['Port'] = '5432' }
    return $map
}
