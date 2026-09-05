[CmdletBinding()]
param(
    [string]$LabRuntimePath,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path

if ([string]::IsNullOrWhiteSpace($LabRuntimePath)) {
    $LabRuntimePath = Join-Path $workspace 'lab-runtime\build-24525702'
}

& (Join-Path $PSScriptRoot 'Verify-Build.ps1') -GamePath $LabRuntimePath

if (-not $SkipBuild) {
    & dotnet build (Join-Path $workspace 'ZB2SecurityLab.sln') --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE"
    }
}

$target = Join-Path $LabRuntimePath 'BepInEx\plugins\ZB2SecurityLab'
New-Item -ItemType Directory -Path $target -Force | Out-Null

$output = Join-Path $workspace "src\ZB2SecurityLab.Plugin\bin\$Configuration\netstandard2.1"
foreach ($assembly in @('ZB2SecurityLab.Core.dll', 'ZB2SecurityLab.Plugin.dll')) {
    Copy-Item -LiteralPath (Join-Path $output $assembly) -Destination (Join-Path $target $assembly) -Force
}

Write-Output "Plugin deployed to: $target"

