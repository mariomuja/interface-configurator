import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { Dynamics365AdapterSettingsComponent } from './dynamics365-adapter-settings.component';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('Dynamics365AdapterSettingsComponent', () => {
  let component: Dynamics365AdapterSettingsComponent;
  let fixture: ComponentFixture<Dynamics365AdapterSettingsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [
        Dynamics365AdapterSettingsComponent,
        HttpClientTestingModule,
        NoopAnimationsModule
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(Dynamics365AdapterSettingsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('initializeSettings', () => {
    it('should initialize Dynamics 365 settings from data', () => {
      const data = {
        dynamics365InstanceUrl: 'https://org.crm.dynamics.com',
        dynamics365TenantId: 'tenant-id',
        dynamics365ClientId: 'client-id',
        dynamics365ClientSecret: 'client-secret',
        entityName: 'accounts'
      };

      component.initializeSettings(data);

      expect(component.dynamics365InstanceUrl).toBe('https://org.crm.dynamics.com');
      expect(component.dynamics365TenantId).toBe('tenant-id');
      expect(component.dynamics365ClientId).toBe('client-id');
      expect(component.dynamics365ClientSecret).toBe('client-secret');
      expect(component.entityName).toBe('accounts');
    });

    it('should use default values when data is missing', () => {
      component.initializeSettings({});

      expect(component.targetSystemId).toBe('Dynamics365');
    });
  });

  describe('getSettings', () => {
    it('should return Dynamics 365 settings', () => {
      component.dynamics365InstanceUrl = 'https://org.crm.dynamics.com';
      component.dynamics365TenantId = 'tenant-id';
      component.dynamics365ClientId = 'client-id';
      component.dynamics365ClientSecret = 'client-secret';
      component.entityName = 'accounts';

      const settings = component.getSettings();

      expect(settings.dynamics365InstanceUrl).toBe('https://org.crm.dynamics.com');
      expect(settings.dynamics365TenantId).toBe('tenant-id');
      expect(settings.dynamics365ClientId).toBe('client-id');
      expect(settings.dynamics365ClientSecret).toBe('client-secret');
      expect(settings.entityName).toBe('accounts');
    });
  });

  describe('validateSettings', () => {
    it('should validate instance name', () => {
      component.instanceName = '';
      const result = component.validateSettings();
      expect(result.valid).toBe(false);
    });
  });
});
