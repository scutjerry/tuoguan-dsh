[CmdletBinding()]
param(
    [switch]$NoDesktopShortcut,
    [switch]$NoStartMenuShortcut,
    [switch]$Launch
)

$ErrorActionPreference = 'Stop'
$repo = 'scutjerry/tuoguan-dsh'
$branch = 'main'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('tuoguan-dsh-' + [Guid]::NewGuid().ToString('N'))
$zipPath = Join-Path $tempRoot 'source.zip'
$extractPath = Join-Path $tempRoot 'source'

try {
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
    $zipUrl = "https://github.com/$repo/archive/refs/heads/$branch.zip"
    Write-Host "Downloading $repo ..."
    Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath -UseBasicParsing
    Expand-Archive -LiteralPath $zipPath -DestinationPath $extractPath -Force
    $projectDir = Get-ChildItem -LiteralPath $extractPath -Directory | Select-Object -First 1
    if (-not $projectDir) { throw 'No project directory was found in the downloaded archive.' }

    $installScript = Join-Path $projectDir.FullName 'install.ps1'
    if (-not (Test-Path -LiteralPath $installScript -PathType Leaf)) { throw 'install.ps1 is missing from the downloaded archive.' }

    $installArguments = @{}
    if ($NoDesktopShortcut) { $installArguments.NoDesktopShortcut = $true }
    if ($NoStartMenuShortcut) { $installArguments.NoStartMenuShortcut = $true }
    if ($Launch) { $installArguments.Launch = $true }
    & $installScript @installArguments
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
