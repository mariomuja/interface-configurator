/**
 * Test Metrics and Analytics
 */

/**
 * Test execution metrics
 */
export interface TestMetrics {
  testName: string;
  duration: number;
  status: 'passed' | 'failed' | 'skipped';
  error?: string;
  retries?: number;
  memoryUsage?: {
    before: number;
    after: number;
    peak: number;
  };
  assertions: number;
  apiCalls?: number;
  domQueries?: number;
}

/**
 * Test suite metrics
 */
export interface SuiteMetrics {
  suiteName: string;
  totalTests: number;
  passedTests: number;
  failedTests: number;
  skippedTests: number;
  totalDuration: number;
  averageDuration: number;
  slowestTest: TestMetrics | null;
  fastestTest: TestMetrics | null;
  tests: TestMetrics[];
}

/**
 * Test metrics collector
 */
export class TestMetricsCollector {
  private static metrics: TestMetrics[] = [];
  private static currentTest: Partial<TestMetrics> | null = null;
  private static startTime: number = 0;
  private static memoryBefore: number = 0;
  private static peakMemory: number = 0;

  /**
   * Start tracking a test
   */
  static startTest(testName: string): void {
    this.currentTest = {
      testName,
      status: 'passed',
      assertions: 0,
      apiCalls: 0,
      domQueries: 0
    };
    this.startTime = Date.now();
    this.memoryBefore = this.getMemoryUsage();
    this.peakMemory = this.memoryBefore;
  }

  /**
   * End tracking a test
   */
  static endTest(status: 'passed' | 'failed' | 'skipped', error?: string): void {
    if (!this.currentTest) return;

    const duration = Date.now() - this.startTime;
    const memoryAfter = this.getMemoryUsage();

    const metrics: TestMetrics = {
      testName: this.currentTest.testName!,
      duration,
      status,
      error,
      retries: this.currentTest.retries,
      memoryUsage: {
        before: this.memoryBefore,
        after: memoryAfter,
        peak: this.peakMemory
      },
      assertions: this.currentTest.assertions || 0,
      apiCalls: this.currentTest.apiCalls,
      domQueries: this.currentTest.domQueries
    };

    this.metrics.push(metrics);
    this.currentTest = null;
  }

  /**
   * Increment assertion count
   */
  static incrementAssertions(): void {
    if (this.currentTest) {
      this.currentTest.assertions = (this.currentTest.assertions || 0) + 1;
    }
  }

  /**
   * Increment API call count
   */
  static incrementApiCalls(): void {
    if (this.currentTest) {
      this.currentTest.apiCalls = (this.currentTest.apiCalls || 0) + 1;
    }
  }

  /**
   * Increment DOM query count
   */
  static incrementDomQueries(): void {
    if (this.currentTest) {
      this.currentTest.domQueries = (this.currentTest.domQueries || 0) + 1;
    }
  }

  /**
   * Update peak memory
   */
  static updateMemory(): void {
    const current = this.getMemoryUsage();
    if (current > this.peakMemory) {
      this.peakMemory = current;
    }
  }

  /**
   * Get memory usage
   */
  private static getMemoryUsage(): number {
    if ('memory' in performance) {
      return (performance as any).memory.usedJSHeapSize / (1024 * 1024); // MB
    }
    return 0;
  }

  /**
   * Get all metrics
   */
  static getMetrics(): TestMetrics[] {
    return [...this.metrics];
  }

  /**
   * Get suite metrics
   */
  static getSuiteMetrics(suiteName: string): SuiteMetrics {
    const suiteTests = this.metrics.filter(m => m.testName.includes(suiteName));
    const passedTests = suiteTests.filter(t => t.status === 'passed');
    const failedTests = suiteTests.filter(t => t.status === 'failed');
    const skippedTests = suiteTests.filter(t => t.status === 'skipped');
    const totalDuration = suiteTests.reduce((sum, t) => sum + t.duration, 0);
    const averageDuration = suiteTests.length > 0 ? totalDuration / suiteTests.length : 0;

    const sortedByDuration = [...suiteTests].sort((a, b) => b.duration - a.duration);

    return {
      suiteName,
      totalTests: suiteTests.length,
      passedTests: passedTests.length,
      failedTests: failedTests.length,
      skippedTests: skippedTests.length,
      totalDuration,
      averageDuration,
      slowestTest: sortedByDuration[0] || null,
      fastestTest: sortedByDuration[sortedByDuration.length - 1] || null,
      tests: suiteTests
    };
  }

  /**
   * Generate metrics report
   */
  static generateReport(): string {
    const totalTests = this.metrics.length;
    const passedTests = this.metrics.filter(t => t.status === 'passed').length;
    const failedTests = this.metrics.filter(t => t.status === 'failed').length;
    const skippedTests = this.metrics.filter(t => t.status === 'skipped').length;
    const totalDuration = this.metrics.reduce((sum, t) => sum + t.duration, 0);
    const averageDuration = totalTests > 0 ? totalDuration / totalTests : 0;

    const slowestTests = [...this.metrics]
      .sort((a, b) => b.duration - a.duration)
      .slice(0, 5);

    return `
Test Metrics Report:
  Total Tests: ${totalTests}
  Passed: ${passedTests} (${((passedTests / totalTests) * 100).toFixed(1)}%)
  Failed: ${failedTests} (${((failedTests / totalTests) * 100).toFixed(1)}%)
  Skipped: ${skippedTests} (${((skippedTests / totalTests) * 100).toFixed(1)}%)
  Total Duration: ${totalDuration}ms
  Average Duration: ${averageDuration.toFixed(2)}ms

Slowest Tests:
${slowestTests.map((t, i) => `  ${i + 1}. ${t.testName}: ${t.duration}ms`).join('\n')}
`;
  }

  /**
   * Clear all metrics
   */
  static clear(): void {
    this.metrics = [];
    this.currentTest = null;
  }
}
