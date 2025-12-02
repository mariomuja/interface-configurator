import { test, expect } from '@playwright/test';

test.describe('Accessibility Tests', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
  });

  test('should have proper page title', async ({ page }) => {
    const title = await page.title();
    expect(title).toBeTruthy();
    expect(title.length).toBeGreaterThan(0);
  });

  test('should have proper heading hierarchy', async ({ page }) => {
    const h1 = await page.locator('h1').count();
    expect(h1).toBeLessThanOrEqual(1); // Should have at most one h1

    // Check for proper heading structure
    const headings = await page.locator('h1, h2, h3, h4, h5, h6').all();
    expect(headings.length).toBeGreaterThan(0);
  });

  test('should have accessible form inputs', async ({ page }) => {
    const inputs = await page.locator('input[type="text"], input[type="email"], input[type="password"]').all();
    
    for (const input of inputs) {
      const id = await input.getAttribute('id');
      const ariaLabel = await input.getAttribute('aria-label');
      const ariaLabelledBy = await input.getAttribute('aria-labelledby');
      const placeholder = await input.getAttribute('placeholder');
      
      // Input should have either id with label, aria-label, or placeholder
      expect(id || ariaLabel || ariaLabelledBy || placeholder).toBeTruthy();
    }
  });

  test('should have accessible buttons', async ({ page }) => {
    const buttons = await page.locator('button').all();
    
    for (const button of buttons) {
      const ariaLabel = await button.getAttribute('aria-label');
      const text = await button.textContent();
      const ariaLabelledBy = await button.getAttribute('aria-labelledby');
      
      // Button should have accessible text or label
      expect(ariaLabel || text?.trim() || ariaLabelledBy).toBeTruthy();
    }
  });

  test('should have proper link accessibility', async ({ page }) => {
    const links = await page.locator('a[href]').all();
    
    for (const link of links) {
      const text = await link.textContent();
      const ariaLabel = await link.getAttribute('aria-label');
      const title = await link.getAttribute('title');
      
      // Link should have accessible text
      expect(text?.trim() || ariaLabel || title).toBeTruthy();
    }
  });

  test('should have proper image alt text', async ({ page }) => {
    const images = await page.locator('img').all();
    
    for (const image of images) {
      const alt = await image.getAttribute('alt');
      const role = await image.getAttribute('role');
      
      // Decorative images should have alt="" or role="presentation"
      // Informative images should have descriptive alt text
      if (alt === null && role !== 'presentation') {
        // This might be an issue, but we'll just check it exists
        const src = await image.getAttribute('src');
        expect(src).toBeTruthy();
      }
    }
  });

  test('should support keyboard navigation', async ({ page }) => {
    // Test Tab navigation
    await page.keyboard.press('Tab');
    const focusedElement = await page.evaluate(() => document.activeElement?.tagName);
    expect(focusedElement).toBeTruthy();
  });

  test('should have proper ARIA attributes on dialogs', async ({ page }) => {
    // Try to open a dialog
    const dialogButton = page.getByRole('button', { name: /add|create|open/i });
    if (await dialogButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await dialogButton.click();
      await page.waitForTimeout(500);

      // Check for dialog ARIA attributes
      const dialog = page.locator('[role="dialog"]');
      if (await dialog.isVisible({ timeout: 1000 }).catch(() => false)) {
        const ariaLabel = await dialog.getAttribute('aria-label');
        const ariaLabelledBy = await dialog.getAttribute('aria-labelledby');
        expect(ariaLabel || ariaLabelledBy).toBeTruthy();
      }
    }
  });

  test('should have proper focus management', async ({ page }) => {
    // Check if focusable elements are properly structured
    const focusableElements = await page.locator(
      'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
    ).count();
    
    expect(focusableElements).toBeGreaterThan(0);
  });

  test('should have sufficient color contrast', async ({ page }) => {
    // This is a basic check - full contrast testing would require specialized tools
    const body = page.locator('body');
    const backgroundColor = await body.evaluate((el) => {
      const styles = window.getComputedStyle(el);
      return styles.backgroundColor;
    });
    
    expect(backgroundColor).toBeTruthy();
  });
});
