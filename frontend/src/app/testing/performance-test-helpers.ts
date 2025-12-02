/**
 * Performance testing utilities
 */

/**
 * Measure execution time of a function
 */
export async function measureExecutionTime<T>(
  fn: () => T | Promise<T>
): Promise<{ result: T; duration: number }> {
  const start = performance.now();
  const result = await fn();
  const duration = performance.now() - start;
  return { result, duration };
}

/**
 * Run performance test multiple times and get statistics
 */
export async function runPerformanceTest(
  fn: () => void | Promise<void>,
  iterations: number = 10
): Promise<{
  min: number;
  max: number;
  avg: number;
  median: number;
  durations: number[];
}> {
  const durations: number[] = [];

  for (let i = 0; i < iterations; i++) {
    const { duration } = await measureExecutionTime(fn);
    durations.push(duration);
  }

  durations.sort((a, b) => a - b);

  const min = durations[0];
  const max = durations[durations.length - 1];
  const avg = durations.reduce((sum, d) => sum + d, 0) / durations.length;
  const median =
    durations.length % 2 === 0
      ? (durations[durations.length / 2 - 1] + durations[durations.length / 2]) / 2
      : durations[Math.floor(durations.length / 2)];

  return { min, max, avg, median, durations };
}

/**
 * Assert that a function executes within a time limit
 */
export async function assertPerformanceThreshold(
  fn: () => void | Promise<void>,
  maxDuration: number,
  message?: string
): Promise<void> {
  const { duration } = await measureExecutionTime(fn);
  expect(duration).toBeLessThan(
    maxDuration,
    message || `Expected execution time to be less than ${maxDuration}ms, but was ${duration}ms`
  );
}

/**
 * Monitor memory usage
 */
export function getMemoryUsage(): {
  usedJSHeapSize: number;
  totalJSHeapSize: number;
  jsHeapSizeLimit: number;
} | null {
  if ('memory' in performance) {
    const memory = (performance as any).memory;
    return {
      usedJSHeapSize: memory.usedJSHeapSize,
      totalJSHeapSize: memory.totalJSHeapSize,
      jsHeapSizeLimit: memory.jsHeapSizeLimit,
    };
  }
  return null;
}

/**
 * Assert memory usage is within threshold
 */
export function assertMemoryUsage(
  initialUsage: { usedJSHeapSize: number } | null,
  maxIncreaseMB: number = 10
): void {
  const currentUsage = getMemoryUsage();
  if (initialUsage && currentUsage) {
    const increaseMB =
      (currentUsage.usedJSHeapSize - initialUsage.usedJSHeapSize) / (1024 * 1024);
    expect(increaseMB).toBeLessThan(
      maxIncreaseMB,
      `Memory usage increased by ${increaseMB.toFixed(2)}MB, exceeding threshold of ${maxIncreaseMB}MB`
    );
  }
}

/**
 * Wait for garbage collection (approximate)
 */
export async function waitForGC(): Promise<void> {
  if ('gc' in global) {
    (global as any).gc();
  }
  // Wait a bit for GC to complete
  await new Promise(resolve => setTimeout(resolve, 100));
}
