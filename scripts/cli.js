#!/usr/bin/env node
'use strict';

const { spawn, spawnSync } = require('child_process');
const path = require('path');

if (process.platform !== 'win32') {
  console.error('tuoguan-dsh only supports Windows 10/11.');
  process.exit(1);
}

const installDirectory = path.join(
  process.env.LOCALAPPDATA || '',
  'Programs',
  'TuoguanDSH'
);
const executable = path.join(installDirectory, 'TuoguanDSH.exe');
const uninstallScript = path.join(installDirectory, 'uninstall.ps1');

if (process.argv[2] === 'uninstall') {
  const result = spawnSync('powershell.exe', [
    '-NoProfile',
    '-ExecutionPolicy', 'Bypass',
    '-File', uninstallScript,
    '-InstallDir', installDirectory
  ], {
    stdio: 'inherit',
    windowsHide: true
  });
  if (result.error) {
    console.error('tuoguan-dsh uninstall failed:', result.error.message);
    process.exit(1);
  }
  process.exit(result.status === null ? 1 : result.status);
}

const child = spawn(executable, [], {
  detached: true,
  stdio: 'ignore',
  windowsHide: true
});
child.unref();
