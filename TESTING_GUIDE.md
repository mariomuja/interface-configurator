# Testing Guide

## Overview

This guide provides comprehensive information about testing practices, utilities, and best practices for the Interface Configurator application.

## Table of Contents

1. [Test Structure](#test-structure)
2. [Test Utilities](#test-utilities)
3. [Mock Data](#mock-data)
4. [Writing Tests](#writing-tests)
5. [Best Practices](#best-practices)
6. [Running Tests](#running-tests)
7. [Test Coverage](#test-coverage)

## Test Structure

### Unit Tests

Unit tests are located alongside their source files with the `.spec.ts` extension:

```
frontend/src/app/
  components/
    my-component/
      my-component.component.ts
      my-component.component.spec.ts
  services/
    my-service.ts
    my-service.spec.ts
```

### E2E Tests

End-to-end tests are located in the `e2e/` directory:

```
e2e/
  auth.spec.ts
  navigation.spec.ts
  adapter-configuration.spec.ts
  accessibility.spec.ts
```

### Test Utilities

Test utilities are located in `frontend/src/app/testing/`:

- `test-utils.ts` - DOM query and interaction helpers
- `mock-data-factory.ts` - Mock data generators
- `integration-test-helpers.ts` - Integration test utilities
- `performance-test-helpers.ts` - Performance testing utilities
- `component-lifecycle-helpers.ts` - Component lifecycle testing
- `router-test-helpers.ts` - Router testing utilities
- `component-interaction-helpers.ts` - Component interaction testing

## Test Utilities

### TestUtils

DOM query and interaction helpers:

```typescript
import { TestUtils } from '../testing/test-utils';

// Query element
const button = TestUtils.query<HTMLButtonElement>(fixture, 'button.submit');

// Click element
TestUtils.click(fixture, 'button.submit');

// Set input value
TestUtils.setInputValue(fixture, 'input[name="email"]', 'test@example.com');

// Query by test ID
const element = TestUtils.queryByTestId(fixture, 'submit-button');

// Check if element has class
const hasClass = TestUtils.hasClass(fixture, 'button', 'active');
```

### MockDataFactory

Create consistent mock data:

```typescript
import { MockDataFactory } from '../testing/mock-data-factory';

// Create mock records
const csvRecord = MockDataFactory.createCsvRecord({ name: 'Custom Name' });
const csvRecords = MockDataFactory.createCsvRecords(10);
const sqlRecord = MockDataFactory.createSqlRecord();
const processLog = MockDataFactory.createProcessLog({ level: 'error' });

// Create mock configurations
const config = MockDataFactory.createInterfaceConfiguration({
  interfaceName: 'TestInterface'
});

// Create mock HTTP errors
const error = MockDataFactory.createHttpError(404, 'Not Found');
```

### ComponentLifecycleHelpers

Test component lifecycle hooks:

```typescript
import { ComponentLifecycleHelpers } from '../testing/component-lifecycle-helpers';

// Test ngOnInit
const initSpy = spyOn(component, 'ngOnInit');
ComponentLifecycleHelpers.testOnInit(fixture, initSpy);

// Test ngOnDestroy
const destroySpy = spyOn(component, 'ngOnDestroy');
ComponentLifecycleHelpers.testOnDestroy(fixture, destroySpy);

// Test subscription cleanup
ComponentLifecycleHelpers.testSubscriptionCleanup(component, 'subscription');
```

### Performance Helpers

Measure and assert performance:

```typescript
import { assertPerformanceThreshold, measureExecutionTime } from '../testing/performance-test-helpers';

// Assert execution time
await assertPerformanceThreshold(
  () => component.processData(),
  100, // 100ms threshold
  'Data processing should complete within 100ms'
);

// Measure execution time
const { result, duration } = await measureExecutionTime(() => {
  return component.calculate();
});
expect(duration).toBeLessThan(50);
```

## Mock Data

### Using MockDataFactory

Always use `MockDataFactory` for creating test data:

```typescript
// Good
const record = MockDataFactory.createCsvRecord({ name: 'Test' });

// Bad
const record = { id: 1, name: 'Test', email: 'test@test.com' };
```

### Overriding Default Values

You can override default values:

```typescript
const record = MockDataFactory.createCsvRecord({
  name: 'Custom Name',
  email: 'custom@example.com'
});
```

## Writing Tests

### Component Tests

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
    const mockData = MockDataFactory.createCsvRecords(5);
    component.data = mockData;
    fixture.detectChanges();

    const dataElements = TestUtils.queryAll(fixture, '.data-item');
    expect(dataElements.length).toBe(5);
  });
});
```

### Service Tests

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

### E2E Tests

```typescript
import { test, expect } from '@playwright/test';

test.describe('Feature', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
  });

  test('should complete workflow', async ({ page }) => {
    const button = page.getByRole('button', { name: /submit/i });
    await button.click();
    
    await expect(page.getByText(/success/i)).toBeVisible();
  });
});
```

## Best Practices

### 1. Use Test Utilities

Always use test utilities instead of manual DOM queries:

```typescript
// Good
const button = TestUtils.query(fixture, 'button.submit');

