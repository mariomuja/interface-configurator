#!/bin/bash
set -e

export PATH="/usr/bin:/usr/local/bin:$PATH"

# Create shared npm cache directory in workspace (persists across containers)
NPM_CACHE_DIR="$PWD/../.npm-cache"
mkdir -p "$NPM_CACHE_DIR"
echo "Using shared npm cache: $NPM_CACHE_DIR"

# We're already in the frontend directory via dir() in Jenkinsfile
echo "Installing Node.js dependencies..."
/usr/bin/docker run --rm \
  --volumes-from interface-configurator-jenkins \
  -v "$NPM_CACHE_DIR:/root/.npm" \
  -w "$PWD" \
  node:22 \
  npm ci --cache /root/.npm

echo "Building Angular frontend..."
/usr/bin/docker run --rm \
  --volumes-from interface-configurator-jenkins \
  -v "$NPM_CACHE_DIR:/root/.npm" \
  -w "$PWD" \
  node:22 \
  npm run build:prod

echo "Frontend build completed successfully"

