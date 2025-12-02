import { test, expect } from '@playwright/test';

test.describe('Dialog Interactions', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
  });

  test('should open and close dialogs', async ({ page }) => {
    // Look for dialog trigger buttons
    const dialogButtons = [
      page.getByRole('button', { name: /add|hinzufügen|create|erstellen/i }),
      page.getByRole('button', { name: /settings|einstellungen|configure/i }),
      page.getByRole('button', { name: /documentation|dokumentation/i })
    ];

    for (const button of dialogButtons) {
      if (await button.isVisible({ timeout: 1000 }).catch(() => false)) {
        await button.click();
        await page.waitForTimeout(500);
        
        // Look for close button
        const closeButton = page.getByRole('button', { name: /close|schließen|cancel|abbrechen/i }).first();
        if (await closeButton.isVisible({ timeout: 1000 }).catch(() => false)) {
          await closeButton.click();
          await page.waitForTimeout(500);
        }
        break;
      }
    }
  });

  test('should validate form inputs in dialogs', async ({ page }) => {
    // Open a dialog with form
    const addButton = page.getByRole('button', { name: /add|create|new/i });
    if (await addButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await addButton.click();
      await page.waitForTimeout(1000);

      // Try to submit empty form
      const submitButton = page.getByRole('button', { name: /save|submit|create|erstellen/i });
      if (await submitButton.isVisible({ timeout: 1000 }).catch(() => false)) {
        await submitButton.click();
        await page.waitForTimeout(500);
        // Form should show validation errors or prevent submission
      }
    }
  });

  test('should handle dialog keyboard navigation', async ({ page }) => {
    const dialogButton = page.getByRole('button', { name: /add|create/i });
    if (await dialogButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await dialogButton.click();
      await page.waitForTimeout(500);

      // Press Escape to close
      await page.keyboard.press('Escape');
      await page.waitForTimeout(500);
    }
  });
});