// Bad
const button = fixture.nativeElement.querySelector('button.submit');
```

### 2. Use Mock Data Factory

Always use `MockDataFactory` for consistent test data:

```typescript
// Good
const record = MockDataFactory.createCsvRecord();

// Bad
const record = { id: 1, name: 'Test' };
```

### 3. Test Edge Cases

Always test edge cases:

```typescript
it('should handle empty data', () => {
  component.data = [];
  fixture.detectChanges();
  expect(component.isEmpty()).toBe(true);
});

it('should handle null values', () => {
  component.data = null;
  expect(() => fixture.detectChanges()).not.toThrow();
});
```

### 4. Test Error Handling

Always test error scenarios:

```typescript
it('should handle HTTP errors', () => {
  service.getData().subscribe({
    next: () => fail('should have failed'),
    error: (error) => {
      expect(error.status).toBe(500);
    }
  });

  const req = httpMock.expectOne('/api/data');
  req.flush(null, { status: 500, statusText: 'Server Error' });
});
```

### 5. Clean Up After Tests

Always clean up subscriptions and resources:

```typescript
afterEach(() => {
  httpMock.verify();
  fixture.destroy();
});
```

### 6. Use Descriptive Test Names

Test names should clearly describe what is being tested:

```typescript
// Good
it('should display error message when API call fails', () => { });

// Bad
it('should work', () => { });
```

### 7. Arrange-Act-Assert Pattern

Structure tests using AAA pattern:

```typescript
it('should calculate total', () => {
  // Arrange
  component.items = [1, 2, 3];
  
  // Act
  const total = component.calculateTotal();
  
  // Assert
  expect(total).toBe(6);
});
```

## Running Tests

### Unit Tests

```bash
# Run all unit tests
cd frontend
npm test

# Run tests in watch mode
npm test -- --watch

# Run tests with coverage
npm test -- --code-coverage
```

### E2E Tests

```bash
# Run all E2E tests
npm run test:e2e

# Run E2E tests in UI mode
npm run test:e2e:ui

# Run specific test file
npx playwright test e2e/auth.spec.ts
```

## Test Coverage

### Coverage Thresholds

Current coverage thresholds (configured in `karma.conf.js`):

- Statements: 70%
- Branches: 65%
- Functions: 70%
- Lines: 70%

### Viewing Coverage Reports

After running tests with coverage:

```bash
cd frontend
npm test -- --code-coverage
```

Coverage reports are generated in `frontend/coverage/interface-configurator/`.

## Additional Resources

- [Angular Testing Guide](https://angular.io/guide/testing)
- [Jasmine Documentation](https://jasmine.github.io/)
- [Playwright Documentation](https://playwright.dev/)
- [Karma Configuration](https://karma-runner.github.io/)

## Troubleshooting

### Common Issues

1. **Tests timing out**: Increase timeout or check for unhandled promises
2. **DOM queries failing**: Use `TestUtils` helpers instead of direct queries
3. **Async operations not completing**: Use `waitForAsyncOperations()` helper
4. **Mock data not matching**: Use `MockDataFactory` for consistency

### Getting Help

If you encounter issues:

1. Check test utilities documentation
2. Review similar test files
3. Check Angular testing best practices
4. Consult team members
