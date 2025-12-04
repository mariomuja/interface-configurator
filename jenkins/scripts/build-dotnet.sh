#!/bin/bash
set -e

export PATH="/usr/bin:/usr/local/bin:$PATH"

echo "Current directory: $PWD"
echo "Solution path: $SOLUTION_PATH"

# Create shared NuGet packages directory in workspace (persists across containers)
NUGET_PACKAGES_DIR="$PWD/.nuget/packages"
mkdir -p "$NUGET_PACKAGES_DIR"
echo "Using shared NuGet packages directory: $NUGET_PACKAGES_DIR"

echo "Restoring NuGet packages..."

# Jenkins runs in Docker container 'interface-configurator-jenkins'
# Use --volumes-from to share the Jenkins container's volumes (including workspace)
# Mount shared NuGet packages directory and set NUGET_PACKAGES environment variable
# Using latest .NET 9 SDK (9.0 tag pulls the latest 9.0.x patch version)
/usr/bin/docker run --rm \
  --volumes-from interface-configurator-jenkins \
  -v "$NUGET_PACKAGES_DIR:/root/.nuget/packages" \
  -e NUGET_PACKAGES=/root/.nuget/packages \
  -w "$PWD" \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  dotnet restore azure-functions/azure-functions.sln

echo "Building solution (incremental build enabled for speed)..."
/usr/bin/docker run --rm \
  --volumes-from interface-configurator-jenkins \
  -v "$NUGET_PACKAGES_DIR:/root/.nuget/packages" \
  -e NUGET_PACKAGES=/root/.nuget/packages \
  -w "$PWD" \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  dotnet build azure-functions/azure-functions.sln --configuration "$BUILD_CONFIGURATION" --no-restore

echo "Build completed successfully"


