/**
 * Router testing helpers
 */

import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Location } from '@angular/common';
import { ComponentFixture } from '@angular/core/testing';

/**
 * Router test utilities
 */
export class RouterTestHelpers {
  /**
   * Create router testing module
   */
  static createRouterTestingModule(routes: any[] = []) {
    return TestBed.configureTestingModule({
      imports: [RouterTestingModule],
      providers: []
    });
  }

  /**
   * Navigate and wait for completion
   */
  static async navigateAndWait(router: Router, url: string): Promise<boolean> {
    const result = await router.navigateByUrl(url);
    await new Promise(resolve => setTimeout(resolve, 100));
    return result;
  }

  /**
   * Get current route
   */
  static getCurrentRoute(router: Router): string {
    return router.url;
  }

  /**
   * Test route navigation
   */
  static async testRouteNavigation(
    router: Router,
    location: Location,
    targetUrl: string
  ): Promise<void> {
    await this.navigateAndWait(router, targetUrl);
    expect(location.path()).toBe(targetUrl);
  }

  /**
   * Test route guard activation
   */
  static async testRouteGuard(
    guard: any,
    route: any,
    state: any,
    expectedResult: boolean
  ): Promise<void> {
    const result = await guard.canActivate(route, state);
    expect(result).toBe(expectedResult);
  }

  /**
   * Test route guard deactivation
   */
  static async testRouteGuardDeactivate(
    guard: any,
    component: any,
    route: any,
    state: any,
    expectedResult: boolean
  ): Promise<void> {
    const result = await guard.canDeactivate(component, route, state);
    expect(result).toBe(expectedResult);
  }

  /**
   * Mock router navigation
   */
  static createMockRouter() {
    return {
      navigate: jasmine.createSpy('navigate').and.returnValue(Promise.resolve(true)),
      navigateByUrl: jasmine.createSpy('navigateByUrl').and.returnValue(Promise.resolve(true)),
      url: '/',
      events: {
        subscribe: jasmine.createSpy('subscribe')
      }
    };
  }

  /**
   * Mock location service
   */
  static createMockLocation() {
    return {
      path: jasmine.createSpy('path').and.returnValue('/'),
      back: jasmine.createSpy('back'),
      forward: jasmine.createSpy('forward'),
      go: jasmine.createSpy('go')
    };
  }
}

// Import RouterTestingModule if available
let RouterTestingModule: any;
try {
  RouterTestingModule = require('@angular/router/testing').RouterTestingModule;
} catch (e) {
  // RouterTestingModule not available
}
