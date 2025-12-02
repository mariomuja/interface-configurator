import { test, expect } from '@playwright/test';

test.describe('Authentication', () => {
  test('should display login dialog', async ({ page }) => {
    await page.goto('/');
    
    // Check if login dialog or login button is visible
    const loginButton = page.getByRole('button', { name: /login|anmelden/i });
    if (await loginButton.isVisible()) {
      await loginButton.click();
    }
    
    // Wait for login dialog
    await expect(page.getByText(/anmelden|login/i)).toBeVisible({ timeout: 5000 });
  });

  test('should show demo login option', async ({ page }) => {
    await page.goto('/');
    
    // Open login dialog if needed
    const loginButton = page.getByRole('button', { name: /login|anmelden/i });
    if (await loginButton.isVisible()) {
      await loginButton.click();
    }
    
    // Check for demo login button
    await expect(page.getByText(/demo|test/i)).toBeVisible({ timeout: 5000 });
  });
});
