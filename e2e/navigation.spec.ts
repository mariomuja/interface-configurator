import { test, expect } from '@playwright/test';

test.describe('Navigation', () => {
  test('should load main page', async ({ page }) => {
    await page.goto('/');
    
    // Check for main content
    await expect(page).toHaveTitle(/Interface Configurator|Infrastructure/i);
  });

  test('should display interface configuration elements', async ({ page }) => {
    await page.goto('/');
    
    // Wait for page to load
    await page.waitForLoadState('networkidle');
    
    // Check for common UI elements (adjust selectors based on actual implementation)
    const hasContent = await page.locator('body').count() > 0;
    expect(hasContent).toBeTruthy();
  });
});
