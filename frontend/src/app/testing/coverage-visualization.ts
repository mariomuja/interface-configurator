/**
 * Test Coverage Visualization Utilities
 */

/**
 * Coverage data structure
 */
export interface CoverageData {
  statements: number;
  branches: number;
  functions: number;
  lines: number;
  files: Array<{
    path: string;
    statements: number;
    branches: number;
    functions: number;
    lines: number;
  }>;
}

/**
 * Coverage visualization utilities
 */
export class CoverageVisualization {
  /**
   * Generate coverage badge HTML
   */
  static generateBadge(coverage: number, threshold: number = 70): string {
    const color = coverage >= threshold ? 'green' : coverage >= threshold * 0.8 ? 'yellow' : 'red';
    return `
      <svg xmlns="http://www.w3.org/2000/svg" width="120" height="20">
        <rect width="120" height="20" fill="#555"/>
        <rect x="60" width="60" height="20" fill="#${color === 'green' ? '4c1' : color === 'yellow' ? 'dfb317' : 'e05d44'}"/>
        <text x="5" y="14" fill="#fff" font-size="11" font-family="DejaVu Sans,Verdana,Geneva,sans-serif">coverage</text>
        <text x="65" y="14" fill="#fff" font-size="11" font-family="DejaVu Sans,Verdana,Geneva,sans-serif">${coverage.toFixed(1)}%</text>
      </svg>
    `;
  }

  /**
   * Generate coverage report HTML
   */
  static generateReport(coverage: CoverageData, thresholds: {
    statements: number;
    branches: number;
    functions: number;
    lines: number;
  }): string {
    const filesHtml = coverage.files.map(file => `
      <tr>
        <td>${file.path}</td>
        <td class="${file.statements >= thresholds.statements ? 'pass' : 'fail'}">${file.statements.toFixed(1)}%</td>
        <td class="${file.branches >= thresholds.branches ? 'pass' : 'fail'}">${file.branches.toFixed(1)}%</td>
        <td class="${file.functions >= thresholds.functions ? 'pass' : 'fail'}">${file.functions.toFixed(1)}%</td>
        <td class="${file.lines >= thresholds.lines ? 'pass' : 'fail'}">${file.lines.toFixed(1)}%</td>
      </tr>
    `).join('');

    return `
      <!DOCTYPE html>
      <html>
      <head>
        <title>Test Coverage Report</title>
        <style>
          body { font-family: Arial, sans-serif; margin: 20px; }
          table { border-collapse: collapse; width: 100%; }
          th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
          th { background-color: #4CAF50; color: white; }
          .pass { background-color: #d4edda; }
          .fail { background-color: #f8d7da; }
          .summary { margin: 20px 0; padding: 15px; background-color: #f0f0f0; }
        </style>
      </head>
      <body>
        <h1>Test Coverage Report</h1>
        <div class="summary">
          <h2>Summary</h2>
          <p>Statements: ${coverage.statements.toFixed(1)}% (Threshold: ${thresholds.statements}%)</p>
          <p>Branches: ${coverage.branches.toFixed(1)}% (Threshold: ${thresholds.branches}%)</p>
          <p>Functions: ${coverage.functions.toFixed(1)}% (Threshold: ${thresholds.functions}%)</p>
          <p>Lines: ${coverage.lines.toFixed(1)}% (Threshold: ${thresholds.lines}%)</p>
        </div>
        <table>
          <thead>
            <tr>
              <th>File</th>
              <th>Statements</th>
              <th>Branches</th>
              <th>Functions</th>
              <th>Lines</th>
            </tr>
          </thead>
          <tbody>
            ${filesHtml}
          </tbody>
        </table>
      </body>
      </html>
    `;
  }

  /**
   * Check if coverage meets thresholds
   */
  static checkThresholds(
    coverage: CoverageData,
    thresholds: {
      statements: number;
      branches: number;
      functions: number;
      lines: number;
    }
  ): { passed: boolean; failures: string[] } {
    const failures: string[] = [];

    if (coverage.statements < thresholds.statements) {
      failures.push(`Statements coverage ${coverage.statements.toFixed(1)}% is below threshold ${thresholds.statements}%`);
    }
    if (coverage.branches < thresholds.branches) {
      failures.push(`Branches coverage ${coverage.branches.toFixed(1)}% is below threshold ${thresholds.branches}%`);
    }
    if (coverage.functions < thresholds.functions) {
      failures.push(`Functions coverage ${coverage.functions.toFixed(1)}% is below threshold ${thresholds.functions}%`);
    }
    if (coverage.lines < thresholds.lines) {
      failures.push(`Lines coverage ${coverage.lines.toFixed(1)}% is below threshold ${thresholds.lines}%`);
    }

    return {
      passed: failures.length === 0,
      failures
    };
  }

  /**
   * Generate coverage trend data
   */
  static generateTrend(
    current: CoverageData,
    previous: CoverageData | null
  ): {
    statements: { current: number; previous: number | null; change: number };
    branches: { current: number; previous: number | null; change: number };
    functions: { current: number; previous: number | null; change: number };
    lines: { current: number; previous: number | null; change: number };
  } {
    const calculateChange = (current: number, previous: number | null): number => {
      if (previous === null) return 0;
      return current - previous;
    };

    return {
      statements: {
        current: current.statements,
        previous: previous?.statements || null,
        change: calculateChange(current.statements, previous?.statements || null)
      },
      branches: {
        current: current.branches,
        previous: previous?.branches || null,
        change: calculateChange(current.branches, previous?.branches || null)
      },
      functions: {
        current: current.functions,
        previous: previous?.functions || null,
        change: calculateChange(current.functions, previous?.functions || null)
      },
      lines: {
        current: current.lines,
        previous: previous?.lines || null,
        change: calculateChange(current.lines, previous?.lines || null)
      }
    };
  }
}
