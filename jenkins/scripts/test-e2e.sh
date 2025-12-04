#!/bin/bash
set -e

export PATH="/usr/bin:/usr/local/bin:$PATH"

echo "Running end-to-end tests..."

# Check if E2E tests directory exists
if [ ! -d "tests/end-to-end" ]; then
    echo "No end-to-end tests found in tests/end-to-end/"
    exit 0
fi

# Check if there are .spec.ts files
SPEC_COUNT=$(find tests/end-to-end -name "*.spec.ts" | wc -l)
if [ "$SPEC_COUNT" -eq 0 ]; then
    echo "No .spec.ts files found in tests/end-to-end/"
    exit 0
fi

echo "Found $SPEC_COUNT E2E test files"

# Create shared npm cache directory
NPM_CACHE_DIR="$PWD/.npm-cache"
mkdir -p "$NPM_CACHE_DIR"

# Check if package.json exists in E2E test directory
if [ ! -f "tests/end-to-end/package.json" ]; then
    echo "WARNING: No package.json found in tests/end-to-end/"
    echo "Skipping E2E tests - Playwright not configured"
    exit 0
fi

echo "Installing E2E test dependencies..."
/usr/bin/docker run --rm \
  --volumes-from interface-configurator-jenkins \
  -v "$NPM_CACHE_DIR:/root/.npm" \
  -w "$PWD/tests/end-to-end" \
  node:22 \
  npm ci --cache /root/.npm

echo "Running Playwright E2E tests..."
/usr/bin/docker run --rm \
  --volumes-from interface-configurator-jenkins \
  -v "$NPM_CACHE_DIR:/root/.npm" \
  -e BASE_URL="${E2E_BASE_URL:-http://localhost:4200}" \
  -e API_URL="${E2E_API_URL:-http://localhost:7071}" \
  -w "$PWD/tests/end-to-end" \
  mcr.microsoft.com/playwright:v1.40.0-jammy \
  npx playwright test

echo "E2E tests completed"

