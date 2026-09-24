[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\TuoguanDSH'),
    [switch]$NoDesktopShortcut,
    [switch]$NoStartMenuShortcut,
    [switch]$Launch
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSCommandPath
$distExe = Join-Path $root 'dist\TuoguanDSH.exe'
$distIcon = Join-Path $root 'dist\dsh-fish.ico'

if (-not (Test-Path -LiteralPath $distExe -PathType Leaf)) { & (Join-Path $root 'build.ps1') }
if (-not (Test-Path -LiteralPath $distExe -PathType Leaf)) { throw "Missing build output: $distExe" }
if (-not (Test-Path -LiteralPath $distIcon -PathType Leaf)) { throw "Missing icon: $distIcon" }

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Copy-Item -LiteralPath $distExe -Destination (Join-Path $InstallDir 'TuoguanDSH.exe') -Force
Copy-Item -LiteralPath $distIcon -Destination (Join-Path $InstallDir 'dsh-fish.ico') -Force
Copy-Item -LiteralPath (Join-Path $root 'uninstall.ps1') -Destination (Join-Path $InstallDir 'uninstall.ps1') -Force

function New-TuoguanShortcut([string]$Path) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($Path)
    $shortcut.TargetPath = Join-Path $InstallDir 'TuoguanDSH.exe'
    $shortcut.WorkingDirectory = $InstallDir
    $shortcut.IconLocation = "$(Join-Path $InstallDir 'dsh-fish.ico'),0"
    $shortcut.Description = 'Launch DSH manually and manage it from the Windows system tray'
    $shortcut.Save()
}

$shortcutPaths = @()
if (-not $NoDesktopShortcut) {
    $desktopShortcut = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'Tuoguan DSH.lnk'
    New-TuoguanShortcut $desktopShortcut
    $shortcutPaths += $desktopShortcut
}
if (-not $NoStartMenuShortcut) {
    $menuDir = Join-Path ([Environment]::GetFolderPath('Programs')) 'Tuoguan DSH'
    New-Item -ItemType Directory -Path $menuDir -Force | Out-Null
    $menuShortcut = Join-Path $menuDir 'Tuoguan DSH.lnk'
    New-TuoguanShortcut $menuShortcut
    $shortcutPaths += $menuShortcut
}

# Deliberately creates no service, scheduled task, registry Run entry, or Startup-folder item.
Write-Host "Installation completed: $InstallDir" -ForegroundColor Green
if ($shortcutPaths.Count -gt 0) { Write-Host ('Shortcuts: ' + ($shortcutPaths -join '; ')) }
Write-Host 'No auto-start entry was created. Tuoguan DSH runs only when you click its shortcut.'

if ($Launch) { Start-Process -FilePath (Join-Path $InstallDir 'TuoguanDSH.exe') -WorkingDirectory $InstallDir }
