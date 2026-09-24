#!/usr/bin/env node
'use strict';

const { spawn, spawnSync } = require('child_process');
const path = require('path');

function printHelp() {
  console.log(`Usage: tuoguan-dsh [command]

Commands:
  install     Install or update the tray app and shortcuts
  uninstall   Remove the tray app and shortcuts
  --help      Show this help

With no command, Tuoguan DSH is launched.`);
}

if (process.platform !== 'win32') {
  console.error('tuoguan-dsh only supports Windows 10/11.');
  process.exit(1);
}

const root = path.resolve(__dirname, '..');
const installDirectory = path.join(
  process.env.LOCALAPPDATA || '',
  'Programs',
  'TuoguanDSH'
);
const executable = path.join(installDirectory, 'TuoguanDSH.exe');
const installScript = path.join(root, 'install.ps1');
const uninstallScript = path.join(root, 'uninstall.ps1');
const command = process.argv[2];

if (command === '--help' || command === '-h' || command === 'help') {
  printHelp();
  process.exit(0);
}

if (command === 'install' || command === 'uninstall') {
  const script = command === 'install' ? installScript : uninstallScript;
  const args = [
    '-NoProfile',
    '-ExecutionPolicy', 'Bypass',
    '-File', script
  ];
  if (command === 'uninstall') {
    args.push('-InstallDir', installDirectory);
  }

  const spawnOptions = {
    stdio: 'inherit',
    windowsHide: true
  };
  if (command === 'install') {
    spawnOptions.cwd = root;
  }

  const result = spawnSync('powershell.exe', args, spawnOptions);
  if (result.error) {
    console.error(`tuoguan-dsh ${command} failed:`, result.error.message);
    process.exit(1);
  }
  process.exit(result.status === null ? 1 : result.status);
}

if (command) {
  console.error(`Unknown command: ${command}`);
  printHelp();
  process.exit(1);
}

const child = spawn(executable, [], {
  detached: true,
  stdio: 'ignore',
  windowsHide: true
});
child.on('error', (error) => {
  console.error('tuoguan-dsh launch failed:', error.message);
  process.exitCode = 1;
});
child.unref();
