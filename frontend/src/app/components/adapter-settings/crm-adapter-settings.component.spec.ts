import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { CrmAdapterSettingsComponent } from './crm-adapter-settings.component';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('CrmAdapterSettingsComponent', () => {
  let component: CrmAdapterSettingsComponent;
  let fixture: ComponentFixture<CrmAdapterSettingsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [
        CrmAdapterSettingsComponent,
        HttpClientTestingModule,
        NoopAnimationsModule
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CrmAdapterSettingsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('initializeSettings', () => {
    it('should initialize CRM settings from data', () => {
      const data = {
        crmOrganizationUrl: 'https://org.crm.dynamics.com',
        crmUsername: 'crmuser',
        crmPassword: 'crmpass',
        entityName: 'contacts'
      };

      component.initializeSettings(data);

      expect(component.crmOrganizationUrl).toBe('https://org.crm.dynamics.com');
      expect(component.crmUsername).toBe('crmuser');
      expect(component.crmPassword).toBe('crmpass');
      expect(component.entityName).toBe('contacts');
    });

    it('should use default values when data is missing', () => {
      component.initializeSettings({});

      expect(component.targetSystemId).toBe('CRM');
    });
  });

  describe('getSettings', () => {
    it('should return CRM settings', () => {
      component.crmOrganizationUrl = 'https://org.crm.dynamics.com';
      component.crmUsername = 'crmuser';
      component.crmPassword = 'crmpass';
      component.entityName = 'contacts';

      const settings = component.getSettings();

      expect(settings.crmOrganizationUrl).toBe('https://org.crm.dynamics.com');
      expect(settings.crmUsername).toBe('crmuser');
      expect(settings.crmPassword).toBe('crmpass');
      expect(settings.entityName).toBe('contacts');
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
