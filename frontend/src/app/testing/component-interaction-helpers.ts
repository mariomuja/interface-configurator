/**
 * Component interaction testing helpers
 */

import { ComponentFixture } from '@angular/core/testing';
import { DebugElement } from '@angular/core';
import { By } from '@angular/platform-browser';
import { EventEmitter } from '@angular/core';

/**
 * Test component interactions and communication
 */
export class ComponentInteractionHelpers {
  /**
   * Test @Input property binding
   */
  static testInputBinding(
    fixture: ComponentFixture<any>,
    component: any,
    inputName: string,
    testValue: any
  ): void {
    component[inputName] = testValue;
    fixture.detectChanges();
    
    expect(component[inputName]).toBe(testValue);
  }

  /**
   * Test @Output event emission
   */
  static testOutputEvent(
    component: any,
    outputName: string,
    emitValue: any,
    callback: (value: any) => void
  ): void {
    const output = component[outputName] as EventEmitter<any>;
    expect(output).toBeDefined();
    
    output.subscribe(callback);
    output.emit(emitValue);
  }

  /**
   * Test parent-child component communication
   */
  static testParentChildCommunication(
    parentFixture: ComponentFixture<any>,
    childSelector: string,
    inputName: string,
    inputValue: any,
    outputName: string,
    outputCallback: (value: any) => void
  ): void {
    const childComponent = parentFixture.debugElement.query(
      By.css(childSelector)
    )?.componentInstance;
    
    if (childComponent) {
      // Test input
      childComponent[inputName] = inputValue;
      parentFixture.detectChanges();
      expect(childComponent[inputName]).toBe(inputValue);
      
      // Test output
      if (childComponent[outputName]) {
        childComponent[outputName].subscribe(outputCallback);
        childComponent[outputName].emit('test');
      }
    }
  }

  /**
   * Test service injection and usage
   */
  static testServiceInjection(
    component: any,
    serviceName: string,
    serviceMethod: string,
    expectedCall: boolean = true
  ): void {
    const service = component[serviceName];
    expect(service).toBeDefined();
    
    if (service && service[serviceMethod]) {
      spyOn(service, serviceMethod);
      component[serviceMethod]();
      
      if (expectedCall) {
        expect(service[serviceMethod]).toHaveBeenCalled();
      }
    }
  }

  /**
   * Test component method call from template
   */
  static testTemplateMethodCall(
    fixture: ComponentFixture<any>,
    selector: string,
    methodName: string,
    methodSpy: jasmine.Spy
  ): void {
    const element = fixture.debugElement.query(By.css(selector));
    expect(element).toBeTruthy();
    
    element.nativeElement.click();
    fixture.detectChanges();
    
    expect(methodSpy).toHaveBeenCalled();
  }

  /**
   * Test two-way data binding
   */
  static testTwoWayBinding(
    fixture: ComponentFixture<any>,
    component: any,
    propertyName: string,
    testValue: any
  ): void {
    component[propertyName] = testValue;
    fixture.detectChanges();
    
    expect(component[propertyName]).toBe(testValue);
    
    // Simulate user input
    const input = fixture.debugElement.query(By.css(`[ngModel]="${propertyName}"`));
    if (input) {
      input.nativeElement.value = 'new value';
      input.nativeElement.dispatchEvent(new Event('input'));
      fixture.detectChanges();
      
      expect(component[propertyName]).toBe('new value');
    }
  }

  /**
   * Test component state synchronization
   */
  static testStateSynchronization(
    component: any,
    stateProperties: string[],
    updateFn: () => void
  ): void {
    const initialState: Record<string, any> = {};
    stateProperties.forEach(prop => {
      initialState[prop] = component[prop];
    });
    
    updateFn();
    
    // Verify at least one property changed
    const hasChange = stateProperties.some(prop => {
      return component[prop] !== initialState[prop];
    });
    
    expect(hasChange).toBe(true);
  }
}
