#!/bin/bash
set -e

export PATH="/usr/bin:/usr/local/bin:$PATH"

# Use shared NuGet packages directory
NUGET_PACKAGES_DIR="$PWD/.nuget/packages"
mkdir -p "$NUGET_PACKAGES_DIR"

echo "Running unit tests in parallel (fast + slow tests)..."
rm -rf test-results
mkdir -p test-results

# Determine test filter based on branch
if [[ "$BRANCH_NAME" == "main" ]] || [[ "$GIT_BRANCH" == *"main"* ]]; then
  INCLUDE_PERFORMANCE="true"
  echo "Including performance tests (main branch)"
else
  INCLUDE_PERFORMANCE="false"
  echo "Excluding performance tests (ready/* branch - saves ~30s)"
fi

# Run fast tests (< 1 second each) in parallel with slow tests
# This significantly reduces total test time

echo "Starting fast tests in background..."
(/usr/bin/docker run --rm \
  --volumes-from interface-configurator-jenkins \
  -v "$NUGET_PACKAGES_DIR:/root/.nuget/packages" \
  -e NUGET_PACKAGES=/root/.nuget/packages \
  -w "$PWD" \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  dotnet test tests/main.Core.Tests/main.Core.Tests.csproj \
    --configuration "$BUILD_CONFIGURATION" \
    --no-build \
    --no-restore \
    --verbosity normal \
    --filter "FullyQualifiedName!~Integration&FullyQualifiedName!~Performance&FullyQualifiedName!~ServiceBusLockTrackingServiceTests&FullyQualifiedName!~InterfaceConfigurationServiceTests&FullyQualifiedName!~MessageDeduplicationServiceTests" \
    --logger "junit;LogFilePath=$PWD/test-results/junit-fast-{assembly}.xml" \
    --results-directory "$PWD/test-results" && echo "✅ Fast tests completed") &
FAST_PID=$!

echo "Starting slow tests in background..."
if [ "$INCLUDE_PERFORMANCE" = "true" ]; then
  SLOW_FILTER="(FullyQualifiedName~ServiceBusLockTrackingServiceTests|FullyQualifiedName~InterfaceConfigurationServiceTests|FullyQualifiedName~MessageDeduplicationServiceTests|FullyQualifiedName~Performance)&FullyQualifiedName!~Integration"
else
  SLOW_FILTER="(FullyQualifiedName~ServiceBusLockTrackingServiceTests|FullyQualifiedName~InterfaceConfigurationServiceTests|FullyQualifiedName~MessageDeduplicationServiceTests)&FullyQualifiedName!~Integration&FullyQualifiedName!~Performance"
fi

(/usr/bin/docker run --rm \
  --volumes-from interface-configurator-jenkins \
  -v "$NUGET_PACKAGES_DIR:/root/.nuget/packages" \
  -e NUGET_PACKAGES=/root/.nuget/packages \
  -w "$PWD" \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  dotnet test tests/main.Core.Tests/main.Core.Tests.csproj \
    --configuration "$BUILD_CONFIGURATION" \
    --no-build \
    --no-restore \
    --verbosity normal \
    --filter "$SLOW_FILTER" \
    --logger "junit;LogFilePath=$PWD/test-results/junit-slow-{assembly}.xml" \
    --results-directory "$PWD/test-results" && echo "✅ Slow tests completed") &
SLOW_PID=$!

# Wait for both test runs to complete
echo "Waiting for parallel test execution to complete..."
wait $FAST_PID
FAST_EXIT=$?
wait $SLOW_PID
SLOW_EXIT=$?

# Check if either failed
if [ $FAST_EXIT -ne 0 ]; then
  echo "❌ Fast tests failed with exit code $FAST_EXIT"
  exit $FAST_EXIT
fi

if [ $SLOW_EXIT -ne 0 ]; then
  echo "❌ Slow tests failed with exit code $SLOW_EXIT"
  exit $SLOW_EXIT
fi

echo "✅ All unit tests completed successfully"
echo "Test results location: $PWD/test-results"
ls -la test-results/ || echo "No test results found"

