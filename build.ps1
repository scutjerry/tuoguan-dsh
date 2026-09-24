[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSCommandPath
$source = Join-Path $root 'src\DSH-Tray.cs'
$icon = Join-Path $root 'assets\dsh-fish.ico'
$dist = Join-Path $root 'dist'
$output = Join-Path $dist 'TuoguanDSH.exe'

$cscCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$csc = $cscCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if (-not $csc) { throw 'The .NET Framework C# compiler csc.exe was not found.' }
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing source: $source" }
if (-not (Test-Path -LiteralPath $icon -PathType Leaf)) { throw "Missing icon: $icon" }

New-Item -ItemType Directory -Path $dist -Force | Out-Null
Remove-Item -LiteralPath $output -Force -ErrorAction SilentlyContinue

& $csc /nologo /target:winexe /optimize+ /out:$output /win32icon:$icon /reference:System.Windows.Forms.dll /reference:System.Drawing.dll $source
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $output -PathType Leaf)) {
    throw "Compilation failed. csc exit code: $LASTEXITCODE"
}

Copy-Item -LiteralPath $icon -Destination (Join-Path $dist 'dsh-fish.ico') -Force
Write-Host "Build completed: $output" -ForegroundColor Green
