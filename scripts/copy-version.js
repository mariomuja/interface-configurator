const fs = require('fs');
const path = require('path');

try {
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
    // Create a default version.json if it doesn't exist
    const defaultVersion = { version: '1.0.0', buildDate: new Date().toISOString() };
    fs.writeFileSync(assetsVersionPath, JSON.stringify(defaultVersion, null, 2));
    console.warn('⚠ version.json not found in root directory, created default version');
  }
} catch (error) {
  console.error('Error copying version.json:', error.message);
  process.exit(1);
}



