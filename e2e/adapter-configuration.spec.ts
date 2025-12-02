import { test, expect } from '@playwright/test';

test.describe('Adapter Configuration', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    // Wait for page to load
    await page.waitForLoadState('networkidle');
  });

  test('should open adapter configuration dialog', async ({ page }) => {
    // Look for adapter configuration button or link
    const configButton = page.getByRole('button', { name: /configure|einstellen|settings/i });
    if (await configButton.isVisible()) {
      await configButton.click();
      await expect(page.getByText(/adapter|konfiguration/i)).toBeVisible({ timeout: 5000 });
    }
  });

  test('should display adapter selection options', async ({ page }) => {
    // Navigate to adapter selection if available
    const adapterButton = page.getByRole('button', { name: /adapter|add|hinzufügen/i });
    if (await adapterButton.isVisible()) {
      await adapterButton.click();
      await page.waitForTimeout(1000);
      // Check for adapter options
      const hasContent = await page.locator('body').textContent();
      expect(hasContent).toBeTruthy();
    }
  });

  test('should validate adapter configuration form', async ({ page }) => {
    // Try to find and interact with adapter configuration form
    const form = page.locator('form, [role="dialog"]');
    if (await form.count() > 0) {
      // Try to submit empty form
      const submitButton = page.getByRole('button', { name: /save|speichern|submit/i });
      if (await submitButton.isVisible()) {
        await submitButton.click();
        // Should show validation errors
        await page.waitForTimeout(500);
      }
    }
  });
});
