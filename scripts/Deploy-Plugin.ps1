[CmdletBinding()]
param(
    [string]$GamePath,
    [string]$LabRuntimePath,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path

if (-not [string]::IsNullOrWhiteSpace($GamePath) -and -not [string]::IsNullOrWhiteSpace($LabRuntimePath)) {
    throw 'Specify either GamePath or the legacy LabRuntimePath, not both.'
}

if ([string]::IsNullOrWhiteSpace($GamePath)) {
    $GamePath = if ([string]::IsNullOrWhiteSpace($LabRuntimePath)) {
        Join-Path $workspace 'lab-runtime\build-24525702'
    } else {
        $LabRuntimePath
    }
}

$resolvedGamePath = (Resolve-Path -LiteralPath $GamePath).Path
& (Join-Path $PSScriptRoot 'Verify-Build.ps1') -GamePath $resolvedGamePath

$bepInExReferencePath = Join-Path $resolvedGamePath 'BepInEx\core'
$bepInExAssembly = Join-Path $bepInExReferencePath 'BepInEx.dll'
if (-not (Test-Path -LiteralPath $bepInExAssembly)) {
    throw "Compatible BepInEx is not present at '$bepInExAssembly'. Loader installation is a separate, explicitly authorized operation."
}

if (-not $SkipBuild) {
    & dotnet build (Join-Path $workspace 'ZB2SecurityLab.sln') --configuration $Configuration `
        "-p:GameRootPath=$resolvedGamePath" `
        "-p:BepInExReferencePath=$bepInExReferencePath"
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE"
    }
}

$target = Join-Path $resolvedGamePath 'BepInEx\plugins\ZB2SecurityLab'
New-Item -ItemType Directory -Path $target -Force | Out-Null

$output = Join-Path $workspace "src\ZB2SecurityLab.Plugin\bin\$Configuration\netstandard2.1"
foreach ($assembly in @('ZB2SecurityLab.Core.dll', 'ZB2SecurityLab.Plugin.dll')) {
    Copy-Item -LiteralPath (Join-Path $output $assembly) -Destination (Join-Path $target $assembly) -Force
}

Write-Output "Plugin deployed to: $target"
