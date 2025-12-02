/**
 * Test utilities and helpers for Angular testing
 */

import { ComponentFixture } from '@angular/core/testing';
import { DebugElement } from '@angular/core';
import { By } from '@angular/platform-browser';

/**
 * Query helper functions for easier DOM queries in tests
 */
export class TestUtils {
  /**
   * Get a single element by CSS selector
   */
  static query<T extends HTMLElement>(
    fixture: ComponentFixture<any>,
    selector: string
  ): T | null {
    const element = fixture.debugElement.query(By.css(selector));
    return element ? (element.nativeElement as T) : null;
  }

  /**
   * Get all elements matching CSS selector
   */
  static queryAll<T extends HTMLElement>(
    fixture: ComponentFixture<any>,
    selector: string
  ): T[] {
    return fixture.debugElement
      .queryAll(By.css(selector))
      .map(el => el.nativeElement as T);
  }

  /**
   * Get element by test ID attribute
   */
  static queryByTestId<T extends HTMLElement>(
    fixture: ComponentFixture<any>,
    testId: string
  ): T | null {
    return this.query<T>(fixture, `[data-testid="${testId}"]`);
  }

  /**
   * Get all elements by test ID attribute
   */
  static queryAllByTestId<T extends HTMLElement>(
    fixture: ComponentFixture<any>,
    testId: string
  ): T[] {
    return this.queryAll<T>(fixture, `[data-testid="${testId}"]`);
  }

  /**
   * Get element by text content (case-insensitive)
   */
  static queryByText(
    fixture: ComponentFixture<any>,
    text: string
  ): DebugElement | null {
    const elements = fixture.debugElement.queryAll(By.css('*'));
    return (
      elements.find(el => {
        const content = el.nativeElement.textContent?.toLowerCase() || '';
        return content.includes(text.toLowerCase());
      }) || null
    );
  }

  /**
   * Trigger click event on element
   */
  static click(fixture: ComponentFixture<any>, selector: string): void {
    const element = this.query<HTMLElement>(fixture, selector);
    if (element) {
      element.click();
      fixture.detectChanges();
    }
  }

  /**
   * Set input value and trigger change event
   */
  static setInputValue(
    fixture: ComponentFixture<any>,
    selector: string,
    value: string
  ): void {
    const input = this.query<HTMLInputElement>(fixture, selector);
    if (input) {
      input.value = value;
      input.dispatchEvent(new Event('input'));
      input.dispatchEvent(new Event('change'));
      fixture.detectChanges();
    }
  }

  /**
   * Wait for async operations to complete
   */
  static async waitForAsync(): Promise<void> {
    await new Promise(resolve => setTimeout(resolve, 0));
  }

  /**
   * Get text content of element
   */
  static getText(fixture: ComponentFixture<any>, selector: string): string {
    const element = this.query<HTMLElement>(fixture, selector);
    return element?.textContent?.trim() || '';
  }

  /**
   * Check if element has CSS class
   */
  static hasClass(
    fixture: ComponentFixture<any>,
    selector: string,
    className: string
  ): boolean {
    const element = this.query<HTMLElement>(fixture, selector);
    return element?.classList.contains(className) || false;
  }

  /**
   * Get attribute value
   */
  static getAttribute(
    fixture: ComponentFixture<any>,
    selector: string,
    attribute: string
  ): string | null {
    const element = this.query<HTMLElement>(fixture, selector);
    return element?.getAttribute(attribute) || null;
  }
}

/**
 * Custom matchers for Jasmine
 */
export const customMatchers = {
  toBeVisible: (util: any, customEqualityTesters: any) => ({
    compare: (actual: HTMLElement) => {
      const result: any = { pass: false };
      const styles = window.getComputedStyle(actual);
      result.pass =
        styles.display !== 'none' &&
        styles.visibility !== 'hidden' &&
        styles.opacity !== '0';
      result.message = result.pass
        ? `Expected element not to be visible`
        : `Expected element to be visible`;
      return result;
    },
  }),

  toHaveText: (util: any, customEqualityTesters: any) => ({
    compare: (actual: HTMLElement, expected: string) => {
      const result: any = { pass: false };
      const actualText = actual.textContent?.trim() || '';
      result.pass = actualText === expected;
      result.message = result.pass
        ? `Expected element not to have text "${expected}"`
        : `Expected element to have text "${expected}" but got "${actualText}"`;
      return result;
    },
  }),
};
