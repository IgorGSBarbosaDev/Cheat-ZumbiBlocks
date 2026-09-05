[CmdletBinding()]
param(
    [string]$SourceGamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Zumbi Blocks 2 Open Alpha',
    [string]$LabRuntimePath,
    [string]$BepInExArchive
)

$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path

if ([string]::IsNullOrWhiteSpace($LabRuntimePath)) {
    $LabRuntimePath = Join-Path $workspace 'lab-runtime\build-24525702'
}

if ([string]::IsNullOrWhiteSpace($BepInExArchive)) {
    $BepInExArchive = Join-Path $workspace '.downloads\BepInEx_win_x64_5.4.23.5.zip'
}

$absoluteTarget = [IO.Path]::GetFullPath($LabRuntimePath)
if (-not $absoluteTarget.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Lab runtime must remain inside the workspace: $absoluteTarget"
}

if (-not (Test-Path -LiteralPath (Join-Path $absoluteTarget 'ZumbiBlocks2.exe'))) {
    New-Item -ItemType Directory -Path $absoluteTarget -Force | Out-Null
    & robocopy $SourceGamePath $absoluteTarget /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /NFL /NDL /NJH /NJS /NP
    if ($LASTEXITCODE -gt 7) {
        throw "robocopy failed with exit code $LASTEXITCODE"
    }
}

& (Join-Path $PSScriptRoot 'Verify-Build.ps1') -GamePath $absoluteTarget

$expectedArchiveHash = '82F9878551030F54657792C0740D9D51A09500EEAE1FBA21106B0C441E6732C4'
$actualArchiveHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $BepInExArchive).Hash
if ($actualArchiveHash -ne $expectedArchiveHash) {
    throw "Unexpected BepInEx archive hash. Expected $expectedArchiveHash, received $actualArchiveHash."
}

if (-not (Test-Path -LiteralPath (Join-Path $absoluteTarget 'BepInEx\core\BepInEx.dll'))) {
    Expand-Archive -LiteralPath $BepInExArchive -DestinationPath $absoluteTarget -Force
}

Write-Output "Lab runtime ready: $absoluteTarget"

