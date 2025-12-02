import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { SapAdapterSettingsComponent } from './sap-adapter-settings.component';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('SapAdapterSettingsComponent', () => {
  let component: SapAdapterSettingsComponent;
  let fixture: ComponentFixture<SapAdapterSettingsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [
        SapAdapterSettingsComponent,
        HttpClientTestingModule,
        NoopAnimationsModule
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SapAdapterSettingsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('initializeSettings', () => {
    it('should initialize SAP settings from data', () => {
      const data = {
        sapApplicationServer: 'sap-server.example.com',
        sapSystemNumber: '00',
        sapClient: '100',
        sapUsername: 'sapuser',
        sapPassword: 'sappass',
        sapLanguage: 'DE'
      };

      component.initializeSettings(data);

      expect(component.sapApplicationServer).toBe('sap-server.example.com');
      expect(component.sapSystemNumber).toBe('00');
      expect(component.sapClient).toBe('100');
      expect(component.sapUsername).toBe('sapuser');
      expect(component.sapPassword).toBe('sappass');
      expect(component.sapLanguage).toBe('DE');
    });

    it('should use default values when data is missing', () => {
      component.initializeSettings({});

      expect(component.sapLanguage).toBe('EN');
      expect(component.targetSystemId).toBe('SAP');
    });
  });

  describe('getSettings', () => {
    it('should return SAP settings', () => {
      component.sapApplicationServer = 'sap-server.example.com';
      component.sapSystemNumber = '00';
      component.sapClient = '100';
      component.sapUsername = 'sapuser';
      component.sapPassword = 'sappass';

      const settings = component.getSettings();

      expect(settings.sapApplicationServer).toBe('sap-server.example.com');
      expect(settings.sapSystemNumber).toBe('00');
      expect(settings.sapClient).toBe('100');
      expect(settings.sapUsername).toBe('sapuser');
      expect(settings.sapPassword).toBe('sappass');
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
