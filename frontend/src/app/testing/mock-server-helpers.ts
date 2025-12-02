/**
 * Mock Server Helpers for E2E Tests
 */

import { Page, Route } from '@playwright/test';

/**
 * Mock server utilities for E2E testing
 */
export class MockServerHelpers {
  /**
   * Mock API endpoint
   */
  static mockApiEndpoint(
    page: Page,
    url: string | RegExp,
    response: any,
    status: number = 200
  ): void {
    page.route(url, route => {
      route.fulfill({
        status,
        contentType: 'application/json',
        body: JSON.stringify(response)
      });
    });
  }

  /**
   * Mock API endpoint with delay
   */
  static mockApiEndpointWithDelay(
    page: Page,
    url: string | RegExp,
    response: any,
    delay: number = 1000,
    status: number = 200
  ): void {
    page.route(url, route => {
      setTimeout(() => {
        route.fulfill({
          status,
          contentType: 'application/json',
          body: JSON.stringify(response)
        });
      }, delay);
    });
  }

  /**
   * Mock API endpoint with error
   */
  static mockApiError(
    page: Page,
    url: string | RegExp,
    status: number = 500,
    message: string = 'Server Error'
  ): void {
    page.route(url, route => {
      route.fulfill({
        status,
        contentType: 'application/json',
        body: JSON.stringify({ error: message })
      });
    });
  }

  /**
   * Mock API endpoint sequence (multiple responses)
   */
  static mockApiSequence(
    page: Page,
    url: string | RegExp,
    responses: Array<{ response: any; status?: number; delay?: number }>
  ): void {
    let callCount = 0;
    page.route(url, route => {
      const currentResponse = responses[callCount % responses.length];
      callCount++;

      const fulfill = () => {
        route.fulfill({
          status: currentResponse.status || 200,
          contentType: 'application/json',
          body: JSON.stringify(currentResponse.response)
        });
      };

      if (currentResponse.delay) {
        setTimeout(fulfill, currentResponse.delay);
      } else {
        fulfill();
      }
    });
  }

  /**
   * Mock API endpoint with request validation
   */
  static mockApiWithValidation(
    page: Page,
    url: string | RegExp,
    validator: (request: Route) => boolean,
    successResponse: any,
    errorResponse: any = { error: 'Validation failed' }
  ): void {
    page.route(url, route => {
      if (validator(route)) {
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(successResponse)
        });
      } else {
        route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify(errorResponse)
        });
      }
    });
  }

  /**
   * Unmock all routes
   */
  static unmockAll(page: Page): void {
    page.unroute('**/*');
  }

  /**
   * Create mock server configuration
   */
  static createMockServerConfig(endpoints: Array<{
    url: string | RegExp;
    method?: string;
    response: any;
    status?: number;
  }>): (page: Page) => void {
    return (page: Page) => {
      endpoints.forEach(endpoint => {
        page.route(endpoint.url, route => {
          if (!endpoint.method || route.request().method() === endpoint.method) {
            route.fulfill({
              status: endpoint.status || 200,
              contentType: 'application/json',
              body: JSON.stringify(endpoint.response)
            });
          } else {
            route.continue();
          }
        });
      });
    };
  }
}
