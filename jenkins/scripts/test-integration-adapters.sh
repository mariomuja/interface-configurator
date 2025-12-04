#!/bin/bash
set -e

export PATH="/usr/bin:/usr/local/bin:$PATH"

NUGET_PACKAGES_DIR="$PWD/.nuget/packages"
mkdir -p "$NUGET_PACKAGES_DIR"

echo "Running Adapter Pipeline & Container integration tests..."
rm -f test-results/junit-integration-adapters-*.xml
mkdir -p test-results

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
  -e AZURE_CONTAINER_REGISTRY="${ACR_NAME:-}" \
  -e ACR_NAME="${ACR_NAME:-}" \
  -e AZURE_CLIENT_ID="${AZURE_CLIENT_ID:-}" \
  -e AZURE_CLIENT_SECRET="${AZURE_CLIENT_SECRET:-}" \
  -e AZURE_TENANT_ID="${AZURE_TENANT_ID:-}" \
  -w "$PWD" \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  dotnet test tests/main.Core.Tests/main.Core.Tests.csproj \
    --configuration "$BUILD_CONFIGURATION" \
    --no-build \
    --no-restore \
    --verbosity normal \
    --filter "(FullyQualifiedName~AdapterPipelineIntegrationTests|FullyQualifiedName~ContainerAppIntegrationTests|FullyQualifiedName~ContainerRegistryIntegrationTests)" \
    --logger "junit;LogFilePath=$PWD/test-results/junit-integration-adapters-{assembly}.xml" \
    --results-directory "$PWD/test-results"

echo "Adapter integration tests completed"

