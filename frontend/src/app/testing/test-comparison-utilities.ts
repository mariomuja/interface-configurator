/**
 * Test Comparison Utilities
 */

import { TestMetrics } from './test-metrics';

/**
 * Test comparison result
 */
export interface TestComparisonResult {
  testName: string;
  current: TestMetrics;
  previous: TestMetrics | null;
  durationChange: number;
  memoryChange: number;
  statusChange: 'improved' | 'degraded' | 'unchanged';
  significantChange: boolean;
}

/**
 * Test comparison utilities
 */
export class TestComparisonUtilities {
  /**
   * Compare test runs
   */
  static compareTestRuns(
    current: TestMetrics[],
    previous: TestMetrics[]
  ): TestComparisonResult[] {
    const previousMap = new Map(previous.map(m => [m.testName, m]));
    const results: TestComparisonResult[] = [];

    current.forEach(currentMetric => {
      const previousMetric = previousMap.get(currentMetric.testName) || null;
      const result = this.compareTestMetric(currentMetric, previousMetric);
      results.push(result);
    });

    return results;
  }

  /**
   * Compare single test metric
   */
  private static compareTestMetric(
    current: TestMetrics,
    previous: TestMetrics | null
  ): TestComparisonResult {
    if (!previous) {
      return {
        testName: current.testName,
        current,
        previous: null,
        durationChange: 0,
        memoryChange: 0,
        statusChange: 'unchanged',
        significantChange: false
      };
    }

    const durationChange = current.duration - previous.duration;
    const memoryChange = (current.memoryUsage?.peak || 0) - (previous.memoryUsage?.peak || 0);
    
    let statusChange: 'improved' | 'degraded' | 'unchanged' = 'unchanged';
    if (previous.status === 'failed' && current.status === 'passed') {
      statusChange = 'improved';
    } else if (previous.status === 'passed' && current.status === 'failed') {
      statusChange = 'degraded';
    } else if (current.duration < previous.duration * 0.9) {
      statusChange = 'improved';
    } else if (current.duration > previous.duration * 1.1) {
      statusChange = 'degraded';
    }

    const significantChange = Math.abs(durationChange) > previous.duration * 0.2 || 
                              Math.abs(memoryChange) > 10; // > 10MB change

    return {
      testName: current.testName,
      current,
      previous,
      durationChange,
      memoryChange,
      statusChange,
      significantChange
    };
  }

  /**
   * Generate comparison report
   */
  static generateComparisonReport(results: TestComparisonResult[]): string {
    const improved = results.filter(r => r.statusChange === 'improved');
    const degraded = results.filter(r => r.statusChange === 'degraded');
    const unchanged = results.filter(r => r.statusChange === 'unchanged');
    const significant = results.filter(r => r.significantChange);

    const avgDurationChange = results.reduce((sum, r) => sum + r.durationChange, 0) / results.length;
    const avgMemoryChange = results.reduce((sum, r) => sum + r.memoryChange, 0) / results.length;

    return `
Test Comparison Report:
  Total Tests: ${results.length}
  Improved: ${improved.length}
  Degraded: ${degraded.length}
  Unchanged: ${unchanged.length}
  Significant Changes: ${significant.length}

Average Changes:
  Duration: ${avgDurationChange > 0 ? '+' : ''}${avgDurationChange.toFixed(2)}ms
  Memory: ${avgMemoryChange > 0 ? '+' : ''}${avgMemoryChange.toFixed(2)}MB

Degraded Tests:
${degraded.slice(0, 10).map(r => `
  - ${r.testName}
    Duration: ${r.durationChange > 0 ? '+' : ''}${r.durationChange.toFixed(2)}ms
    Memory: ${r.memoryChange > 0 ? '+' : ''}${r.memoryChange.toFixed(2)}MB
`).join('')}

Improved Tests:
${improved.slice(0, 10).map(r => `
  - ${r.testName}
    Duration: ${r.durationChange < 0 ? '' : '+'}${r.durationChange.toFixed(2)}ms
    Memory: ${r.memoryChange < 0 ? '' : '+'}${r.memoryChange.toFixed(2)}MB
`).join('')}
`;
  }

  /**
   * Find regressions
   */
  static findRegressions(results: TestComparisonResult[]): TestComparisonResult[] {
    return results.filter(r => 
      r.statusChange === 'degraded' || 
      (r.previous && r.previous.status === 'passed' && r.current.status === 'failed')
    );
  }

  /**
   * Find improvements
   */
  static findImprovements(results: TestComparisonResult[]): TestComparisonResult[] {
    return results.filter(r => 
      r.statusChange === 'improved' || 
      (r.previous && r.previous.status === 'failed' && r.current.status === 'passed')
    );
  }
}
