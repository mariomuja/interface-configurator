#!/bin/bash
set -e

export PATH="/usr/bin:/usr/local/bin:$PATH"

# Use shared NuGet packages directory (same as build script)
NUGET_PACKAGES_DIR="$PWD/.nuget/packages"
mkdir -p "$NUGET_PACKAGES_DIR"

echo "Packaging .NET artifacts..."
mkdir -p "$ARTIFACTS_PATH"

# Using latest .NET 9 SDK (9.0 tag pulls the latest 9.0.x patch version)
/usr/bin/docker run --rm \
  --volumes-from interface-configurator-jenkins \
  -v "$NUGET_PACKAGES_DIR:/root/.nuget/packages" \
  -e NUGET_PACKAGES=/root/.nuget/packages \
  -w "$PWD" \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  dotnet publish azure-functions/main/main.csproj --configuration "$BUILD_CONFIGURATION" --output "$ARTIFACTS_PATH/azure-functions"

/usr/bin/docker run --rm \
  --volumes-from interface-configurator-jenkins \
  -v "$NUGET_PACKAGES_DIR:/root/.nuget/packages" \
  -e NUGET_PACKAGES=/root/.nuget/packages \
  -w "$PWD" \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  dotnet publish main.Core/main.Core.csproj --configuration "$BUILD_CONFIGURATION" --output "$ARTIFACTS_PATH/main.Core"

echo "Packaging completed"


