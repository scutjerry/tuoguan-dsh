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

$package = Get-Content -LiteralPath (Join-Path $root 'package.json') -Raw | ConvertFrom-Json
Assert-True ($package.name -eq 'tuoguan-dsh') 'package.json has the wrong package name.'
Assert-True ($package.scripts.postinstall -eq 'node ./scripts/npm-postinstall.js') 'package.json is missing the npm postinstall hook.'
Assert-True ($package.scripts.preuninstall -eq 'node ./scripts/npm-preuninstall.js') 'package.json is missing the npm preuninstall hook.'
Assert-True (-not $package.scripts.postuninstall) 'package.json should not run the same uninstall hook twice.'
Assert-True ($package.scripts.prepublishOnly -eq 'node ./scripts/check-dist.js') 'package.json is missing the prepublish artifact check.'
Assert-True (-not $package.scripts.prepare) 'package.json must not build during npm git dependency preparation.'
Assert-True ($package.engines.node -eq '>=18') 'package.json has the wrong minimum Node.js version.'
Assert-True ($package.publishConfig.access -eq 'public') 'package.json is missing public publish access.'
Assert-True (Test-Path -LiteralPath (Join-Path $root 'scripts\cli.js') -PathType Leaf) 'The npm CLI entry is missing.'
Assert-True (Test-Path -LiteralPath (Join-Path $root 'scripts\check-dist.js') -PathType Leaf) 'The npm publish artifact check is missing.'

$cliText = Get-Content -LiteralPath (Join-Path $root 'scripts\cli.js') -Raw
Assert-True ($cliText -match "command === 'install'") 'The npm CLI is missing the manual install command.'
Assert-True ($cliText -match "command === '--help'") 'The npm CLI is missing help output.'

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
