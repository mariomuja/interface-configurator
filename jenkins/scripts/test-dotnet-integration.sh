#!/bin/bash
set -e

export PATH="/usr/bin:/usr/local/bin:$PATH"

# Use shared NuGet packages directory (same as build script)
NUGET_PACKAGES_DIR="$PWD/.nuget/packages"
mkdir -p "$NUGET_PACKAGES_DIR"

echo "Running integration tests..."
mkdir -p test-results

# Check if Azure credentials are available
if [ -z "$AZURE_STORAGE_CONNECTION_STRING" ]; then
    echo "WARNING: AZURE_STORAGE_CONNECTION_STRING not set - some integration tests may be skipped"
fi

# Using latest .NET 8 SDK (8.0 tag pulls the latest 8.0.x patch version)
# --no-build flag skips rebuild since we already built in previous stage
/usr/bin/docker run --rm \
  --volumes-from interface-configurator-jenkins \
  -v "$NUGET_PACKAGES_DIR:/root/.nuget/packages" \
  -e NUGET_PACKAGES=/root/.nuget/packages \
  -e AZURE_STORAGE_CONNECTION_STRING="${AZURE_STORAGE_CONNECTION_STRING:-}" \
  -e AZURE_SERVICE_BUS_CONNECTION_STRING="${AZURE_SERVICE_BUS_CONNECTION_STRING:-}" \
  -e AZURE_SQL_SERVER="${AZURE_SQL_SERVER:-}" \
  -e AZURE_SQL_DATABASE="${AZURE_SQL_DATABASE:-}" \
  -e AZURE_SQL_USER="${AZURE_SQL_USER:-}" \
  -e AZURE_SQL_PASSWORD="${AZURE_SQL_PASSWORD:-}" \
  -w "$PWD" \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test tests/main.Core.Tests/main.Core.Tests.csproj \
    --configuration "$BUILD_CONFIGURATION" \
    --no-build \
    --verbosity normal \
    --filter "FullyQualifiedName~Integration" \
    --logger "junit;LogFilePath=$PWD/test-results/junit-integration-{assembly}.xml" \
    --results-directory "$PWD/test-results"

echo "Integration tests completed"
echo "Test results location: $PWD/test-results"
ls -la test-results/ || echo "No test results found"

