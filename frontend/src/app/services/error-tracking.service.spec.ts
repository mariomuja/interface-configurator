import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ErrorTrackingService, FunctionCall, ErrorReport } from './error-tracking.service';

describe('ErrorTrackingService', () => {
  let service: ErrorTrackingService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [ErrorTrackingService]
    });
    service = TestBed.inject(ErrorTrackingService);
    // Clear history before each test
    service.clearHistory();
    service.clearErrorReport();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should track successful function call', () => {
    service.trackFunctionCall('testFunction', 'TestComponent', { param: 'value' }, 'result', 100);
    
    const report = service.getCurrentErrorReport();
    // No error report should exist for successful calls
    expect(report).toBeNull();
  });

  it('should track error and create error report', () => {
    const error = new Error('Test error');
    const report = service.trackError('testFunction', error, 'TestComponent', { context: 'test' });

    expect(report).toBeTruthy();
    expect(report.errorId).toBeTruthy();
    expect(report.currentError.functionName).toBe('testFunction');
    expect(report.currentError.component).toBe('TestComponent');
    expect(report.currentError.error.message).toBe('Test error');
    expect(report.functionCallHistory.length).toBeGreaterThan(0);
  });

  it('should limit function call history to MAX_HISTORY_SIZE', () => {
    // Track more than MAX_HISTORY_SIZE calls
    for (let i = 0; i < 150; i++) {
      service.trackFunctionCall(`function${i}`, 'Component', {}, {}, 10);
    }

    const error = new Error('Test');
    const report = service.trackError('testFunction', error, 'TestComponent');
    
    // Should only keep last 100 calls
    expect(report.functionCallHistory.length).toBeLessThanOrEqual(100);
  });

  it('should add application state', () => {
    service.addApplicationState('testKey', 'testValue');
    
    const error = new Error('Test');
    const report = service.trackError('testFunction', error, 'TestComponent');
    
    expect(report.applicationState['testKey']).toBe('testValue');
  });

  it('should sanitize large objects in logging', () => {
    const largeObject = { data: 'x'.repeat(100000) };
    service.trackFunctionCall('testFunction', 'Component', largeObject, {}, 10);
    
    // Should not throw error
    expect(true).toBeTruthy();
  });

  it('should clear error report', () => {
    const error = new Error('Test');
    service.trackError('testFunction', error, 'TestComponent');
    
    expect(service.getCurrentErrorReport()).toBeTruthy();
    
    service.clearErrorReport();
    
    expect(service.getCurrentErrorReport()).toBeNull();
  });

  it('should clear history', () => {
    service.trackFunctionCall('testFunction', 'Component', {}, {}, 10);
    service.clearHistory();
    
    const error = new Error('Test');
    const report = service.trackError('testFunction', error, 'TestComponent');
    
    expect(report.functionCallHistory.length).toBe(1); // Only the error call
  });
});
