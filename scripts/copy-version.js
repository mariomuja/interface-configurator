const fs = require('fs');
const path = require('path');

const rootVersionPath = path.join(__dirname, '..', 'version.json');
const assetsVersionPath = path.join(__dirname, '..', 'frontend', 'src', 'assets', 'version.json');
const assetsDir = path.join(__dirname, '..', 'frontend', 'src', 'assets');

// Ensure assets directory exists
if (!fs.existsSync(assetsDir)) {
  fs.mkdirSync(assetsDir, { recursive: true });
}

// Copy version.json to assets
if (fs.existsSync(rootVersionPath)) {
  fs.copyFileSync(rootVersionPath, assetsVersionPath);
  console.log('✓ Copied version.json to frontend/src/assets/');
} else {
  console.warn('⚠ version.json not found in root directory');
}



