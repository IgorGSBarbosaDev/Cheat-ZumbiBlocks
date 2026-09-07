[CmdletBinding()]
param(
    [string]$BepInExArchive,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path

if ([string]::IsNullOrWhiteSpace($BepInExArchive)) {
    $BepInExArchive = Join-Path $workspace '.downloads\BepInEx_win_x64_5.4.23.5.zip'
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $workspace 'artifacts\launcher'
}

$archive = (Resolve-Path -LiteralPath $BepInExArchive).Path
$expectedArchiveHash = '82F9878551030F54657792C0740D9D51A09500EEAE1FBA21106B0C441E6732C4'
$actualArchiveHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash
if ($actualArchiveHash -ne $expectedArchiveHash) {
    throw "Unexpected BepInEx archive hash. Expected $expectedArchiveHash, received $actualArchiveHash."
}

$absoluteOutput = [IO.Path]::GetFullPath($OutputPath)
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $workspace 'artifacts'))
if (-not $absoluteOutput.StartsWith($artifactsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Launcher output must be a child directory of '$artifactsRoot': $absoluteOutput"
}

if (Test-Path -LiteralPath $absoluteOutput) {
    Remove-Item -LiteralPath $absoluteOutput -Recurse -Force
}

New-Item -ItemType Directory -Path $absoluteOutput -Force | Out-Null

& dotnet build (Join-Path $workspace 'src\ZB2SecurityLab.Plugin\ZB2SecurityLab.Plugin.csproj') `
    --configuration Release `
    -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) {
    throw "Plugin build failed with exit code $LASTEXITCODE."
}

& dotnet publish (Join-Path $workspace 'src\ZB2SecurityLab.Launcher\ZB2SecurityLab.Launcher.csproj') `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=false `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugSymbols=false `
    -p:DebugType=None `
    -p:RequireLauncherPayload=true `
    "-p:BepInExArchivePath=$archive" `
    --output $absoluteOutput
if ($LASTEXITCODE -ne 0) {
    throw "Launcher publish failed with exit code $LASTEXITCODE."
}

$publishedFiles = @(Get-ChildItem -LiteralPath $absoluteOutput -File)
if ($publishedFiles.Count -ne 1 -or $publishedFiles[0].Name -ne 'ZB2SecurityLab.Launcher.exe') {
    throw "Expected exactly one published executable, found: $($publishedFiles.Name -join ', ')"
}

$result = Get-FileHash -Algorithm SHA256 -LiteralPath $publishedFiles[0].FullName
Write-Output "Launcher published: $($publishedFiles[0].FullName)"
Write-Output "SHA-256: $($result.Hash)"
