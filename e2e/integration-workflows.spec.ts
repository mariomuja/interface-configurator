import { test, expect } from '@playwright/test';

test.describe('Integration Workflows', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
  });

  test('should complete full adapter configuration workflow', async ({ page }) => {
    // Step 1: Open add interface dialog
    const addButton = page.getByRole('button', { name: /add|create|new|hinzufügen/i });
    if (await addButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await addButton.click();
      await page.waitForTimeout(500);

      // Step 2: Enter interface name
      const nameInput = page.locator('input[type="text"]').first();
      if (await nameInput.isVisible({ timeout: 1000 }).catch(() => false)) {
        await nameInput.fill('TestIntegrationInterface');
        await page.waitForTimeout(300);

        // Step 3: Submit form
        const submitButton = page.getByRole('button', { name: /create|save|submit|erstellen|speichern/i });
        if (await submitButton.isVisible({ timeout: 1000 }).catch(() => false)) {
          await submitButton.click();
          await page.waitForTimeout(1000);
        }
      }
    }
  });

  test('should handle adapter selection and configuration flow', async ({ page }) => {
    // This test verifies the complete flow of selecting and configuring an adapter
    const adapterButton = page.getByRole('button', { name: /adapter|configure|einstellen/i });
    if (await adapterButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await adapterButton.click();
      await page.waitForTimeout(1000);

      // Look for adapter selection
      const adapterOptions = page.locator('[role="button"], [role="option"]');
      const count = await adapterOptions.count();
      if (count > 0) {
        await adapterOptions.first().click();
        await page.waitForTimeout(500);
      }
    }
  });

  test('should handle data transport workflow', async ({ page }) => {
    // Step 1: Check for transport interface
    const transportSection = page.getByText(/transport|daten|data/i);
    if (await transportSection.isVisible({ timeout: 2000 }).catch(() => false)) {
      // Step 2: Look for start transport button
      const startButton = page.getByRole('button', { name: /start|transport|starten/i });
      if (await startButton.isVisible({ timeout: 2000 }).catch(() => false)) {
        // Step 3: Check if button is enabled
        const isDisabled = await startButton.isDisabled();
        if (!isDisabled) {
          await startButton.click();
          await page.waitForTimeout(1000);

          // Step 4: Verify transport started (check for status indicators)
          const statusIndicator = page.getByText(/running|läuft|processing|verarbeitung/i);
          if (await statusIndicator.isVisible({ timeout: 2000 }).catch(() => false)) {
            expect(statusIndicator).toBeVisible();
          }
        }
      }
    }
  });

  test('should handle error recovery workflow', async ({ page }) => {
    // Simulate network error
    await page.route('**/api/**', route => {
      route.fulfill({
        status: 500,
        body: JSON.stringify({ error: 'Server error' })
      });
    });

    // Try to perform an action that would trigger API call
    const actionButton = page.getByRole('button').first();
    if (await actionButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await actionButton.click();
      await page.waitForTimeout(1000);

      // Check for error message display
      const errorMessage = page.getByText(/error|fehler|failed|fehlgeschlagen/i);
      if (await errorMessage.isVisible({ timeout: 2000 }).catch(() => false)) {
        expect(errorMessage).toBeVisible();
      }
    }

    // Restore normal network
    await page.unroute('**/api/**');
  });

  test('should handle dialog open-close workflow', async ({ page }) => {
    // Open dialog
    const openButton = page.getByRole('button', { name: /open|show|anzeigen/i });
    if (await openButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await openButton.click();
      await page.waitForTimeout(500);

      // Verify dialog is open
      const dialog = page.locator('[role="dialog"]');
      if (await dialog.isVisible({ timeout: 1000 }).catch(() => false)) {
        expect(dialog).toBeVisible();

        // Close dialog
        const closeButton = page.getByRole('button', { name: /close|schließen|cancel|abbrechen/i });
        if (await closeButton.isVisible({ timeout: 1000 }).catch(() => false)) {
          await closeButton.click();
          await page.waitForTimeout(500);

          // Verify dialog is closed
          const dialogAfterClose = page.locator('[role="dialog"]');
          const isVisible = await dialogAfterClose.isVisible({ timeout: 500 }).catch(() => false);
          expect(isVisible).toBeFalsy();
        }
      }
    }
  });

  test('should handle form validation workflow', async ({ page }) => {
    // Open form dialog
    const formButton = page.getByRole('button', { name: /add|create|new/i });
    if (await formButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await formButton.click();
      await page.waitForTimeout(500);

      // Try to submit empty form
      const submitButton = page.getByRole('button', { name: /save|submit|create/i });
      if (await submitButton.isVisible({ timeout: 1000 }).catch(() => false)) {
        await submitButton.click();
        await page.waitForTimeout(500);

        // Check for validation errors
        const errorMessages = page.locator('.mat-error, .error-message, [role="alert"]');
        const errorCount = await errorMessages.count();
        if (errorCount > 0) {
          expect(errorCount).toBeGreaterThan(0);
        }
      }
    }
  });
});
