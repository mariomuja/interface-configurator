# Pipeline Optimization Ideas

## Current Bottlenecks

### Slowest Stages:
1. **Test .NET unit** - ~37s (mainly `CsvProcessingServicePerformanceTests` with large files: 29s)
2. **Build .NET** - ~50s
3. **Integration tests** - Variable (depends on Azure)

## Optimization Strategies

### 1. ✅ ALREADY IMPLEMENTED
- ✅ Parallel builds (.NET + Frontend)
- ✅ Shallow git checkout (depth: 1)
- ✅ Shared NuGet cache (`.nuget/packages`)
- ✅ Shared npm cache (`.npm-cache`)
- ✅ Skip frontend build when no frontend changes
- ✅ Skip deployment stages on ready/* branches
- ✅ `--no-build` flag on tests (reuses build artifacts)

### 2. SKIP PERFORMANCE TESTS ON READY/* BRANCHES

**Current:** All tests run on every commit (including 29s performance tests)

**Optimization:** Skip performance tests on `ready/*`, run only on `main`

```bash
# In test-dotnet-unit.sh
if [ "$BRANCH_NAME" = "main" ]; then
  FILTER="FullyQualifiedName!~Integration"
else
  # Skip performance tests on ready/* branches
  FILTER="FullyQualifiedName!~Integration&FullyQualifiedName!~Performance"
fi

dotnet test --filter "$FILTER"
```

**Time saved:** ~30s on ready/* branches

### 3. PARALLEL TEST EXECUTION

**Current:** Tests run sequentially

**Optimization:** Use `dotnet test --parallel` or split test categories

```bash
# Run fast tests and slow tests in parallel
dotnet test --filter "FullyQualifiedName!~Performance&FullyQualifiedName!~Integration" &
PID1=$!

dotnet test --filter "FullyQualifiedName~Performance&FullyQualifiedName!~Integration" &
PID2=$!

wait $PID1 $PID2
```

**Time saved:** ~15-20s (if tests can run in parallel without conflicts)

### 4. DOCKER IMAGE CACHING

**Current:** Downloads `mcr.microsoft.com/dotnet/sdk:8.0` and `node:22` every time

**Optimization:** Pre-pull images in Jenkins startup or use local registry mirror

```bash
# In docker-entrypoint.sh or Jenkins init
docker pull mcr.microsoft.com/dotnet/sdk:8.0
docker pull node:22
docker pull hashicorp/terraform:latest
docker pull mcr.microsoft.com/playwright:v1.40.0-jammy
```

**Time saved:** ~5-10s per build

### 5. INCREMENTAL BUILDS

**Current:** Using `--no-incremental` in some builds

**Optimization:** Remove `--no-incremental` and rely on Docker volume caching

```bash
# In build-dotnet.sh
dotnet build --configuration Release  # Allow incremental
```

**Time saved:** ~10-15s on subsequent builds with no code changes

### 6. TEST RESULT CACHING

**Current:** All tests run every time

**Optimization:** Use `dotnet test --test-adapter-path` with selective test execution based on file changes

```bash
# Only run tests for changed files
CHANGED_FILES=$(git diff --name-only HEAD~1)
if echo "$CHANGED_FILES" | grep -q "Services/"; then
  dotnet test --filter "FullyQualifiedName~Services"
fi
```

**Time saved:** Variable, up to 50% on small changes

### 7. SPLIT UNIT TESTS INTO CATEGORIES

**Current:** All 158 unit tests run in one stage

**Optimization:** Split into parallel stages

```groovy
parallel {
    stage('Test: Fast Tests') {
        steps {
            sh 'dotnet test --filter "ExecutionTime<1000"'
        }
    }
    stage('Test: Slow Tests') {
        steps {
            sh 'dotnet test --filter "ExecutionTime>=1000"'
        }
    }
}
```

**Time saved:** ~15-20s

### 8. AGGRESSIVE SHALLOW CLONE

**Current:** `depth: 1`

**Optimization:** Add `--single-branch` and `--no-tags`

```groovy
[$class: 'CloneOption', depth: 1, shallow: true, noTags: true, reference: '']
```

**Time saved:** ~2-3s

### 9. SKIP RESTORE ON TESTS

**Current:** Test stage does `dotnet test` which includes implicit restore

**Optimization:** Add `--no-restore` since packages were already restored in build stage

```bash
dotnet test --no-build --no-restore
```

**Time saved:** ~3-5s

### 10. USE FASTER TEST LOGGER

**Current:** Using JUnit XML logger

**Optimization:** Only use JUnit on final stage, use `trx` for faster local logging

```bash
dotnet test --logger "trx;LogFileName=results.trx"
# Convert to JUnit only if needed
```

**Time saved:** ~2-3s

## Recommended Implementation Priority

### HIGH IMPACT (Implement Now):
1. ✅ Skip performance tests on ready/* branches → **Save 30s**
2. ✅ Add `--no-restore` to test command → **Save 5s**
3. ✅ Parallel test categories → **Save 15-20s**

### MEDIUM IMPACT (Implement Later):
4. Docker image pre-caching → Save 10s
5. Remove `--no-incremental` → Save 10-15s

### LOW IMPACT (Nice to Have):
6. Test result caching based on file changes
7. Aggressive shallow clone options
8. Faster test logger

## Expected Total Time Savings

**Current pipeline on ready/*:** ~2-3 minutes
**Optimized pipeline on ready/*:** ~1-1.5 minutes (50% faster!)

**Current pipeline on main:** ~4-5 minutes (with all stages)
**Optimized pipeline on main:** ~3-3.5 minutes

