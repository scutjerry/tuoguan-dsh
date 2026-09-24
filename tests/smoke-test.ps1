[CmdletBinding()]
param(
    [switch]$IncludeInstallTest
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$failures = New-Object System.Collections.Generic.List[string]

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { [void]$script:failures.Add($Message) }
}

& (Join-Path $root 'build.ps1')
$exe = Join-Path $root 'dist\TuoguanDSH.exe'
$ico = Join-Path $root 'dist\dsh-fish.ico'
Assert-True (Test-Path -LiteralPath $exe -PathType Leaf) 'Missing dist/TuoguanDSH.exe after build.'
Assert-True (Test-Path -LiteralPath $ico -PathType Leaf) 'Missing dist/dsh-fish.ico after build.'

Add-Type -AssemblyName System.Drawing
if (Test-Path -LiteralPath $ico -PathType Leaf) {
    $icon = New-Object System.Drawing.Icon($ico, (New-Object System.Drawing.Size(32,32)))
    $bitmap = $icon.ToBitmap()
    Assert-True ($bitmap.GetPixel(0,0).A -eq 0) 'The icon corner is not transparent.'
    $bitmap.Dispose()
    $icon.Dispose()
}

$sourceText = Get-Content -LiteralPath (Join-Path $root 'src\DSH-Tray.cs') -Raw
Assert-True ($sourceText -notmatch 'C:\\Users\\') 'Source contains a hard-coded user profile path.'
Assert-True ($sourceText -notmatch 'Local\\DSH-Web-Tray-[0-9]+') 'Source contains a machine-specific mutex identifier.'
Assert-True ($sourceText -match 'CreateNoWindow = true') 'Source does not hide the Node console window.'
Assert-True ($sourceText -match 'ExitApplication') 'Source is missing the full-exit implementation.'

$installText = Get-Content -LiteralPath (Join-Path $root 'install.ps1') -Raw
Assert-True ($installText -notmatch 'Register-ScheduledTask|schtasks|CurrentVersion\\Run') 'Installer contains an auto-start registration mechanism.'

if ($IncludeInstallTest) {
    $testInstall = Join-Path $env:TEMP ('TuoguanDSH-Smoke-' + [Guid]::NewGuid().ToString('N'))
    try {
        & (Join-Path $root 'install.ps1') -InstallDir $testInstall -NoDesktopShortcut -NoStartMenuShortcut
        Assert-True (Test-Path -LiteralPath (Join-Path $testInstall 'TuoguanDSH.exe') -PathType Leaf) 'Test installation is missing the EXE.'
        Assert-True (Test-Path -LiteralPath (Join-Path $testInstall 'dsh-fish.ico') -PathType Leaf) 'Test installation is missing the icon.'
        & (Join-Path $root 'uninstall.ps1') -InstallDir $testInstall
        Start-Sleep -Seconds 2
        Assert-True (-not (Test-Path -LiteralPath $testInstall)) 'Test uninstall did not remove the installation directory.'
    }
    finally {
        Remove-Item -LiteralPath $testInstall -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($failures.Count -gt 0) {
    Write-Host 'Smoke test failures:' -ForegroundColor Red
    $failures | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    exit 1
}

Write-Host 'All smoke tests passed.' -ForegroundColor Green
