# Failsafe Improvements - Making Everything More Resilient

This document outlines comprehensive improvements to make the system more failsafe, resilient, and production-ready.

## Table of Contents

1. [Database Connection Resilience](#database-connection-resilience)
2. [Retry Policies & Circuit Breakers](#retry-policies--circuit-breakers)
3. [Health Checks & Monitoring](#health-checks--monitoring)
4. [Error Handling Improvements](#error-handling-improvements)
5. [Deployment Safety](#deployment-safety)
6. [Infrastructure Resilience](#infrastructure-resilience)
7. [Frontend Resilience](#frontend-resilience)
8. [Testing & Validation](#testing--validation)

---

## Database Connection Resilience

### Current State
- Connection timeout: 30 seconds (fixed)
- No retry logic for transient failures
- No connection pooling configuration
- No health checks before operations

### Improvements

#### 1. Add Retry Policy with Polly (High Priority)

**Install Polly NuGet package:**
```bash
dotnet add azure-functions/main/main.csproj package Polly
dotnet add azure-functions/main/main.csproj package Microsoft.Extensions.Http.Polly
```

**Create `RetryPolicyHelper.cs`:**
```csharp
using Polly;
using Polly.Retry;
using Microsoft.Data.SqlClient;

public static class RetryPolicyHelper
{
    public static AsyncRetryPolicy GetSqlRetryPolicy(int maxRetries = 3)
    {
        return Policy
            .Handle<SqlException>(ex => IsTransientError(ex))
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: maxRetries,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    Console.WriteLine($"Retry {retryCount}/{maxRetries} after {timespan.TotalSeconds}s");
                });
    }

    private static bool IsTransientError(SqlException ex)
    {
        // SQL Server transient error numbers
        var transientErrors = new[] { 2, 53, 121, 233, 10053, 10054, 10060, 40197, 40501, 40613, 49918, 49919, 49920 };
        return transientErrors.Contains(ex.Number);
    }
}
```

**Update `Program.cs` to use retry policy:**
```csharp
// Add connection resilience
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
        sqlOptions.CommandTimeout(60); // Increase timeout for retries
    }));

services.AddDbContext<MessageBoxDbContext>(options =>
    options.UseSqlServer(messageBoxConnectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
        sqlOptions.CommandTimeout(60);
    }));
```

#### 2. Add Connection Health Check

**Create `DatabaseHealthCheck.cs`:**
```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _context;

    public DatabaseHealthCheck(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple query to test connection
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            
            if (canConnect)
            {
                // Test query
                await _context.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
                return HealthCheckResult.Healthy("Database connection is healthy");
            }
            
            return HealthCheckResult.Unhealthy("Cannot connect to database");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database health check failed", ex);
        }
    }
}
```

**Register health check in `Program.cs`:**
```csharp
services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<MessageBoxHealthCheck>("messagebox");
```

#### 3. Add Connection Pooling Configuration

**Update connection strings:**
```csharp
var connectionString = $"Server=tcp:{sqlServer},1433;Initial Catalog={sqlDatabase};" +
    "Persist Security Info=False;User ID={sqlUser};Password={sqlPassword};" +
    "MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;" +
    "Connection Timeout=30;Pooling=true;Min Pool Size=5;Max Pool Size=100;" +
    "Connection Lifetime=0;"; // 0 = connection never expires
```

---

## Retry Policies & Circuit Breakers

### Current State
- Basic retry logic in MessageBoxService
- No circuit breaker pattern
- No exponential backoff

### Improvements

#### 1. Implement Circuit Breaker Pattern

**Create `CircuitBreakerService.cs`:**
```csharp
using Polly;
using Polly.CircuitBreaker;

public class CircuitBreakerService
{
    private readonly AsyncCircuitBreakerPolicy _circuitBreaker;

    public CircuitBreakerService()
    {
        _circuitBreaker = Policy
            .Handle<SqlException>(ex => IsTransientError(ex))
            .Or<TimeoutException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (exception, duration) =>
                {
                    Console.WriteLine($"Circuit breaker opened for {duration}");
                },
                onReset: () =>
                {
                    Console.WriteLine("Circuit breaker reset");
                });
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        return await _circuitBreaker.ExecuteAsync(action);
    }
}
```

#### 2. Add Retry with Circuit Breaker to Critical Operations

**Update `SqlServerAdapter.cs`:**
```csharp
private readonly CircuitBreakerService _circuitBreaker;

public async Task WriteAsync(...)
{
    var policy = Policy
        .Handle<SqlException>(ex => IsTransientError(ex))
        .Or<TimeoutException>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                _logger?.LogWarning("Retry {RetryCount}/3 after {Delay}s", retryCount, timespan.TotalSeconds);
            })
        .WrapAsync(_circuitBreaker.GetPolicy());

    await policy.ExecuteAsync(async () =>
    {
        // Existing write logic
    });
}
```

---

## Health Checks & Monitoring

### Current State
- Basic health check script exists
- No automated health monitoring
- No alerting

### Improvements

#### 1. Add HTTP Health Check Endpoint

**Create `HealthCheckFunction.cs`:**
```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net;

public class HealthCheckFunction
{
    private readonly HealthCheckService _healthCheckService;

    public HealthCheckFunction(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    [Function("health")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req,
        FunctionContext context)
    {
        var healthCheckResult = await _healthCheckService.CheckHealthAsync();
        
        var response = req.CreateResponse(healthCheckResult.Status == HealthStatus.Healthy 
            ? HttpStatusCode.OK 
            : HttpStatusCode.ServiceUnavailable);
        
        await response.WriteAsJsonAsync(new
        {
            status = healthCheckResult.Status.ToString(),
            checks = healthCheckResult.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                exception = e.Value.Exception?.Message
            })
        });
        
        return response;
    }
}
```

#### 2. Add Application Insights Custom Metrics

**Create `MetricsService.cs`:**
```csharp
using Microsoft.ApplicationInsights;

public class MetricsService
{
    private readonly TelemetryClient _telemetryClient;

    public MetricsService(TelemetryClient telemetryClient)
    {
        _telemetryClient = telemetryClient;
    }

    public void TrackMessageProcessed(string adapterName, int recordCount, TimeSpan duration)
    {
        _telemetryClient.TrackMetric("MessagesProcessed", recordCount, new Dictionary<string, string>
        {
            { "Adapter", adapterName },
            { "Duration", duration.TotalSeconds.ToString() }
        });
    }

    public void TrackError(string adapterName, string errorType, Exception ex)
    {
        _telemetryClient.TrackException(ex, new Dictionary<string, string>
        {
            { "Adapter", adapterName },
            { "ErrorType", errorType }
        });
    }
}
```

#### 3. Add GitHub Actions Health Check After Deployment

**Update `.github/workflows/deploy-functions.yml`:**
```yaml
- name: Health Check After Deployment
  run: |
    echo "Waiting for Function App to be ready..."
    sleep 30
    
    MAX_RETRIES=5
    RETRY_COUNT=0
    
    while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
      HEALTH_STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
        "https://${{ secrets.AZURE_FUNCTIONAPP_NAME }}.azurewebsites.net/api/health" || echo "000")
      
      if [ "$HEALTH_STATUS" = "200" ]; then
        echo "✅ Health check passed!"
        exit 0
      fi
      
      RETRY_COUNT=$((RETRY_COUNT + 1))
      echo "Health check attempt $RETRY_COUNT/$MAX_RETRIES failed (HTTP $HEALTH_STATUS), retrying..."
      sleep 10
    done
    
    echo "❌ Health check failed after $MAX_RETRIES attempts"
    exit 1
  continue-on-error: false
```

---

## Error Handling Improvements

### Current State
- Good error logging
- Basic retry logic
- No dead letter queue monitoring

### Improvements

#### 1. Add Dead Letter Queue Monitoring

**Create `DeadLetterMonitor.cs`:**
```csharp
public class DeadLetterMonitor
{
    private readonly MessageBoxDbContext _context;
    private readonly ILogger<DeadLetterMonitor> _logger;

    public async Task<int> GetDeadLetterCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Messages
            .CountAsync(m => m.Status == "DeadLetter", cancellationToken);
    }

    public async Task<List<Message>> GetRecentDeadLettersAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        return await _context.Messages
            .Where(m => m.Status == "DeadLetter")
            .OrderByDescending(m => m.datetime_processed)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}
```

#### 2. Add Error Classification

**Create `ErrorClassifier.cs`:**
```csharp
public enum ErrorCategory
{
    Transient,      // Retryable (network, timeout)
    Permanent,      // Not retryable (invalid data, permission)
    Configuration,  // Configuration error
    Unknown
}

public static class ErrorClassifier
{
    public static ErrorCategory Classify(Exception ex)
    {
        return ex switch
        {
            SqlException sqlEx when IsTransientError(sqlEx) => ErrorCategory.Transient,
            TimeoutException => ErrorCategory.Transient,
            UnauthorizedAccessException => ErrorCategory.Permanent,
            ArgumentException => ErrorCategory.Permanent,
            InvalidOperationException => ErrorCategory.Configuration,
            _ => ErrorCategory.Unknown
        };
    }
}
```

#### 3. Improve Blob Movement Error Handling

**Update `main.cs` MoveBlobToFolderAsync:**
```csharp
private async Task MoveBlobToFolderAsync(string containerName, string sourceFolder, string targetFolder, string blobName, string reason)
{
    if (_blobServiceClient == null)
    {
        _logger.LogWarning("BlobServiceClient not initialized. Cannot move blob {BlobName}", blobName);
        return;
    }

    var retryPolicy = Policy
        .Handle<RequestFailedException>(ex => ex.Status == 409 || ex.Status >= 500) // Conflict or server error
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

    await retryPolicy.ExecuteAsync(async () =>
    {
        // Existing blob movement logic
    });
}
```

---

## Deployment Safety

### Current State
- Basic deployment workflow
- No rollback mechanism
- No pre-deployment validation

### Improvements

#### 1. Add Pre-Deployment Validation

**Create `.github/workflows/pre-deployment-checks.yml`:**
```yaml
name: Pre-Deployment Checks

on:
  pull_request:
    paths:
      - 'azure-functions/**'
      - '.github/workflows/**'

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      
      - name: Validate Build
        run: |
          cd azure-functions/main
          dotnet build --configuration Release --no-restore
      
      - name: Run Tests
        run: |
          cd azure-functions/main.Core.Tests
          dotnet test --configuration Release --no-build
      
      - name: Check for Secrets
        run: |
          if grep -r "password\|secret\|key" --include="*.cs" azure-functions/ | grep -v "//"; then
            echo "⚠️  Potential secrets found in code"
            exit 1
          fi
```

#### 2. Add Deployment Rollback Script

**Create `terraform/rollback-deployment.ps1`:**
```powershell
param(
    [string]$ResourceGroup = "rg-infrastructure-as-code",
    [string]$FunctionAppName = "func-integration-main",
    [string]$PreviousVersion = ""
)

Write-Host "Rolling back Function App deployment..." -ForegroundColor Yellow

# Get current deployment
$currentDeployment = az functionapp deployment list-publishing-profiles `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --query "[0]" -o json | ConvertFrom-Json

if ($PreviousVersion) {
    Write-Host "Reverting to version: $PreviousVersion" -ForegroundColor Cyan
    # Restore from backup or previous deployment
} else {
    Write-Host "Restarting Function App to clear any issues..." -ForegroundColor Cyan
    az functionapp restart --resource-group $ResourceGroup --name $FunctionAppName
}

Write-Host "Rollback complete" -ForegroundColor Green
```

#### 3. Add Terraform State Backup

**Create `terraform/backup-state.ps1`:**
```powershell
# Backup Terraform state before apply
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupPath = "terraform-state-backup-$timestamp.json"

Write-Host "Backing up Terraform state..." -ForegroundColor Yellow
az storage blob download `
    --account-name $env:TF_STATE_STORAGE_ACCOUNT `
    --container-name $env:TF_STATE_CONTAINER `
    --name terraform.tfstate `
    --file $backupPath

Write-Host "State backed up to: $backupPath" -ForegroundColor Green
```

---

## Infrastructure Resilience

### Current State
- Basic Terraform configuration
- No state locking
- No backup strategy

### Improvements

#### 1. Add Terraform State Locking

**Update `terraform/backend.tf`:**
```hcl
terraform {
  backend "azurerm" {
    resource_group_name  = "rg-terraform-state"
    storage_account_name = "stterraformstate"
    container_name       = "tfstate"
    key                  = "interface-configurator.terraform.tfstate"
    
    # Enable state locking
    use_azuread_auth     = true
  }
}
```

#### 2. Add Terraform Validation

**Create `.github/workflows/terraform-validate.yml`:**
```yaml
name: Terraform Validate

on:
  pull_request:
    paths:
      - 'terraform/**'

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup Terraform
        uses: hashicorp/setup-terraform@v3
      
      - name: Terraform Init
        run: |
          cd terraform
          terraform init -backend=false
      
      - name: Terraform Validate
        run: |
          cd terraform
          terraform validate
      
      - name: Terraform Format Check
        run: |
          cd terraform
          terraform fmt -check
```

#### 3. Add Resource Tagging for Disaster Recovery

**Update `terraform/main.tf`:**
```hcl
resource "azurerm_linux_function_app" "main" {
  # ... existing configuration ...
  
  tags = {
    Environment     = var.environment
    BackupRequired  = "true"
    BackupSchedule  = "daily"
    DRPriority      = "high"
    LastBackup      = timestamp()
  }
}
```

---

## Frontend Resilience

### Current State
- Basic error handling
- No retry logic for API calls
- No offline detection

### Improvements

#### 1. Add Retry Logic to API Calls

**Update `frontend/src/app/services/transport.service.ts`:**
```typescript
import { retry, catchError } from 'rxjs/operators';
import { throwError, timer } from 'rxjs';

private httpRequestWithRetry<T>(request: Observable<T>, maxRetries = 3): Observable<T> {
  return request.pipe(
    retry({
      count: maxRetries,
      delay: (error, retryCount) => {
        console.log(`Retry attempt ${retryCount}/${maxRetries}`);
        return timer(Math.pow(2, retryCount) * 1000); // Exponential backoff
      }
    }),
    catchError(error => {
      console.error('Request failed after retries:', error);
      return throwError(() => error);
    })
  );
}
```

#### 2. Add Offline Detection

**Create `frontend/src/app/services/network.service.ts`:**
```typescript
import { Injectable } from '@angular/core';
import { BehaviorSubject, fromEvent } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class NetworkService {
  private onlineStatus = new BehaviorSubject<boolean>(navigator.onLine);
  
  public online$ = this.onlineStatus.asObservable();

  constructor() {
    fromEvent(window, 'online').subscribe(() => {
      this.onlineStatus.next(true);
    });
    
    fromEvent(window, 'offline').subscribe(() => {
      this.onlineStatus.next(false);
    });
  }
}
```

#### 3. Add Error Boundary Component

**Create `frontend/src/app/components/error-boundary/error-boundary.component.ts`:**
```typescript
import { Component, ErrorHandler } from '@angular/core';

@Component({
  selector: 'app-error-boundary',
  template: `
    <div *ngIf="hasError" class="error-boundary">
      <h2>Something went wrong</h2>
      <p>{{ errorMessage }}</p>
      <button (click)="retry()">Retry</button>
    </div>
  `
})
export class ErrorBoundaryComponent {
  hasError = false;
  errorMessage = '';

  constructor(private errorHandler: ErrorHandler) {}

  handleError(error: Error) {
    this.hasError = true;
    this.errorMessage = error.message;
    this.errorHandler.handleError(error);
  }

  retry() {
    window.location.reload();
  }
}
```

---

## Testing & Validation

### Current State
- Basic unit tests exist
- No integration tests
- No load testing

### Improvements

#### 1. Add Integration Tests

**Create `azure-functions/main.Core.Tests/IntegrationTests.cs`:**
```csharp
public class IntegrationTests : IClassFixture<TestFixture>
{
    [Fact]
    public async Task DatabaseConnection_ShouldSucceed()
    {
        // Test database connectivity
    }

    [Fact]
    public async Task BlobStorage_ShouldReadWrite()
    {
        // Test blob storage operations
    }
}
```

#### 2. Add Load Testing

**Create `load-test.yml`:**
```yaml
name: Load Test

on:
  schedule:
    - cron: '0 2 * * *' # Daily at 2 AM

jobs:
  load-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Run Load Test
        run: |
          # Use k6 or similar tool
          k6 run load-test.js
```

#### 3. Add Chaos Engineering Tests

**Create `chaos-tests.yml`:**
```yaml
# Test system resilience by simulating failures
- Simulate database connection loss
- Simulate blob storage unavailability
- Simulate high latency
- Verify retry mechanisms work
```

---

## Implementation Priority

### Phase 1 (Critical - Week 1)
1. ✅ Database connection retry policy
2. ✅ Health check endpoint
3. ✅ Pre-deployment validation
4. ✅ Error classification

### Phase 2 (High Priority - Week 2)
1. Circuit breaker pattern
2. Dead letter queue monitoring
3. Frontend retry logic
4. Terraform state locking

### Phase 3 (Medium Priority - Week 3)
1. Application Insights custom metrics
2. Deployment rollback mechanism
3. Load testing
4. Offline detection

### Phase 4 (Nice to Have - Week 4)
1. Chaos engineering tests
2. Advanced monitoring dashboards
3. Automated alerting
4. Disaster recovery procedures

---

## Monitoring Checklist

- [ ] Set up Application Insights alerts for:
  - [ ] High error rate (> 5% of requests)
  - [ ] Slow response times (> 5 seconds)
  - [ ] Database connection failures
  - [ ] Dead letter queue growth
  - [ ] Function App downtime

- [ ] Create dashboards for:
  - [ ] System health overview
  - [ ] Error trends
  - [ ] Performance metrics
  - [ ] Message processing rates

---

## Next Steps

1. Review and prioritize improvements
2. Create GitHub issues for each improvement
3. Implement Phase 1 improvements
4. Test improvements in staging environment
5. Deploy to production with monitoring

