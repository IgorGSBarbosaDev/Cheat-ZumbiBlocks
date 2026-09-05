[CmdletBinding()]
param(
    [string]$GamePath,
    [string]$ExpectedExecutableSha256 = '66C3ED6829349AAC8B5CB5FDB2A85EF62D17C1BDED4334352AF7349CA608EA0A',
    [string]$ExpectedAssemblySha256 = 'C41A298975D35F0DAD0A05531BCE6E0B6E274D0DDF265217D65CE3AC5CBC84E1'
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($GamePath)) {
    $workspace = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
    $GamePath = Join-Path $workspace 'lab-runtime\build-24525702'
}

$resolvedGamePath = (Resolve-Path -LiteralPath $GamePath).Path
$targets = @(
    [PSCustomObject]@{
        Name = 'ZumbiBlocks2.exe'
        Path = Join-Path $resolvedGamePath 'ZumbiBlocks2.exe'
        Expected = $ExpectedExecutableSha256
    },
    [PSCustomObject]@{
        Name = 'Assembly-CSharp.dll'
        Path = Join-Path $resolvedGamePath 'ZumbiBlocks2_Data\Managed\Assembly-CSharp.dll'
        Expected = $ExpectedAssemblySha256
    }
)

$results = foreach ($target in $targets) {
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $target.Path).Hash
    [PSCustomObject]@{
        Name = $target.Name
        Path = $target.Path
        ExpectedSha256 = $target.Expected
        ActualSha256 = $actual
        IsMatch = $actual -eq $target.Expected
    }
}

$results | Format-Table -AutoSize
if ($results.IsMatch -contains $false) {
    throw 'Unsupported build fingerprint. No plugin deployment or game-context polling is allowed.'
}

Write-Output 'Build fingerprint verified.'

