/**
 * Component lifecycle testing helpers
 */

import { ComponentFixture } from '@angular/core/testing';
import { DebugElement } from '@angular/core';

/**
 * Test component lifecycle hooks
 */
export class ComponentLifecycleHelpers {
  /**
   * Test ngOnInit execution
   */
  static testOnInit(fixture: ComponentFixture<any>, initSpy?: jasmine.Spy): void {
    const component = fixture.componentInstance;
    
    if (initSpy) {
      expect(initSpy).not.toHaveBeenCalled();
    }
    
    fixture.detectChanges();
    
    if (initSpy) {
      expect(initSpy).toHaveBeenCalled();
    }
  }

  /**
   * Test ngOnDestroy execution
   */
  static testOnDestroy(fixture: ComponentFixture<any>, destroySpy?: jasmine.Spy): void {
    const component = fixture.componentInstance;
    
    if (destroySpy) {
      expect(destroySpy).not.toHaveBeenCalled();
    }
    
    fixture.destroy();
    
    if (destroySpy) {
      expect(destroySpy).toHaveBeenCalled();
    }
  }

  /**
   * Test ngOnChanges execution
   */
  static testOnChanges(
    fixture: ComponentFixture<any>,
    component: any,
    inputName: string,
    newValue: any,
    changesSpy?: jasmine.Spy
  ): void {
    if (changesSpy) {
      expect(changesSpy).not.toHaveBeenCalled();
    }
    
    component[inputName] = newValue;
    fixture.detectChanges();
    
    if (changesSpy) {
      expect(changesSpy).toHaveBeenCalled();
    }
  }

  /**
   * Test that subscriptions are cleaned up
   */
  static testSubscriptionCleanup(
    component: any,
    subscriptionProperty: string = 'subscription'
  ): void {
    const subscription = component[subscriptionProperty];
    
    if (subscription) {
      expect(subscription.closed).toBe(false);
      
      // Simulate component destruction
      if (component.ngOnDestroy) {
        component.ngOnDestroy();
      }
      
      expect(subscription.closed).toBe(true);
    }
  }

  /**
   * Test that multiple subscriptions are cleaned up
   */
  static testMultipleSubscriptionCleanup(
    component: any,
    subscriptionProperties: string[]
  ): void {
    subscriptionProperties.forEach(prop => {
      this.testSubscriptionCleanup(component, prop);
    });
  }

  /**
   * Test component initialization with async data
   */
  static async testAsyncInit(
    fixture: ComponentFixture<any>,
    dataLoadSpy: jasmine.Spy
  ): Promise<void> {
    fixture.detectChanges();
    
    // Wait for async operations
    await new Promise(resolve => setTimeout(resolve, 100));
    fixture.detectChanges();
    
    expect(dataLoadSpy).toHaveBeenCalled();
  }

  /**
   * Test component state after initialization
   */
  static testInitialState(
    component: any,
    expectedState: Record<string, any>
  ): void {
    Object.keys(expectedState).forEach(key => {
      expect(component[key]).toEqual(expectedState[key]);
    });
  }

  /**
   * Test component state changes
   */
  static testStateChange(
    component: any,
    changeFn: () => void,
    expectedState: Record<string, any>
  ): void {
    changeFn();
    
    Object.keys(expectedState).forEach(key => {
      expect(component[key]).toEqual(expectedState[key]);
    });
  }
}
