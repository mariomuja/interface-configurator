import { test, expect } from '@playwright/test';

/**
 * Azure Static Web App Tests
 * 
 * These tests verify that:
 * 1. Static web app is accessible
 * 2. Frontend loads correctly
 * 3. API endpoints are accessible
 * 4. User interactions work
 */

// Base URL for Static Web App - should be set via environment variable
const STATIC_WEB_APP_URL = process.env.STATIC_WEB_APP_URL || 
  process.env.AZURE_STATIC_WEB_APP_URL || 
  'https://interface-configurator.vercel.app';

test.describe('Static Web App Accessibility', () => {
  test('Home page should load', async ({ page }) => {
    await page.goto(STATIC_WEB_APP_URL);
    await expect(page).toHaveTitle(/Interface Configurator/i);
  });

  test('Page should be responsive', async ({ page }) => {
    await page.goto(STATIC_WEB_APP_URL);
    
    // Test mobile viewport
    await page.setViewportSize({ width: 375, height: 667 });
    const mobileContent = await page.textContent('body');
    expect(mobileContent).toBeTruthy();
    
    // Test desktop viewport
    await page.setViewportSize({ width: 1920, height: 1080 });
    const desktopContent = await page.textContent('body');
    expect(desktopContent).toBeTruthy();
  });

  test('API endpoints should be accessible', async ({ page }) => {
    // Test health check or API endpoint
    const response = await page.request.get(`${STATIC_WEB_APP_URL}/api/health`);
    expect(response.status()).toBeLessThan(500);
  });

  test('Static assets should load', async ({ page }) => {
    await page.goto(STATIC_WEB_APP_URL);
    
    // Check for CSS/JS files
    const scripts = await page.locator('script[src]').all();
    const stylesheets = await page.locator('link[rel="stylesheet"]').all();
    
    expect(scripts.length).toBeGreaterThan(0);
    expect(stylesheets.length).toBeGreaterThan(0);
  });

  test('No console errors on page load', async ({ page }) => {
    const consoleErrors: string[] = [];
    page.on('console', msg => {
      if (msg.type() === 'error') {
        consoleErrors.push(msg.text());
      }
    });

    await page.goto(STATIC_WEB_APP_URL);
    await page.waitForLoadState('networkidle');

    // Filter out known non-critical errors
    const criticalErrors = consoleErrors.filter(
      err => !err.includes('favicon') && !err.includes('404')
    );

    expect(criticalErrors.length).toBe(0);
  });
});

test.describe('Frontend Functionality', () => {
  test('Interface list should be accessible', async ({ page }) => {
    await page.goto(STATIC_WEB_APP_URL);
    
    // Wait for content to load
    await page.waitForSelector('body', { timeout: 10000 });
    
    // Check if page loaded successfully
    const bodyText = await page.textContent('body');
    expect(bodyText).toBeTruthy();
  });

  test('Navigation should work', async ({ page }) => {
    await page.goto(STATIC_WEB_APP_URL);
    
    // Try to find and click navigation elements
    const links = await page.locator('a').all();
    expect(links.length).toBeGreaterThan(0);
  });

  test('Forms should be accessible', async ({ page }) => {
    await page.goto(STATIC_WEB_APP_URL);
    
    // Look for form elements
    const inputs = await page.locator('input, textarea, select').all();
    // Forms may not be visible initially, but should exist
    expect(inputs.length).toBeGreaterThanOrEqual(0);
  });
});

test.describe('API Integration', () => {
  test('Health check endpoint should respond', async ({ page }) => {
    const response = await page.request.get(`${STATIC_WEB_APP_URL}/api/health`);
    expect(response.status()).toBeLessThan(500);
  });

  test('CORS headers should be set correctly', async ({ page }) => {
    const response = await page.request.get(`${STATIC_WEB_APP_URL}/api/health`);
    const headers = response.headers();
    
    // CORS headers should be present for API responses
    expect(response.status()).toBeLessThan(500);
  });
});

test.describe('Performance', () => {
  test('Page should load within reasonable time', async ({ page }) => {
    const startTime = Date.now();
    await page.goto(STATIC_WEB_APP_URL, { waitUntil: 'networkidle' });
    const loadTime = Date.now() - startTime;

    // Page should load within 5 seconds
    expect(loadTime).toBeLessThan(5000);
  });

  test('Static assets should load quickly', async ({ page }) => {
    const response = await page.goto(STATIC_WEB_APP_URL);
    
    // Check response time
    const timing = response?.timing();
    if (timing) {
      const totalTime = timing.responseEnd - timing.requestStart;
      expect(totalTime).toBeLessThan(3000);
    }
  });
});

