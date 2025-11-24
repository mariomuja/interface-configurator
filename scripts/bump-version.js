#!/usr/bin/env node

/**
 * Auto-increment version based on code changes
 * This script checks if code files have changed and increments the version accordingly
 */

const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const VERSION_FILE = path.join(__dirname, '..', 'version.json');
const VERSION_HASH_FILE = path.join(__dirname, '..', '.version-hash');
const FRONTEND_ASSETS_VERSION = path.join(__dirname, '..', 'frontend', 'src', 'assets', 'version.json');
const FRONTEND_PACKAGE_JSON = path.join(__dirname, '..', 'frontend', 'package.json');
const ROOT_PACKAGE_JSON = path.join(__dirname, '..', 'package.json');

// Files and directories to monitor for changes
const MONITOR_PATHS = [
  'frontend/src',
  'azure-functions/main',
  'azure-functions/main.Core',
  'api',
  'scripts'
];

// Files to ignore
const IGNORE_PATTERNS = [
  /node_modules/,
  /\.git/,
  /dist/,
  /build/,
  /\.angular/,
  /bin/,
  /obj/,
  /\.vs/,
  /\.vscode/,
  /version\.json$/,
  /\.version-hash$/,
  /package-lock\.json$/,
  /yarn\.lock$/,
  /\.log$/,
  /\.md$/
];

/**
 * Calculate hash of all monitored files
 */
function calculateCodeHash() {
  const hashes = [];
  
  function processFile(filePath) {
    try {
      const content = fs.readFileSync(filePath, 'utf8');
      const hash = crypto.createHash('sha256').update(content).digest('hex');
      hashes.push(hash);
    } catch (err) {
      // Ignore errors for files that don't exist or can't be read
    }
  }
  
  function processDirectory(dirPath) {
    try {
      const entries = fs.readdirSync(dirPath, { withFileTypes: true });
      
      for (const entry of entries) {
        const fullPath = path.join(dirPath, entry.name);
        const relativePath = path.relative(path.join(__dirname, '..'), fullPath);
        
        // Skip ignored patterns
        if (IGNORE_PATTERNS.some(pattern => pattern.test(relativePath))) {
          continue;
        }
        
        if (entry.isDirectory()) {
          processDirectory(fullPath);
        } else if (entry.isFile()) {
          // Only process code files
          const ext = path.extname(entry.name);
          if (['.ts', '.js', '.tsx', '.jsx', '.cs', '.html', '.css', '.json', '.bicep', '.tf'].includes(ext) ||
              entry.name === 'package.json' || entry.name === 'tsconfig.json' || entry.name === 'host.json') {
            processFile(fullPath);
          }
        }
      }
    } catch (err) {
      // Ignore errors for directories that don't exist or can't be read
    }
  }
  
  // Process all monitored paths
  for (const monitorPath of MONITOR_PATHS) {
    const fullPath = path.join(__dirname, '..', monitorPath);
    if (fs.existsSync(fullPath)) {
      if (fs.statSync(fullPath).isFile()) {
        processFile(fullPath);
      } else {
        processDirectory(fullPath);
      }
    }
  }
  
  // Create combined hash
  const combinedHash = crypto.createHash('sha256').update(hashes.sort().join('')).digest('hex');
  return combinedHash;
}

/**
 * Read current version
 */
function readVersion() {
  try {
    if (fs.existsSync(VERSION_FILE)) {
      return JSON.parse(fs.readFileSync(VERSION_FILE, 'utf8'));
    }
  } catch (err) {
    console.error('Error reading version.json:', err.message);
  }
  
  return {
    version: '1.0.0',
    buildNumber: 0,
    lastUpdated: new Date().toISOString()
  };
}

/**
 * Write version to file
 */
function writeVersion(versionInfo) {
  const versionJson = JSON.stringify(versionInfo, null, 2) + '\n';
  fs.writeFileSync(VERSION_FILE, versionJson, 'utf8');
  fs.writeFileSync(FRONTEND_ASSETS_VERSION, versionJson, 'utf8');
  
  // Update package.json files
  try {
    if (fs.existsSync(FRONTEND_PACKAGE_JSON)) {
      const pkg = JSON.parse(fs.readFileSync(FRONTEND_PACKAGE_JSON, 'utf8'));
      pkg.version = versionInfo.version;
      fs.writeFileSync(FRONTEND_PACKAGE_JSON, JSON.stringify(pkg, null, 2) + '\n', 'utf8');
    }
  } catch (err) {
    console.warn('Warning: Could not update frontend/package.json:', err.message);
  }
  
  try {
    if (fs.existsSync(ROOT_PACKAGE_JSON)) {
      const pkg = JSON.parse(fs.readFileSync(ROOT_PACKAGE_JSON, 'utf8'));
      pkg.version = versionInfo.version;
      fs.writeFileSync(ROOT_PACKAGE_JSON, JSON.stringify(pkg, null, 2) + '\n', 'utf8');
    }
  } catch (err) {
    console.warn('Warning: Could not update root package.json:', err.message);
  }
  
  console.log(`✓ Version updated to ${versionInfo.version} (build ${versionInfo.buildNumber})`);
}

/**
 * Read stored hash
 */
function readStoredHash() {
  try {
    if (fs.existsSync(VERSION_HASH_FILE)) {
      return fs.readFileSync(VERSION_HASH_FILE, 'utf8').trim();
    }
  } catch (err) {
    // Ignore errors
  }
  return null;
}

/**
 * Store hash
 */
function storeHash(hash) {
  try {
    fs.writeFileSync(VERSION_HASH_FILE, hash, 'utf8');
  } catch (err) {
    console.warn('Warning: Could not write .version-hash:', err.message);
  }
}

/**
 * Increment version
 */
function incrementVersion(currentVersion) {
  const parts = currentVersion.version.split('.');
  const major = parseInt(parts[0]) || 1;
  const minor = parseInt(parts[1]) || 0;
  const patch = parseInt(parts[2]) || 0;
  
  // Increment patch version
  const newPatch = patch + 1;
  const newVersion = `${major}.${minor}.${newPatch}`;
  const newBuild = (currentVersion.buildNumber || 0) + 1;
  
  return {
    version: newVersion,
    buildNumber: newBuild,
    lastUpdated: new Date().toISOString()
  };
}

/**
 * Main function
 */
function main() {
  const force = process.argv.includes('--force');
  
  console.log('Checking for code changes...');
  
  const currentVersion = readVersion();
  const currentHash = calculateCodeHash();
  const storedHash = readStoredHash();
  
  if (!force && storedHash === currentHash) {
    console.log('✓ No code changes detected. Version unchanged.');
    console.log(`  Current version: ${currentVersion.version} (build ${currentVersion.buildNumber})`);
    return;
  }
  
  console.log('Code changes detected. Incrementing version...');
  const newVersion = incrementVersion(currentVersion);
  writeVersion(newVersion);
  storeHash(currentHash);
  
  console.log(`\nVersion bumped successfully!`);
  console.log(`  Old: ${currentVersion.version} (build ${currentVersion.buildNumber})`);
  console.log(`  New: ${newVersion.version} (build ${newVersion.buildNumber})`);
}

main();


