#!/bin/bash
set -e

export PATH="/usr/bin:/usr/local/bin:$PATH"

echo "Analyzing changed files to determine which tests to run..."

# Get list of changed files (compared to previous commit)
CHANGED_FILES=$(git diff --name-only HEAD~1 2>/dev/null || echo "")

if [ -z "$CHANGED_FILES" ]; then
    echo "No changed files detected or first build - running all tests"
    bash jenkins/scripts/test-dotnet-unit-parallel.sh
    exit 0
fi

echo "Changed files:"
echo "$CHANGED_FILES"
echo ""

# Determine which test categories to run based on changed files
RUN_ALL=false
TEST_CATEGORIES=""

# Check for changes in different areas
if echo "$CHANGED_FILES" | grep -q "Services/"; then
    TEST_CATEGORIES="${TEST_CATEGORIES}|Services"
    echo "✓ Services changed - will run Services tests"
fi

if echo "$CHANGED_FILES" | grep -q "Adapters/"; then
    TEST_CATEGORIES="${TEST_CATEGORIES}|Adapters"
    echo "✓ Adapters changed - will run Adapters tests"
fi

if echo "$CHANGED_FILES" | grep -q "Functions/"; then
    TEST_CATEGORIES="${TEST_CATEGORIES}|Functions"
    echo "✓ Functions changed - will run Functions tests"
fi

if echo "$CHANGED_FILES" | grep -q "Models/"; then
    RUN_ALL=true
    echo "✓ Models changed - will run ALL tests (models affect everything)"
fi

if echo "$CHANGED_FILES" | grep -q "Middleware/"; then
    TEST_CATEGORIES="${TEST_CATEGORIES}|Middleware"
    echo "✓ Middleware changed - will run Middleware tests"
fi

# If core infrastructure changed, run all tests
if echo "$CHANGED_FILES" | grep -qE "(Program.cs|Startup.cs|\.csproj)"; then
    RUN_ALL=true
    echo "✓ Core infrastructure changed - will run ALL tests"
fi

# If test files themselves changed, run all tests
if echo "$CHANGED_FILES" | grep -q "Tests/"; then
    RUN_ALL=true
    echo "✓ Test files changed - will run ALL tests"
fi

# If no specific categories identified or forced to run all
if [ "$RUN_ALL" = "true" ] || [ -z "$TEST_CATEGORIES" ]; then
    echo ""
    echo "Running ALL unit tests..."
    bash jenkins/scripts/test-dotnet-unit-parallel.sh
else
    # Remove leading pipe
    TEST_CATEGORIES="${TEST_CATEGORIES:1}"
    echo ""
    echo "Running selective tests for categories: $TEST_CATEGORIES"
    
    # Use shared NuGet packages directory
    NUGET_PACKAGES_DIR="$PWD/.nuget/packages"
    mkdir -p "$NUGET_PACKAGES_DIR"
    rm -rf test-results
    mkdir -p test-results
    
    /usr/bin/docker run --rm \
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
        --filter "FullyQualifiedName~($TEST_CATEGORIES)&FullyQualifiedName!~Integration" \
        --logger "junit;LogFilePath=$PWD/test-results/junit-{assembly}.xml" \
        --results-directory "$PWD/test-results"
    
    echo "Selective tests completed"
fi

echo "Test execution completed"

