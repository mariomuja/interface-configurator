/**
 * Test Retry Strategies
 */

/**
 * Retry configuration
 */
export interface RetryConfig {
  maxRetries: number;
  delay: number;
  backoffMultiplier?: number;
  retryCondition?: (error: any) => boolean;
}

/**
 * Default retry configuration
 */
export const defaultRetryConfig: RetryConfig = {
  maxRetries: 3,
  delay: 1000,
  backoffMultiplier: 2,
  retryCondition: () => true
};

/**
 * Retry strategies for tests
 */
export class TestRetryStrategies {
  /**
   * Retry a test function with exponential backoff
   */
  static async retryWithBackoff<T>(
    fn: () => Promise<T> | T,
    config: RetryConfig = defaultRetryConfig
  ): Promise<T> {
    let lastError: any;
    let delay = config.delay;

    for (let attempt = 0; attempt <= config.maxRetries; attempt++) {
      try {
        return await fn();
      } catch (error) {
        lastError = error;

        if (attempt < config.maxRetries) {
          if (config.retryCondition && !config.retryCondition(error)) {
            throw error;
          }

          await new Promise(resolve => setTimeout(resolve, delay));
          delay *= config.backoffMultiplier || 2;
        }
      }
    }

    throw lastError;
  }

  /**
   * Retry a test function with fixed delay
   */
  static async retryWithFixedDelay<T>(
    fn: () => Promise<T> | T,
    maxRetries: number = 3,
    delay: number = 1000
  ): Promise<T> {
    return this.retryWithBackoff(fn, {
      maxRetries,
      delay,
      backoffMultiplier: 1
    });
  }

  /**
   * Retry only on specific errors
   */
  static async retryOnError<T>(
    fn: () => Promise<T> | T,
    errorTypes: string[],
    config: RetryConfig = defaultRetryConfig
  ): Promise<T> {
    return this.retryWithBackoff(fn, {
      ...config,
      retryCondition: (error: any) => {
        return errorTypes.some(type => error.name === type || error.constructor.name === type);
      }
    });
  }

  /**
   * Retry only on network errors
   */
  static async retryOnNetworkError<T>(
    fn: () => Promise<T> | T,
    config: RetryConfig = defaultRetryConfig
  ): Promise<T> {
    return this.retryOnError(fn, ['NetworkError', 'TimeoutError', 'HttpErrorResponse'], config);
  }

  /**
   * Retry with custom condition
   */
  static async retryWithCondition<T>(
    fn: () => Promise<T> | T,
    condition: (error: any, attempt: number) => boolean,
    config: RetryConfig = defaultRetryConfig
  ): Promise<T> {
    let lastError: any;
    let delay = config.delay;

    for (let attempt = 0; attempt <= config.maxRetries; attempt++) {
      try {
        return await fn();
      } catch (error) {
        lastError = error;

        if (attempt < config.maxRetries && condition(error, attempt)) {
          await new Promise(resolve => setTimeout(resolve, delay));
          delay *= config.backoffMultiplier || 2;
        } else {
          throw error;
        }
      }
    }

    throw lastError;
  }

  /**
   * Create Jasmine retry helper
   */
  static createJasmineRetryHelper(config: RetryConfig = defaultRetryConfig) {
    return (testFn: () => void | Promise<void>) => {
      return async () => {
        await this.retryWithBackoff(async () => {
          await testFn();
        }, config);
      };
    };
  }
}
