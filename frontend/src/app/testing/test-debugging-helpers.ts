/**
 * Test Debugging Helpers
 */

import { ComponentFixture } from '@angular/core/testing';

/**
 * Test debugging utilities
 */
export class TestDebuggingHelpers {
  /**
   * Log component state for debugging
   */
  static logComponentState(component: any, label: string = 'Component State'): void {
    console.group(label);
    console.log('Component:', component.constructor.name);
    Object.keys(component).forEach(key => {
      if (!key.startsWith('_') && typeof component[key] !== 'function') {
        try {
          console.log(`${key}:`, component[key]);
        } catch (e) {
          console.log(`${key}: [Error reading property]`);
        }
      }
    });
    console.groupEnd();
  }

  /**
   * Log fixture HTML for debugging
   */
  static logFixtureHtml(fixture: ComponentFixture<any>, label: string = 'Fixture HTML'): void {
    console.group(label);
    console.log(fixture.nativeElement.innerHTML);
    console.groupEnd();
  }

  /**
   * Log DOM structure for debugging
   */
  static logDOMStructure(element: HTMLElement, maxDepth: number = 3, currentDepth: number = 0): void {
    if (currentDepth > maxDepth) return;

    const indent = '  '.repeat(currentDepth);
    console.log(`${indent}<${element.tagName.toLowerCase()}${element.className ? ` class="${element.className}"` : ''}>`);

    Array.from(element.children).forEach(child => {
      this.logDOMStructure(child as HTMLElement, maxDepth, currentDepth + 1);
    });

    if (currentDepth === maxDepth) {
      console.log(`${indent}  ...`);
    }
  }

  /**
   * Create test debug context
   */
  static createDebugContext(testName: string): {
    log: (message: string, data?: any) => void;
    logState: (component: any) => void;
    logHtml: (fixture: ComponentFixture<any>) => void;
    end: () => void;
  } {
    const startTime = Date.now();
    console.group(`🔍 Debug: ${testName}`);

    return {
      log: (message: string, data?: any) => {
        console.log(`[${Date.now() - startTime}ms] ${message}`, data || '');
      },
      logState: (component: any) => {
        this.logComponentState(component, 'Component State');
      },
      logHtml: (fixture: ComponentFixture<any>) => {
        this.logFixtureHtml(fixture, 'Fixture HTML');
      },
      end: () => {
        console.log(`Total time: ${Date.now() - startTime}ms`);
        console.groupEnd();
      }
    };
  }

  /**
   * Wait and log state changes
   */
  static async waitAndLogState(
    component: any,
    waitTime: number = 100,
    iterations: number = 5
  ): Promise<void> {
    for (let i = 0; i < iterations; i++) {
      await new Promise(resolve => setTimeout(resolve, waitTime));
      console.log(`State check ${i + 1}:`, {
        timestamp: Date.now(),
        state: this.serializeComponentState(component)
      });
    }
  }

  /**
   * Serialize component state for logging
   */
  private static serializeComponentState(component: any): Record<string, any> {
    const state: Record<string, any> = {};
    Object.keys(component).forEach(key => {
      if (!key.startsWith('_') && typeof component[key] !== 'function') {
        try {
          state[key] = JSON.parse(JSON.stringify(component[key]));
        } catch (e) {
          state[key] = String(component[key]);
        }
      }
    });
    return state;
  }

  /**
   * Create test failure report
   */
  static createFailureReport(
    testName: string,
    error: Error,
    component?: any,
    fixture?: ComponentFixture<any>
  ): string {
    const report = [
      `Test Failure Report: ${testName}`,
      `Error: ${error.message}`,
      `Stack: ${error.stack}`,
      ''
    ];

    if (component) {
      report.push('Component State:');
      report.push(JSON.stringify(this.serializeComponentState(component), null, 2));
      report.push('');
    }

    if (fixture) {
      report.push('Fixture HTML:');
      report.push(fixture.nativeElement.innerHTML);
      report.push('');
    }

    return report.join('\n');
  }

  /**
   * Enable verbose test logging
   */
  static enableVerboseLogging(): void {
    (window as any).__TEST_VERBOSE_LOGGING__ = true;
  }

  /**
   * Disable verbose test logging
   */
  static disableVerboseLogging(): void {
    (window as any).__TEST_VERBOSE_LOGGING__ = false;
  }

  /**
   * Check if verbose logging is enabled
   */
  static isVerboseLoggingEnabled(): boolean {
    return !!(window as any).__TEST_VERBOSE_LOGGING__;
  }
}
