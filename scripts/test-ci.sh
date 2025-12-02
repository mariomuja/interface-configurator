#!/bin/bash

# CI/CD Test Integration Script
# This script runs tests in CI/CD environments

set -e

echo "🚀 Starting CI/CD Test Execution"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
TEST_TYPE="${TEST_TYPE:-all}"
COVERAGE_THRESHOLD="${COVERAGE_THRESHOLD:-70}"
PARALLEL="${PARALLEL:-true}"

# Function to print colored output
print_status() {
    echo -e "${GREEN}✓${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

# Function to run unit tests
run_unit_tests() {
    echo "📦 Running Unit Tests..."
    cd frontend
    
    if [ "$PARALLEL" = "true" ]; then
        npm test -- --code-coverage --browsers=ChromeHeadless --watch=false
    else
        npm test -- --code-coverage --browsers=ChromeHeadless --watch=false --single-run
    fi
    
    # Check coverage
    if [ -f "coverage/interface-configurator/coverage-summary.json" ]; then
        COVERAGE=$(node -e "
            const coverage = require('./coverage/interface-configurator/coverage-summary.json');
            const lines = coverage.total.lines.pct;
            console.log(lines);
        ")
        
        if (( $(echo "$COVERAGE < $COVERAGE_THRESHOLD" | bc -l) )); then
            print_error "Coverage ${COVERAGE}% is below threshold ${COVERAGE_THRESHOLD}%"
            exit 1
        else
            print_status "Coverage ${COVERAGE}% meets threshold ${COVERAGE_THRESHOLD}%"
        fi
    fi
    
    cd ..
}

# Function to run E2E tests
run_e2e_tests() {
    echo "🌐 Running E2E Tests..."
    
    # Install Playwright browsers if needed
    npx playwright install --with-deps chromium || true
    
    # Run E2E tests
    npm run test:e2e
    
    print_status "E2E tests completed"
}

# Function to generate test report
generate_test_report() {
    echo "📊 Generating Test Report..."
    
    # Create reports directory
    mkdir -p test-reports
    
    # Copy coverage reports
    if [ -d "frontend/coverage" ]; then
        cp -r frontend/coverage test-reports/ || true
    fi
    
    # Copy Playwright reports
    if [ -d "test-results" ]; then
        cp -r test-results test-reports/ || true
    fi
    
    print_status "Test report generated in test-reports/"
}

# Main execution
main() {
    case "$TEST_TYPE" in
        unit)
            run_unit_tests
            ;;
        e2e)
            run_e2e_tests
            ;;
        all)
            run_unit_tests
            run_e2e_tests
            ;;
        *)
            print_error "Unknown test type: $TEST_TYPE"
            print_warning "Valid types: unit, e2e, all"
            exit 1
            ;;
    esac
    
    generate_test_report
    
    print_status "All tests completed successfully!"
}

# Run main function
main
