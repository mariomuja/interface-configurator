/**
 * Integration test helpers for testing component-service interactions
 */

import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatDialogModule } from '@angular/material/dialog';
import { MatSnackBarModule } from '@angular/material/snack-bar';

/**
 * Common test module configuration for integration tests
 */
export function createIntegrationTestModule(component: any, providers: any[] = []) {
  return TestBed.configureTestingModule({
    imports: [
      component,
      HttpClientTestingModule,
      NoopAnimationsModule,
      MatDialogModule,
      MatSnackBarModule,
    ],
    providers: [...providers],
  });
}

/**
 * Wait for all async operations to complete
 */
export async function waitForAsyncOperations(): Promise<void> {
  await new Promise(resolve => setTimeout(resolve, 100));
}

/**
 * Create a spy that tracks call counts and arguments
 */
export function createTrackingSpy(name: string, returnValue: any = undefined) {
  const spy = jasmine.createSpy(name);
  if (returnValue !== undefined) {
    spy.and.returnValue(returnValue);
  }
  return spy;
}

/**
 * Verify service was called with specific arguments
 */
export function verifyServiceCall(
  spy: jasmine.Spy,
  expectedCallCount: number,
  expectedArgs?: any[]
) {
  expect(spy).toHaveBeenCalledTimes(expectedCallCount);
  if (expectedArgs) {
    expect(spy).toHaveBeenCalledWith(...expectedArgs);
  }
}

/**
 * Create mock HTTP response
 */
export function createMockHttpResponse(data: any, status: number = 200) {
  return {
    status,
    statusText: status === 200 ? 'OK' : 'Error',
    body: data,
  };
}

/**
 * Simulate user interaction sequence
 */
export async function simulateUserInteraction(
  fixture: any,
  interactions: Array<{ type: string; selector: string; value?: any }>
) {
  for (const interaction of interactions) {
    switch (interaction.type) {
      case 'click':
        const clickElement = fixture.nativeElement.querySelector(interaction.selector);
        if (clickElement) {
          clickElement.click();
          fixture.detectChanges();
          await waitForAsyncOperations();
        }
        break;
      case 'input':
        const inputElement = fixture.nativeElement.querySelector(interaction.selector);
        if (inputElement) {
          inputElement.value = interaction.value;
          inputElement.dispatchEvent(new Event('input'));
          inputElement.dispatchEvent(new Event('change'));
          fixture.detectChanges();
          await waitForAsyncOperations();
        }
        break;
    }
  }
}
