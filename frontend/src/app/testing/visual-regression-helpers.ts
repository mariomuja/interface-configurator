/**
 * Visual Regression Testing Helpers
 */

import { ComponentFixture } from '@angular/core/testing';
import { Page } from '@playwright/test';

/**
 * Visual regression testing utilities
 */
export class VisualRegressionHelpers {
  /**
   * Take component screenshot
   */
  static async takeComponentScreenshot(
    fixture: ComponentFixture<any>,
    name: string
  ): Promise<string> {
    const element = fixture.nativeElement;
    // In browser environment, would use html2canvas or similar
    // For now, return HTML representation
    return element.innerHTML;
  }

  /**
   * Compare component screenshots
   */
  static compareScreenshots(
    current: string,
    baseline: string,
    threshold: number = 0.1
  ): { match: boolean; difference: number; message: string } {
    // Simple string comparison for now
    // In production, would use image comparison library
    const similarity = this.calculateSimilarity(current, baseline);
    const match = similarity >= 1 - threshold;

    return {
      match,
      difference: 1 - similarity,
      message: match
        ? 'Screenshots match'
        : `Screenshots differ by ${((1 - similarity) * 100).toFixed(2)}%`
    };
  }

  /**
   * Calculate similarity between two strings
   */
  private static calculateSimilarity(str1: string, str2: string): number {
    if (str1 === str2) return 1;
    if (str1.length === 0 || str2.length === 0) return 0;

    const longer = str1.length > str2.length ? str1 : str2;
    const shorter = str1.length > str2.length ? str2 : str1;
    const editDistance = this.levenshteinDistance(str1, str2);

    return (longer.length - editDistance) / longer.length;
  }

  /**
   * Calculate Levenshtein distance
   */
  private static levenshteinDistance(str1: string, str2: string): number {
    const matrix: number[][] = [];

    for (let i = 0; i <= str2.length; i++) {
      matrix[i] = [i];
    }

    for (let j = 0; j <= str1.length; j++) {
      matrix[0][j] = j;
    }

    for (let i = 1; i <= str2.length; i++) {
      for (let j = 1; j <= str1.length; j++) {
        if (str2.charAt(i - 1) === str1.charAt(j - 1)) {
          matrix[i][j] = matrix[i - 1][j - 1];
        } else {
          matrix[i][j] = Math.min(
            matrix[i - 1][j - 1] + 1,
            matrix[i][j - 1] + 1,
            matrix[i - 1][j] + 1
          );
        }
      }
    }

    return matrix[str2.length][str1.length];
  }

  /**
   * Take E2E page screenshot
   */
  static async takePageScreenshot(
    page: Page,
    name: string,
    options?: { fullPage?: boolean; clip?: { x: number; y: number; width: number; height: number } }
  ): Promise<Buffer> {
    return await page.screenshot({
      path: `test-results/screenshots/${name}.png`,
      fullPage: options?.fullPage ?? false,
      clip: options?.clip
    });
  }

  /**
   * Compare E2E screenshots using Playwright
   */
  static async comparePageScreenshots(
    page: Page,
    name: string,
    threshold: number = 0.2
  ): Promise<{ match: boolean; message: string }> {
    try {
      await expect(page).toHaveScreenshot(`${name}.png`, {
        threshold,
        maxDiffPixels: 100
      });
      return { match: true, message: 'Screenshots match' };
    } catch (error: any) {
      return {
        match: false,
        message: error.message || 'Screenshots differ'
      };
    }
  }

  /**
   * Generate visual diff report
   */
  static generateVisualDiffReport(
    baseline: string,
    current: string,
    diff: string
  ): string {
    return `
Visual Regression Test Report:
  Baseline: ${baseline}
  Current: ${current}
  Diff: ${diff}
  Status: ${diff ? 'FAILED' : 'PASSED'}
`;
  }
}

// Import expect for Playwright
import { expect } from '@playwright/test';
