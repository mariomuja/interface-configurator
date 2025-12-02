import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SqlServerAdapterSettingsComponent } from './sql-server-adapter-settings.component';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('SqlServerAdapterSettingsComponent', () => {
  let component: SqlServerAdapterSettingsComponent;
  let fixture: ComponentFixture<SqlServerAdapterSettingsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [
        SqlServerAdapterSettingsComponent,
        NoopAnimationsModule
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SqlServerAdapterSettingsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('initializeSettings', () => {
    it('should initialize SQL Server settings from data', () => {
      const data = {
        sqlServerName: 'localhost',
        sqlDatabaseName: 'TestDB',
        sqlUserName: 'sa',
        sqlPassword: 'password',
        sqlIntegratedSecurity: true,
        sqlPollingInterval: 120,
        sqlBatchSize: 500,
        tableName: 'TestTable'
      };

      component.initializeSettings(data);

      expect(component.sqlServerName).toBe('localhost');
      expect(component.sqlDatabaseName).toBe('TestDB');
      expect(component.sqlUserName).toBe('sa');
      expect(component.sqlPassword).toBe('password');
      expect(component.sqlIntegratedSecurity).toBe(true);
      expect(component.sqlPollingInterval).toBe(120);
      expect(component.sqlBatchSize).toBe(500);
      expect(component.tableName).toBe('TestTable');
    });

    it('should use default values when data is missing', () => {
      component.initializeSettings({});

      expect(component.sqlPollingInterval).toBe(60);
      expect(component.sqlBatchSize).toBe(1000);
      expect(component.tableName).toBe('TransportData');
      expect(component.sqlIntegratedSecurity).toBe(false);
    });
  });

  describe('getSettings', () => {
    it('should return SQL Server settings for Source adapter', () => {
      component.adapterType = 'Source';
      component.sqlServerName = 'localhost';
      component.sqlDatabaseName = 'TestDB';
      component.sqlPollingStatement = 'SELECT * FROM Test';
      component.sqlPollingInterval = 120;

      const settings = component.getSettings();

      expect(settings.sqlServerName).toBe('localhost');
      expect(settings.sqlDatabaseName).toBe('TestDB');
      expect(settings.sqlPollingStatement).toBe('SELECT * FROM Test');
      expect(settings.sqlPollingInterval).toBe(120);
    });

    it('should return SQL Server settings for Destination adapter', () => {
      component.adapterType = 'Destination';
      component.sqlServerName = 'localhost';
      component.sqlDatabaseName = 'TestDB';
      component.tableName = 'TargetTable';

      const settings = component.getSettings();

      expect(settings.sqlServerName).toBe('localhost');
      expect(settings.sqlDatabaseName).toBe('TestDB');
      expect(settings.tableName).toBe('TargetTable');
      expect(settings.sqlPollingStatement).toBeUndefined();
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
