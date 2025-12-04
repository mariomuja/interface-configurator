#!/bin/bash
set -e

export PATH="/usr/bin:/usr/local/bin:$PATH"

# Use shared NuGet packages directory (same as build script)
NUGET_PACKAGES_DIR="$PWD/.nuget/packages"
mkdir -p "$NUGET_PACKAGES_DIR"

echo "Running all unit tests (excluding integration tests)..."
# Clean old test results to avoid false UNSTABLE status
rm -rf test-results
mkdir -p test-results

# Determine test filter based on branch
# Skip performance tests on ready/* branches to speed up pipeline
if [[ "$BRANCH_NAME" == "main" ]] || [[ "$GIT_BRANCH" == *"main"* ]]; then
  TEST_FILTER="FullyQualifiedName!~Integration"
  echo "Running all unit tests including performance tests (main branch)"
else
  TEST_FILTER="FullyQualifiedName!~Integration&FullyQualifiedName!~Performance"
  echo "Running unit tests excluding performance tests (ready/* branch - saves ~30s)"
fi

# Using latest .NET 8 SDK (8.0 tag pulls the latest 8.0.x patch version)
# --no-build and --no-restore flags skip rebuild and restore since we already did them
/usr/bin/docker run --rm \
  --volumes-from interface-configurator-jenkins \
  -v "$NUGET_PACKAGES_DIR:/root/.nuget/packages" \
  -e NUGET_PACKAGES=/root/.nuget/packages \
  -e BRANCH_NAME="${BRANCH_NAME:-}" \
  -e GIT_BRANCH="${GIT_BRANCH:-}" \
  -w "$PWD" \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test tests/main.Core.Tests/main.Core.Tests.csproj \
    --configuration "$BUILD_CONFIGURATION" \
    --no-build \
    --no-restore \
    --verbosity normal \
    --filter "$TEST_FILTER" \
    --logger "junit;LogFilePath=$PWD/test-results/junit-{assembly}.xml" \
    --results-directory "$PWD/test-results"

echo "Unit tests completed"
echo "Test results location: $PWD/test-results"
ls -la test-results/ || echo "No test results found"


