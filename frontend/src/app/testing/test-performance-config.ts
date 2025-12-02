/**
 * Test Performance Configuration
 */

/**
 * Performance thresholds for tests
 */
export const PerformanceThresholds = {
  componentInit: 100, // ms
  serviceCall: 50, // ms
  httpRequest: 200, // ms
  render: 50, // ms
  userInteraction: 100, // ms
  memoryIncrease: 10 // MB
};

/**
 * Test timeout configurations
 */
export const TestTimeouts = {
  short: 1000,
  medium: 3000,
  long: 5000,
  veryLong: 10000
};

/**
 * Batch size for performance tests
 */
export const PerformanceBatchSizes = {
  small: 10,
  medium: 100,
  large: 1000,
  veryLarge: 10000
};

/**
 * Performance test configuration
 */
export interface PerformanceTestConfig {
  iterations: number;
  warmupIterations: number;
  timeout: number;
  threshold: number;
}

/**
 * Default performance test configuration
 */
export const defaultPerformanceConfig: PerformanceTestConfig = {
  iterations: 10,
  warmupIterations: 2,
  timeout: 5000,
  threshold: 100
};
