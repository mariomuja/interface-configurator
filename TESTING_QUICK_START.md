# Testing Quick Start Guide

## 🚀 Quick Start

### Run All Tests
```bash
# Unit tests
cd frontend && npm test

# E2E tests
npm run test:e2e

# Both (from root)
npm test && npm run test:e2e
```

### Run with Coverage
```bash
cd frontend
npm test -- --code-coverage
```

### Run E2E with UI
```bash
npm run test:e2e:ui
```

## 📚 Common Test Utilities

### DOM Queries
```typescript
import { TestUtils } from './testing/test-utils';

const button = TestUtils.query(fixture, 'button.submit');
TestUtils.click(fixture, 'button.submit');
TestUtils.setInputValue(fixture, 'input[name="email"]', 'test@example.com');
```

### Mock Data
```typescript
import { MockDataFactory } from './testing/mock-data-factory';

const record = MockDataFactory.createCsvRecord({ name: 'Test' });
const logs = MockDataFactory.createProcessLogs(10);
```

### Fluent Builders
```typescript
import { CsvRecordBuilder } from './testing/test-data-builders';

const record = CsvRecordBuilder.create()
  .withId(1)
  .withName('Test')
  .withEmail('test@example.com')
  .build();
```

### Test Fixtures
```typescript
import { TestFixtures } from './testing/test-fixtures';

const scenario = TestFixtures.createTransportScenario();
const fixture = await TestFixtures.createComponentFixture(MyComponent);
```

## 🎯 Test Structure

### Component Test Template
```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MyComponent } from './my-component.component';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { TestUtils } from '../testing/test-utils';
import { MockDataFactory } from '../testing/mock-data-factory';

describe('MyComponent', () => {
  let component: MyComponent;
  let fixture: ComponentFixture<MyComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MyComponent, NoopAnimationsModule]
    }).compileComponents();

    fixture = TestBed.createComponent(MyComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should display data', () => {
    const data = MockDataFactory.createCsvRecords(5);
    component.data = data;
    fixture.detectChanges();

    const elements = TestUtils.queryAll(fixture, '.data-item');
    expect(elements.length).toBe(5);
  });
});
```

### Service Test Template
```typescript
import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { MyService } from './my-service.service';
import { MockDataFactory } from '../testing/mock-data-factory';

describe('MyService', () => {
  let service: MyService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [MyService]
    });
    service = TestBed.inject(MyService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should fetch data', () => {
    const mockData = MockDataFactory.createCsvRecords(3);
    
    service.getData().subscribe(data => {
      expect(data).toEqual(mockData);
    });

    const req = httpMock.expectOne('/api/data');
    expect(req.request.method).toBe('GET');
    req.flush(mockData);
  });
});
```

## 🔍 Debugging Tests

### Enable Verbose Logging
```typescript
import { TestDebuggingHelpers } from './testing/test-debugging-helpers';

TestDebuggingHelpers.enableVerboseLogging();

const debug = TestDebuggingHelpers.createDebugContext('My Test');
debug.logState(component);
debug.logHtml(fixture);
debug.end();
```

### Test Isolation
```typescript
import { TestIsolationHelpers } from './testing/test-isolation-helpers';

beforeEach(() => {
  TestIsolationHelpers.cleanupAll();
});
```

## 📊 Coverage

### View Coverage Report
```bash
cd frontend
npm test -- --code-coverage
# Open: frontend/coverage/interface-configurator/index.html
```

### Check Coverage Thresholds
```typescript
import { CoverageVisualization } from './testing/coverage-visualization';

const check = CoverageVisualization.checkThresholds(coverageData, {
  statements: 70,
  branches: 65,
  functions: 70,
  lines: 70
});
```

## 🌐 E2E Testing

### Basic E2E Test
```typescript
import { test, expect } from '@playwright/test';

test('should load page', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveTitle(/Interface Configurator/);
});
```

### Mock API in E2E
```typescript
import { MockServerHelpers } from '../testing/mock-server-helpers';

test('should handle API response', async ({ page }) => {
  MockServerHelpers.mockApiEndpoint(page, '**/api/data', { data: 'test' });
  await page.goto('/');
  // Test with mocked API
});
```

## 🎨 Visual Regression

### Visual Test
```typescript
import { test, expect } from '@playwright/test';

test('should match screenshot', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveScreenshot('main-page.png');
});
```

## 📈 Performance Testing

### Performance Assertion
```typescript
import { assertPerformanceThreshold } from './testing/performance-test-helpers';

it('should execute quickly', async () => {
  await assertPerformanceThreshold(
    () => component.processData(),
    100, // 100ms threshold
    'Data processing should complete within 100ms'
  );
});
```

## 🔄 Retry Strategies

### Retry on Network Error
```typescript
import { TestRetryStrategies } from './testing/test-retry-strategies';

await TestRetryStrategies.retryOnNetworkError(async () => {
  await component.loadData();
});
```

## 📊 Test Metrics

### Track Test Execution
```typescript
import { TestMetricsCollector } from './testing/test-metrics';

beforeEach(() => {
  TestMetricsCollector.startTest('My Test');
});

afterEach(() => {
  TestMetricsCollector.endTest('passed');
});

// Generate report
console.log(TestMetricsCollector.generateReport());
```

## 🚀 CI/CD

### Run Tests in CI
```bash
# Bash
./scripts/test-ci.sh

# PowerShell
.\scripts\test-ci.ps1

# With options
TEST_TYPE=unit COVERAGE_THRESHOLD=80 ./scripts/test-ci.sh
```

## 📖 More Information

- See `TESTING_GUIDE.md` for comprehensive documentation
- See `TEST_IMPROVEMENTS_COMPLETE_SUMMARY.md` for full overview
