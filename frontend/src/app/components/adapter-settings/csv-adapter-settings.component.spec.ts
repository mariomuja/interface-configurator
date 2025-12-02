import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CsvAdapterSettingsComponent } from './csv-adapter-settings.component';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('CsvAdapterSettingsComponent', () => {
  let component: CsvAdapterSettingsComponent;
  let fixture: ComponentFixture<CsvAdapterSettingsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [
        CsvAdapterSettingsComponent,
        NoopAnimationsModule
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CsvAdapterSettingsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('initializeSettings', () => {
    it('should initialize CSV settings from data', () => {
      const data = {
        receiveFolder: '/test/folder',
        fileMask: '*.csv',
        batchSize: 500,
        fieldSeparator: ',',
        csvAdapterType: 'FILE',
        csvPollingInterval: 30
      };

      component.initializeSettings(data);

      expect(component.receiveFolder).toBe('/test/folder');
      expect(component.fileMask).toBe('*.csv');
      expect(component.batchSize).toBe(500);
      expect(component.fieldSeparator).toBe(',');
      expect(component.csvAdapterType).toBe('FILE');
      expect(component.csvPollingInterval).toBe(30);
    });

    it('should use default values when data is missing', () => {
      component.initializeSettings({});

      expect(component.fileMask).toBe('*.txt');
      expect(component.batchSize).toBe(1000);
      expect(component.fieldSeparator).toBe('║');
      expect(component.csvPollingInterval).toBe(10);
    });

    it('should initialize SFTP settings', () => {
      const data = {
        sftpHost: 'sftp.example.com',
        sftpPort: 2222,
        sftpUsername: 'user',
        sftpPassword: 'pass',
        sftpFolder: '/data'
      };

      component.initializeSettings(data);

      expect(component.sftpHost).toBe('sftp.example.com');
      expect(component.sftpPort).toBe(2222);
      expect(component.sftpUsername).toBe('user');
      expect(component.sftpPassword).toBe('pass');
      expect(component.sftpFolder).toBe('/data');
    });
  });

  describe('getSettings', () => {
    it('should return CSV settings for Source adapter', () => {
      component.adapterType = 'Source';
      component.receiveFolder = '/test';
      component.fileMask = '*.csv';
      component.batchSize = 500;
      component.fieldSeparator = ',';
      component.csvAdapterType = 'FILE';
      component.csvPollingInterval = 30;

      const settings = component.getSettings();

      expect(settings.receiveFolder).toBe('/test');
      expect(settings.fileMask).toBe('*.csv');
      expect(settings.batchSize).toBe(500);
      expect(settings.fieldSeparator).toBe(',');
      expect(settings.csvAdapterType).toBe('FILE');
      expect(settings.csvPollingInterval).toBe(30);
    });

    it('should return CSV settings for Destination adapter', () => {
      component.adapterType = 'Destination';
      component.destinationReceiveFolder = '/out';
      component.destinationFileMask = '*.txt';
      component.batchSize = 1000;

      const settings = component.getSettings();

      expect(settings.destinationReceiveFolder).toBe('/out');
      expect(settings.destinationFileMask).toBe('*.txt');
      expect(settings.batchSize).toBe(1000);
      expect(settings.csvPollingInterval).toBeUndefined();
    });

    it('should trim string values', () => {
      component.receiveFolder = '  /test/folder  ';
      component.fileMask = '  *.csv  ';

      const settings = component.getSettings();

      expect(settings.receiveFolder).toBe('/test/folder');
      expect(settings.fileMask).toBe('*.csv');
    });
  });

  describe('validateSettings', () => {
    it('should validate instance name', () => {
      component.instanceName = '';
      const result = component.validateSettings();
      expect(result.valid).toBe(false);
    });

    it('should pass validation with valid instance name', () => {
      component.instanceName = 'TestInstance';
      const result = component.validateSettings();
      expect(result.valid).toBe(true);
    });
  });
});
