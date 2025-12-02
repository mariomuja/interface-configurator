import { test, expect } from '@playwright/test';

test.describe('Visual Regression Tests', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
  });

  test('should match main page screenshot', async ({ page }) => {
    await expect(page).toHaveScreenshot('main-page.png', {
      fullPage: true,
      threshold: 0.2
    });
  });

  test('should match login dialog screenshot', async ({ page }) => {
    const loginButton = page.getByRole('button', { name: /login|anmelden/i });
    if (await loginButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await loginButton.click();
      await page.waitForTimeout(500);

      const dialog = page.locator('[role="dialog"]');
      if (await dialog.isVisible({ timeout: 1000 }).catch(() => false)) {
        await expect(dialog).toHaveScreenshot('login-dialog.png', {
          threshold: 0.2
        });
      }
    }
  });

  test('should match transport component screenshot', async ({ page }) => {
    const transportSection = page.locator('app-transport, [class*="transport"]').first();
    if (await transportSection.isVisible({ timeout: 2000 }).catch(() => false)) {
      await expect(transportSection).toHaveScreenshot('transport-component.png', {
        threshold: 0.2
      });
    }
  });

  test('should match mobile viewport screenshot', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 667 });
    await page.waitForTimeout(500);

    await expect(page).toHaveScreenshot('mobile-viewport.png', {
      fullPage: true,
      threshold: 0.3
    });
  });

  test('should match tablet viewport screenshot', async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.waitForTimeout(500);

    await expect(page).toHaveScreenshot('tablet-viewport.png', {
      fullPage: true,
      threshold: 0.3
    });
  });
});
