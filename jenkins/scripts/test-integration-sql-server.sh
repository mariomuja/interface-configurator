#!/bin/bash
set -e

export PATH="/usr/bin:/usr/local/bin:$PATH"

NUGET_PACKAGES_DIR="$PWD/.nuget/packages"
mkdir -p "$NUGET_PACKAGES_DIR"

echo "Running SQL Server integration tests..."
rm -f test-results/junit-integration-sql-*.xml
mkdir -p test-results

/usr/bin/docker run --rm \
  --volumes-from interface-configurator-jenkins \
  -v "$NUGET_PACKAGES_DIR:/root/.nuget/packages" \
  -e NUGET_PACKAGES=/root/.nuget/packages \
  -e AZURE_SQL_SERVER="${AZURE_SQL_SERVER:-}" \
  -e AZURE_SQL_DATABASE="${AZURE_SQL_DATABASE:-}" \
  -e AZURE_SQL_USER="${AZURE_SQL_USER:-}" \
  -e AZURE_SQL_PASSWORD="${AZURE_SQL_PASSWORD:-}" \
  -w "$PWD" \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test tests/main.Core.Tests/main.Core.Tests.csproj \
    --configuration "$BUILD_CONFIGURATION" \
    --no-build \
    --no-restore \
    --verbosity normal \
    --filter "FullyQualifiedName~SqlServerIntegrationTests" \
    --logger "junit;LogFilePath=$PWD/test-results/junit-integration-sql-{assembly}.xml" \
    --results-directory "$PWD/test-results"

echo "SQL Server integration tests completed"

