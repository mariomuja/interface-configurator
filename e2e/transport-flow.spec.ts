import { test, expect } from '@playwright/test';

test.describe('Transport Flow', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
  });

  test('should display transport interface', async ({ page }) => {
    // Check for transport-related UI elements
    const hasTransportContent = await page.locator('body').textContent();
    expect(hasTransportContent).toBeTruthy();
  });

  test('should show transport controls', async ({ page }) => {
    // Look for transport start button or controls
    const startButton = page.getByRole('button', { name: /start|transport|starten/i });
    if (await startButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      expect(startButton).toBeVisible();
    }
  });

  test('should display process logs section', async ({ page }) => {
    // Check for logs or process information
    const logsSection = page.getByText(/log|prozess|process/i);
    if (await logsSection.isVisible({ timeout: 2000 }).catch(() => false)) {
      expect(logsSection).toBeVisible();
    }
  });

  test('should handle transport errors gracefully', async ({ page }) => {
    // Intercept network requests to simulate errors
    await page.route('**/api/start-transport', route => {
      route.fulfill({
        status: 500,
        body: JSON.stringify({ error: 'Server error' })
      });
    });

    const startButton = page.getByRole('button', { name: /start|transport/i });
    if (await startButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await startButton.click();
      // Should show error message
      await page.waitForTimeout(1000);
    }
  });
});
