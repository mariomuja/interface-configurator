import { TrackFunction } from './track-function.decorator';
import { ErrorTrackingService } from '../services/error-tracking.service';

describe('TrackFunction Decorator', () => {
  let mockErrorTrackingService: jasmine.SpyObj<ErrorTrackingService>;
  let testClass: any;

  beforeEach(() => {
    mockErrorTrackingService = jasmine.createSpyObj('ErrorTrackingService', [
      'trackFunctionCall',
      'trackError',
    ]);

    // Create a test class with the decorator
    class TestClass {
      errorTrackingService = mockErrorTrackingService;

      @TrackFunction('TestComponent')
      syncMethod(param: string): string {
        return `result: ${param}`;
      }

      @TrackFunction('TestComponent')
      async asyncMethod(param: string): Promise<string> {
        return Promise.resolve(`async result: ${param}`);
      }

      @TrackFunction('TestComponent')
      async asyncMethodWithError(param: string): Promise<string> {
        return Promise.reject(new Error('Async error'));
      }

      @TrackFunction('TestComponent')
      syncMethodWithError(param: string): string {
        throw new Error('Sync error');
      }

      @TrackFunction()
      methodWithoutComponentName(param: string): string {
        return `result: ${param}`;
      }

      methodWithoutDecorator(param: string): string {
        return `result: ${param}`;
      }
    }

    testClass = new TestClass();
  });

  describe('synchronous methods', () => {
    it('should track successful synchronous method calls', () => {
      const result = testClass.syncMethod('test');

      expect(result).toBe('result: test');
      expect(mockErrorTrackingService.trackFunctionCall).toHaveBeenCalledWith(
        'TestClass.syncMethod',
        'TestComponent',
        ['test'],
        'result: test',
        jasmine.any(Number)
      );
    });

    it('should track errors in synchronous methods', () => {
      expect(() => testClass.syncMethodWithError('test')).toThrow('Sync error');

      expect(mockErrorTrackingService.trackError).toHaveBeenCalledWith(
        'TestClass.syncMethodWithError',
        jasmine.any(Error),
        'TestComponent',
        { arguments: ['test'] }
      );
    });

    it('should work without component name', () => {
      const result = testClass.methodWithoutComponentName('test');

      expect(result).toBe('result: test');
      expect(mockErrorTrackingService.trackFunctionCall).toHaveBeenCalledWith(
        'TestClass.methodWithoutComponentName',
        undefined,
        ['test'],
        'result: test',
        jasmine.any(Number)
      );
    });
  });

  describe('asynchronous methods', () => {
    it('should track successful async method calls', async () => {
      const result = await testClass.asyncMethod('test');

      expect(result).toBe('async result: test');
      expect(mockErrorTrackingService.trackFunctionCall).toHaveBeenCalledWith(
        'TestClass.asyncMethod',
        'TestComponent',
        ['test'],
        'async result: test',
        jasmine.any(Number)
      );
    });

    it('should track errors in async methods', async () => {
      await expectAsync(testClass.asyncMethodWithError('test')).toBeRejectedWith(
        Error,
        'Async error'
      );

      expect(mockErrorTrackingService.trackError).toHaveBeenCalledWith(
        'TestClass.asyncMethodWithError',
        jasmine.any(Error),
        'TestComponent',
        { arguments: ['test'] }
      );
    });
  });

  describe('performance tracking', () => {
    it('should measure execution time', async () => {
      await testClass.asyncMethod('test');

      const callArgs = mockErrorTrackingService.trackFunctionCall.calls.mostRecent()
        .args;
      const duration = callArgs[4];

      expect(duration).toBeGreaterThanOrEqual(0);
      expect(typeof duration).toBe('number');
    });
  });

  describe('error handling', () => {
    it('should execute method even if error tracking service is not available', () => {
      testClass.errorTrackingService = undefined;

      const result = testClass.syncMethod('test');

      expect(result).toBe('result: test');
      expect(mockErrorTrackingService.trackFunctionCall).not.toHaveBeenCalled();
    });

    it('should handle null error tracking service gracefully', () => {
      testClass.errorTrackingService = null;

      const result = testClass.syncMethod('test');

      expect(result).toBe('result: test');
    });
  });

  describe('method execution', () => {
    it('should preserve original method behavior', () => {
      const result = testClass.syncMethod('test');
      expect(result).toBe('result: test');
    });

    it('should preserve original method behavior for methods without decorator', () => {
      const result = testClass.methodWithoutDecorator('test');
      expect(result).toBe('result: test');
      expect(mockErrorTrackingService.trackFunctionCall).not.toHaveBeenCalled();
    });

    it('should handle methods with multiple parameters', () => {
      class MultiParamClass {
        errorTrackingService = mockErrorTrackingService;

        @TrackFunction('TestComponent')
        multiParamMethod(a: string, b: number, c: boolean): string {
          return `${a}-${b}-${c}`;
        }
      }

      const instance = new MultiParamClass();
      const result = instance.multiParamMethod('test', 42, true);

      expect(result).toBe('test-42-true');
      expect(mockErrorTrackingService.trackFunctionCall).toHaveBeenCalledWith(
        'MultiParamClass.multiParamMethod',
        'TestComponent',
        ['test', 42, true],
        'test-42-true',
        jasmine.any(Number)
      );
    });
  });
});
