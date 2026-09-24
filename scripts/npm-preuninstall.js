'use strict';

const { spawnSync } = require('child_process');
const path = require('path');

if (process.platform !== 'win32') process.exit(0);

const root = path.resolve(__dirname, '..');
const script = path.join(root, 'uninstall.ps1');
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
  console.error('tuoguan-dsh preuninstall failed:', result.error.message);
  process.exit(1);
}
process.exit(result.status === null ? 1 : result.status);
