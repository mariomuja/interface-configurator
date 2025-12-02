/**
 * Test Flakiness Detection
 */

import { TestMetrics } from './test-metrics';

/**
 * Flakiness detection result
 */
export interface FlakinessResult {
  testName: string;
  flakinessScore: number; // 0-100, higher = more flaky
  failureRate: number;
  inconsistentDuration: boolean;
  inconsistentMemory: boolean;
  recommendations: string[];
}

/**
 * Test flakiness detector
 */
export class TestFlakinessDetector {
  /**
   * Analyze test metrics for flakiness
   */
  static analyzeFlakiness(metrics: TestMetrics[]): FlakinessResult[] {
    const testGroups = this.groupByTestName(metrics);
    const results: FlakinessResult[] = [];

    Object.keys(testGroups).forEach(testName => {
      const testRuns = testGroups[testName];
      const result = this.analyzeTestFlakiness(testName, testRuns);
      results.push(result);
    });

    return results.sort((a, b) => b.flakinessScore - a.flakinessScore);
  }

  /**
   * Group metrics by test name
   */
  private static groupByTestName(metrics: TestMetrics[]): Record<string, TestMetrics[]> {
    const groups: Record<string, TestMetrics[]> = {};

    metrics.forEach(metric => {
      if (!groups[metric.testName]) {
        groups[metric.testName] = [];
      }
      groups[metric.testName].push(metric);
    });

    return groups;
  }

  /**
   * Analyze flakiness for a single test
   */
  private static analyzeTestFlakiness(testName: string, runs: TestMetrics[]): FlakinessResult {
    const totalRuns = runs.length;
    const failedRuns = runs.filter(r => r.status === 'failed').length;
    const failureRate = totalRuns > 0 ? failedRuns / totalRuns : 0;

    // Check duration consistency
    const durations = runs.map(r => r.duration);
    const avgDuration = durations.reduce((sum, d) => sum + d, 0) / durations.length;
    const durationVariance = durations.reduce((sum, d) => sum + Math.pow(d - avgDuration, 2), 0) / durations.length;
    const durationStdDev = Math.sqrt(durationVariance);
    const inconsistentDuration = durationStdDev / avgDuration > 0.5; // More than 50% variance

    // Check memory consistency
    const memoryUsages = runs
      .filter(r => r.memoryUsage)
      .map(r => r.memoryUsage!.peak);
    const avgMemory = memoryUsages.reduce((sum, m) => sum + m, 0) / memoryUsages.length;
    const memoryVariance = memoryUsages.reduce((sum, m) => sum + Math.pow(m - avgMemory, 2), 0) / memoryUsages.length;
    const memoryStdDev = Math.sqrt(memoryVariance);
    const inconsistentMemory = memoryUsages.length > 0 && memoryStdDev / avgMemory > 0.3; // More than 30% variance

    // Calculate flakiness score
    let flakinessScore = failureRate * 50; // 0-50 points for failures
    if (inconsistentDuration) flakinessScore += 25;
    if (inconsistentMemory) flakinessScore += 25;

    // Generate recommendations
    const recommendations: string[] = [];
    if (failureRate > 0.3) {
      recommendations.push('High failure rate detected. Consider adding retry logic or fixing underlying issues.');
    }
    if (inconsistentDuration) {
      recommendations.push('Inconsistent execution time detected. Check for race conditions or async issues.');
    }
    if (inconsistentMemory) {
      recommendations.push('Memory usage varies significantly. Check for memory leaks or resource cleanup.');
    }
    if (runs.some(r => r.retries && r.retries > 0)) {
      recommendations.push('Test requires retries. Investigate root cause of flakiness.');
    }

    return {
      testName,
      flakinessScore: Math.min(100, flakinessScore),
      failureRate,
      inconsistentDuration,
      inconsistentMemory,
      recommendations
    };
  }

  /**
   * Generate flakiness report
   */
  static generateFlakinessReport(results: FlakinessResult[]): string {
    const flakyTests = results.filter(r => r.flakinessScore > 30);
    const veryFlakyTests = results.filter(r => r.flakinessScore > 70);

    return `
Test Flakiness Report:
  Total Tests Analyzed: ${results.length}
  Flaky Tests (>30%): ${flakyTests.length}
  Very Flaky Tests (>70%): ${veryFlakyTests.length}

Top Flaky Tests:
${results.slice(0, 10).map((r, i) => `
  ${i + 1}. ${r.testName}
     Flakiness Score: ${r.flakinessScore.toFixed(1)}%
     Failure Rate: ${(r.failureRate * 100).toFixed(1)}%
     Inconsistent Duration: ${r.inconsistentDuration ? 'Yes' : 'No'}
     Inconsistent Memory: ${r.inconsistentMemory ? 'Yes' : 'No'}
     Recommendations:
${r.recommendations.map(rec => `       - ${rec}`).join('\n')}
`).join('')}
`;
  }

  /**
   * Detect flaky patterns
   */
  static detectFlakyPatterns(metrics: TestMetrics[]): {
    pattern: string;
    description: string;
    affectedTests: string[];
  }[] {
    const patterns: Array<{
      pattern: string;
      description: string;
      affectedTests: string[];
    }> = [];

    // Pattern: Tests that fail intermittently
    const intermittentFailures = metrics
      .filter(m => m.status === 'failed')
      .map(m => m.testName);
    if (intermittentFailures.length > 0) {
      patterns.push({
        pattern: 'intermittent-failures',
        description: 'Tests that fail intermittently',
        affectedTests: [...new Set(intermittentFailures)]
      });
    }

    // Pattern: Tests with high memory usage
    const highMemoryTests = metrics
      .filter(m => m.memoryUsage && m.memoryUsage.peak > 100) // > 100MB
      .map(m => m.testName);
    if (highMemoryTests.length > 0) {
      patterns.push({
        pattern: 'high-memory-usage',
        description: 'Tests with high memory usage (>100MB)',
        affectedTests: [...new Set(highMemoryTests)]
      });
    }

    // Pattern: Tests with long duration
    const slowTests = metrics
      .filter(m => m.duration > 5000) // > 5 seconds
      .map(m => m.testName);
    if (slowTests.length > 0) {
      patterns.push({
        pattern: 'slow-tests',
        description: 'Tests with long execution time (>5s)',
        affectedTests: [...new Set(slowTests)]
      });
    }

    return patterns;
  }
}
