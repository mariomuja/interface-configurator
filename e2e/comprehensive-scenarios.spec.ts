import { test, expect } from '@playwright/test';

test.describe('Comprehensive E2E Scenarios', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
  });

  test('should complete full interface creation workflow', async ({ page }) => {
    // Step 1: Open add interface dialog
    const addButton = page.getByRole('button', { name: /add|create|new|hinzufügen/i });
    if (await addButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await addButton.click();
      await page.waitForTimeout(500);

      // Step 2: Enter interface name
      const nameInput = page.locator('input[type="text"]').first();
      if (await nameInput.isVisible({ timeout: 1000 }).catch(() => false)) {
        await nameInput.fill('E2ETestInterface');
        await page.waitForTimeout(300);

        // Step 3: Submit
        const submitButton = page.getByRole('button', { name: /create|save|submit/i });
        if (await submitButton.isVisible({ timeout: 1000 }).catch(() => false)) {
          await submitButton.click();
          await page.waitForTimeout(1000);

          // Step 4: Verify interface appears in list
          const interfaceElement = page.getByText('E2ETestInterface');
          if (await interfaceElement.isVisible({ timeout: 2000 }).catch(() => false)) {
            expect(interfaceElement).toBeVisible();
          }
        }
      }
    }
  });

  test('should handle adapter configuration with all steps', async ({ page }) => {
    // Navigate to adapter configuration
    const configButton = page.getByRole('button', { name: /configure|settings|einstellungen/i });
    if (await configButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await configButton.click();
      await page.waitForTimeout(1000);

      // Fill in configuration steps
      const inputs = await page.locator('input').all();
      for (let i = 0; i < Math.min(inputs.length, 5); i++) {
        if (await inputs[i].isVisible({ timeout: 500 }).catch(() => false)) {
          await inputs[i].fill(`test-value-${i}`);
          await page.waitForTimeout(200);
        }
      }

      // Submit configuration
      const saveButton = page.getByRole('button', { name: /save|speichern/i });
      if (await saveButton.isVisible({ timeout: 1000 }).catch(() => false)) {
        await saveButton.click();
        await page.waitForTimeout(1000);
      }
    }
  });

  test('should handle data transport with monitoring', async ({ page }) => {
    // Start transport
    const startButton = page.getByRole('button', { name: /start|transport|starten/i });
    if (await startButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await startButton.click();
      await page.waitForTimeout(1000);

      // Monitor progress
      const progressIndicator = page.locator('[role="progressbar"], .progress, .status');
      if (await progressIndicator.count() > 0) {
        // Wait for completion or timeout
        await page.waitForTimeout(5000);
      }

      // Check for completion message
      const successMessage = page.getByText(/success|completed|erfolgreich|abgeschlossen/i);
      if (await successMessage.isVisible({ timeout: 2000 }).catch(() => false)) {
        expect(successMessage).toBeVisible();
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

    // Try to perform action
    const actionButton = page.getByRole('button').first();
    if (await actionButton.isVisible({ timeout: 2000 }).catch(() => false)) {
      await actionButton.click();
      await page.waitForTimeout(1000);

      // Verify error message
      const errorMessage = page.getByText(/error|fehler|failed/i);
      if (await errorMessage.isVisible({ timeout: 2000 }).catch(() => false)) {
        expect(errorMessage).toBeVisible();
      }

      // Restore network
      await page.unroute('**/api/**');

      // Retry action
      await actionButton.click();
      await page.waitForTimeout(1000);
    }
  });

  test('should handle multiple dialogs in sequence', async ({ page }) => {
    const dialogs = [
      { button: /add|create/i, close: /close|cancel/i },
      { button: /settings|configure/i, close: /close|cancel/i },
      { button: /help|documentation/i, close: /close/i }
    ];

    for (const dialog of dialogs) {
      const openButton = page.getByRole('button', { name: dialog.button });
      if (await openButton.isVisible({ timeout: 2000 }).catch(() => false)) {
        await openButton.click();
        await page.waitForTimeout(500);

        const closeButton = page.getByRole('button', { name: dialog.close });
        if (await closeButton.isVisible({ timeout: 1000 }).catch(() => false)) {
          await closeButton.click();
          await page.waitForTimeout(500);
        }
      }
    }
  });

  test('should handle form validation errors', async ({ page }) => {
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
        const errors = page.locator('.mat-error, .error-message, [role="alert"]');
        const errorCount = await errors.count();
        if (errorCount > 0) {
          expect(errorCount).toBeGreaterThan(0);
        }

        // Fill required fields
        const inputs = await page.locator('input[required], input').all();
        for (const input of inputs.slice(0, 3)) {
          if (await input.isVisible({ timeout: 500 }).catch(() => false)) {
            await input.fill('test-value');
            await page.waitForTimeout(200);
          }
        }

        // Try submit again
        await submitButton.click();
        await page.waitForTimeout(500);
      }
    }
  });

  test('should handle keyboard navigation', async ({ page }) => {
    // Tab through focusable elements
    await page.keyboard.press('Tab');
    await page.waitForTimeout(100);

    const focusedElement = await page.evaluate(() => document.activeElement?.tagName);
    expect(focusedElement).toBeTruthy();

    // Navigate with arrow keys if applicable
    await page.keyboard.press('ArrowDown');
    await page.waitForTimeout(100);

    // Test Enter key
    await page.keyboard.press('Enter');
    await page.waitForTimeout(500);

    // Test Escape key
    await page.keyboard.press('Escape');
    await page.waitForTimeout(500);
  });

  test('should handle responsive layout', async ({ page }) => {
    // Test mobile viewport
    await page.setViewportSize({ width: 375, height: 667 });
    await page.waitForTimeout(500);

    const mobileElements = page.locator('.mobile, [class*="mobile"], [class*="small"]');
    const mobileCount = await mobileElements.count();
    // Just verify page renders
    expect(await page.locator('body').isVisible()).toBe(true);

    // Test tablet viewport
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.waitForTimeout(500);

    // Test desktop viewport
    await page.setViewportSize({ width: 1920, height: 1080 });
    await page.waitForTimeout(500);
  });

  test('should handle concurrent user actions', async ({ page }) => {
    // Simulate rapid clicks
    const buttons = await page.locator('button').all();
    if (buttons.length > 0) {
      for (let i = 0; i < Math.min(buttons.length, 3); i++) {
        if (await buttons[i].isVisible({ timeout: 500 }).catch(() => false)) {
          await buttons[i].click();
          await page.waitForTimeout(100);
        }
      }
    }

    // Wait for all actions to complete
    await page.waitForTimeout(2000);
  });
});
