'use strict';

const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const requiredFiles = [
  'dist/TuoguanDSH.exe',
  'dist/dsh-fish.ico',
  'scripts/cli.js',
  'install.ps1',
  'uninstall.ps1'
];
const missingFiles = requiredFiles.filter((relativePath) => {
  const filePath = path.join(root, ...relativePath.split('/'));
  try {
    return !fs.statSync(filePath).isFile();
  } catch (error) {
    if (error && error.code === 'ENOENT') return true;
    throw error;
  }
});

if (missingFiles.length > 0) {
  console.error('tuoguan-dsh: required publish files are missing:');
  for (const relativePath of missingFiles) {
    console.error(` - ${relativePath}`);
  }
  process.exit(1);
}

console.log(`tuoguan-dsh: verified ${requiredFiles.length} required publish files.`);
