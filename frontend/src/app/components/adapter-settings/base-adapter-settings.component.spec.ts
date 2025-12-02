import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { BaseAdapterSettingsComponent } from './base-adapter-settings.component';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

// Create a concrete test component that extends BaseAdapterSettingsComponent
@Component({
  template: '<div>Test Component</div>'
})
class TestAdapterSettingsComponent extends BaseAdapterSettingsComponent {
  getSettings(): any {
    return {
      instanceName: this.instanceName,
      isEnabled: this.isEnabled
    };
  }

  initializeSettings(data: any): void {
    this.instanceName = data.instanceName || '';
    this.isEnabled = data.isEnabled ?? true;
  }
}

describe('BaseAdapterSettingsComponent', () => {
  let component: TestAdapterSettingsComponent;
  let fixture: ComponentFixture<TestAdapterSettingsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [
        TestAdapterSettingsComponent,
        NoopAnimationsModule
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TestAdapterSettingsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('validateSettings', () => {
    it('should return valid when instance name is provided', () => {
      component.instanceName = 'TestInstance';
      const result = component.validateSettings();
      expect(result.valid).toBe(true);
      expect(result.errors.length).toBe(0);
    });

    it('should return invalid when instance name is empty', () => {
      component.instanceName = '';
      const result = component.validateSettings();
      expect(result.valid).toBe(false);
      expect(result.errors).toContain('Instance name is required');
    });

    it('should return invalid when instance name is only whitespace', () => {
      component.instanceName = '   ';
      const result = component.validateSettings();
      expect(result.valid).toBe(false);
      expect(result.errors).toContain('Instance name is required');
    });
  });

  describe('onInstanceNameChange', () => {
    it('should update instance name and emit events', () => {
      spyOn(component.instanceNameChange, 'emit');
      spyOn(component.settingsChange, 'emit');

      component.onInstanceNameChange('NewInstance');

      expect(component.instanceName).toBe('NewInstance');
      expect(component.instanceNameChange.emit).toHaveBeenCalledWith('NewInstance');
      expect(component.settingsChange.emit).toHaveBeenCalled();
    });
  });

  describe('onEnabledChange', () => {
    it('should update isEnabled and emit events', () => {
      spyOn(component.isEnabledChange, 'emit');
      spyOn(component.settingsChange, 'emit');

      component.onEnabledChange(false);

      expect(component.isEnabled).toBe(false);
      expect(component.isEnabledChange.emit).toHaveBeenCalledWith(false);
      expect(component.settingsChange.emit).toHaveBeenCalled();
    });
  });

  describe('initializeSettings', () => {
    it('should initialize settings from data', () => {
      const data = {
        instanceName: 'InitialInstance',
        isEnabled: false
      };

      component.initializeSettings(data);

      expect(component.instanceName).toBe('InitialInstance');
      expect(component.isEnabled).toBe(false);
    });
  });

  describe('getSettings', () => {
    it('should return current settings', () => {
      component.instanceName = 'TestInstance';
      component.isEnabled = true;

      const settings = component.getSettings();

      expect(settings.instanceName).toBe('TestInstance');
      expect(settings.isEnabled).toBe(true);
    });
  });
});
