'use strict';

const { spawnSync } = require('child_process');
const path = require('path');

if (process.platform !== 'win32') {
  console.log('tuoguan-dsh: Windows only; skipping tray installation on this platform.');
  process.exit(0);
}

const root = path.resolve(__dirname, '..');
const script = path.join(root, 'install.ps1');
const result = spawnSync('powershell.exe', [
  '-NoProfile',
  '-ExecutionPolicy', 'Bypass',
  '-File', script
], {
  cwd: root,
  stdio: 'inherit',
  windowsHide: true
});

if (result.error) {
  console.error('tuoguan-dsh postinstall failed:', result.error.message);
  process.exit(1);
}
process.exit(result.status === null ? 1 : result.status);
