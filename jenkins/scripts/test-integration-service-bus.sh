#!/bin/bash
set -e

export PATH="/usr/bin:/usr/local/bin:$PATH"

NUGET_PACKAGES_DIR="$PWD/.nuget/packages"
mkdir -p "$NUGET_PACKAGES_DIR"

echo "Running Service Bus integration tests..."
rm -f test-results/junit-integration-servicebus-*.xml
mkdir -p test-results

/usr/bin/docker run --rm \
  --volumes-from interface-configurator-jenkins \
  -v "$NUGET_PACKAGES_DIR:/root/.nuget/packages" \
  -e NUGET_PACKAGES=/root/.nuget/packages \
  -e AZURE_SERVICE_BUS_CONNECTION_STRING="${AZURE_SERVICE_BUS_CONNECTION_STRING:-}" \
  -w "$PWD" \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  dotnet test tests/main.Core.Tests/main.Core.Tests.csproj \
    --configuration "$BUILD_CONFIGURATION" \
    --no-build \
    --no-restore \
    --verbosity normal \
    --filter "FullyQualifiedName~ServiceBusIntegrationTests" \
    --logger "junit;LogFilePath=$PWD/test-results/junit-integration-servicebus-{assembly}.xml" \
    --results-directory "$PWD/test-results"

echo "Service Bus integration tests completed"

