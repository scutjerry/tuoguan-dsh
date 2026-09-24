[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\TuoguanDSH'),
    [switch]$KeepLogs
)

$ErrorActionPreference = 'Stop'

Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -eq 'TuoguanDSH.exe' -and $_.ExecutablePath -eq (Join-Path $InstallDir 'TuoguanDSH.exe') } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

$desktopShortcut = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'Tuoguan DSH.lnk'
$menuDir = Join-Path ([Environment]::GetFolderPath('Programs')) 'Tuoguan DSH'
Remove-Item -LiteralPath $desktopShortcut -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $menuDir -Recurse -Force -ErrorAction SilentlyContinue

if (Test-Path -LiteralPath $InstallDir -PathType Container) {
    if ($KeepLogs) {
        Get-ChildItem -LiteralPath $InstallDir -Force | Where-Object { $_.Name -ne 'logs' } | Remove-Item -Recurse -Force
    } else {
        $escaped = $InstallDir.Replace("'", "''")
        Start-Process -FilePath 'powershell.exe' -WindowStyle Hidden -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-Command',"Start-Sleep -Milliseconds 800; Remove-Item -LiteralPath '$escaped' -Recurse -Force -ErrorAction SilentlyContinue")
    }
}

Write-Host 'Tuoguan DSH was uninstalled.' -ForegroundColor Green
