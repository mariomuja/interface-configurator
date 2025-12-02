/**
 * Test Reporting Helpers
 */

/**
 * Test execution statistics
 */
export interface TestStats {
  total: number;
  passed: number;
  failed: number;
  skipped: number;
  duration: number;
  coverage?: {
    statements: number;
    branches: number;
    functions: number;
    lines: number;
  };
}

/**
 * Test reporting utilities
 */
export class TestReportingHelpers {
  /**
   * Collect test statistics
   */
  static collectStats(): TestStats {
    const jasmineEnv = (window as any).jasmine?.getEnv();
    if (!jasmineEnv) {
      return {
        total: 0,
        passed: 0,
        failed: 0,
        skipped: 0,
        duration: 0
      };
    }

    const specs = jasmineEnv.specs;
    let passed = 0;
    let failed = 0;
    let skipped = 0;

    specs.forEach((spec: any) => {
      const result = spec.result;
      if (result.status === 'passed') {
        passed++;
      } else if (result.status === 'failed') {
        failed++;
      } else if (result.status === 'pending' || result.status === 'disabled') {
        skipped++;
      }
    });

    return {
      total: specs.length,
      passed,
      failed,
      skipped,
      duration: Date.now() // Would need actual start time
    };
  }

  /**
   * Format test results for console
   */
  static formatResults(stats: TestStats): string {
    return `
Test Results:
  Total: ${stats.total}
  Passed: ${stats.passed} (${((stats.passed / stats.total) * 100).toFixed(1)}%)
  Failed: ${stats.failed} (${((stats.failed / stats.total) * 100).toFixed(1)}%)
  Skipped: ${stats.skipped} (${((stats.skipped / stats.total) * 100).toFixed(1)}%)
  Duration: ${stats.duration}ms
`;
  }

  /**
   * Generate test report summary
   */
  static generateSummary(): string {
    const stats = this.collectStats();
    return this.formatResults(stats);
  }

  /**
   * Log slow tests
   */
  static logSlowTests(threshold: number = 1000): void {
    const jasmineEnv = (window as any).jasmine?.getEnv();
    if (!jasmineEnv) return;

    const specs = jasmineEnv.specs;
    const slowTests: Array<{ name: string; duration: number }> = [];

    specs.forEach((spec: any) => {
      const duration = spec.result?.duration || 0;
      if (duration > threshold) {
        slowTests.push({
          name: spec.fullName,
          duration
        });
      }
    });

    if (slowTests.length > 0) {
      console.warn(`Slow tests (>${threshold}ms):`);
      slowTests
        .sort((a, b) => b.duration - a.duration)
        .forEach(test => {
          console.warn(`  ${test.name}: ${test.duration}ms`);
        });
    }
  }

  /**
   * Generate coverage report summary
   */
  static generateCoverageSummary(coverage: TestStats['coverage']): string {
    if (!coverage) {
      return 'Coverage data not available';
    }

    return `
Coverage Summary:
  Statements: ${coverage.statements.toFixed(1)}%
  Branches: ${coverage.branches.toFixed(1)}%
  Functions: ${coverage.functions.toFixed(1)}%
  Lines: ${coverage.lines.toFixed(1)}%
`;
  }
}
