[CmdletBinding()]
param(
    [string]$SteamPath,
    [string]$AppId = '1941780'
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($SteamPath)) {
    $SteamPath = (Get-ItemProperty -LiteralPath 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
}

if ([string]::IsNullOrWhiteSpace($SteamPath)) {
    $SteamPath = 'C:\Program Files (x86)\Steam'
}

$resolvedSteamPath = (Resolve-Path -LiteralPath $SteamPath).Path
$libraryRoots = [System.Collections.Generic.List[string]]::new()
$libraryRoots.Add($resolvedSteamPath)

$libraryFile = Join-Path $resolvedSteamPath 'steamapps\libraryfolders.vdf'
if (Test-Path -LiteralPath $libraryFile) {
    foreach ($line in Get-Content -LiteralPath $libraryFile) {
        if ($line -match '^\s*"path"\s*"([^"]+)"') {
            $candidate = $matches[1] -replace '\\\\', '\'
            if (Test-Path -LiteralPath $candidate) {
                $libraryRoots.Add((Resolve-Path -LiteralPath $candidate).Path)
            }
        }
    }
}

foreach ($libraryRoot in $libraryRoots | Select-Object -Unique) {
    $manifestPath = Join-Path $libraryRoot "steamapps\appmanifest_$AppId.acf"
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        continue
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw
    if ($manifest -notmatch '"installdir"\s+"([^"]+)"') {
        throw "Steam manifest does not contain installdir: $manifestPath"
    }

    $gamePath = Join-Path $libraryRoot (Join-Path 'steamapps\common' $matches[1])
    $resolvedGamePath = (Resolve-Path -LiteralPath $gamePath).Path
    Write-Output $resolvedGamePath
    return
}

throw "Steam app $AppId was not found below '$resolvedSteamPath'."
