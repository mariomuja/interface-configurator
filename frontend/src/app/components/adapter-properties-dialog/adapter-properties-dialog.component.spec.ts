import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AdapterPropertiesDialogComponent, AdapterPropertiesData } from './adapter-properties-dialog.component';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { provideAnimations } from '@angular/platform-browser/animations';

describe('AdapterPropertiesDialogComponent', () => {
  let component: AdapterPropertiesDialogComponent;
  let fixture: ComponentFixture<AdapterPropertiesDialogComponent>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<AdapterPropertiesDialogComponent>>;

  const createMockData = (overrides: Partial<AdapterPropertiesData> = {}): AdapterPropertiesData => ({
    adapterType: 'Source',
    adapterName: 'CSV',
    instanceName: 'TestInstance',
    isEnabled: true,
    csvAdapterType: 'RAW',
    csvPollingInterval: 10,
    adapterInstanceGuid: 'test-guid',
    ...overrides
  });

  beforeEach(async () => {
    const dialogRefSpy = jasmine.createSpyObj('MatDialogRef', ['close']);

    await TestBed.configureTestingModule({
      imports: [AdapterPropertiesDialogComponent],
      providers: [
        provideAnimations(),
        { provide: MatDialogRef, useValue: dialogRefSpy },
        { provide: MAT_DIALOG_DATA, useValue: createMockData() }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdapterPropertiesDialogComponent);
    component = fixture.componentInstance;
    dialogRef = TestBed.inject(MatDialogRef) as jasmine.SpyObj<MatDialogRef<AdapterPropertiesDialogComponent>>;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('ngOnInit', () => {
    it('should initialize with provided data', () => {
      const mockData = createMockData({
        instanceName: 'CustomInstance',
        isEnabled: false,
        receiveFolder: '/custom/folder'
      });
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        imports: [AdapterPropertiesDialogComponent],
        providers: [
          provideAnimations(),
          { provide: MatDialogRef, useValue: dialogRef },
          { provide: MAT_DIALOG_DATA, useValue: mockData }
        ]
      });
      fixture = TestBed.createComponent(AdapterPropertiesDialogComponent);
      component = fixture.componentInstance;
      component.ngOnInit();

      expect(component.instanceName).toBe('CustomInstance');
      expect(component.isEnabled).toBe(false);
      expect(component.receiveFolder).toBe('/custom/folder');
    });

    it('should use default values when data is missing', () => {
      const mockData = createMockData({
        instanceName: '',
        isEnabled: undefined,
        receiveFolder: undefined
      });
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        imports: [AdapterPropertiesDialogComponent],
        providers: [
          provideAnimations(),
          { provide: MatDialogRef, useValue: dialogRef },
          { provide: MAT_DIALOG_DATA, useValue: mockData }
        ]
      });
      fixture = TestBed.createComponent(AdapterPropertiesDialogComponent);
      component = fixture.componentInstance;
      component.ngOnInit();

      expect(component.instanceName).toBe('');
      expect(component.isEnabled).toBe(true);
      expect(component.receiveFolder).toBe('');
    });
  });

  describe('onCancel', () => {
    it('should close dialog without data', () => {
      component.onCancel();
      expect(dialogRef.close).toHaveBeenCalled();
    });
  });

  describe('onSave', () => {
    beforeEach(() => {
      component.ngOnInit();
    });

    it('should close dialog with saved data for CSV Source adapter', () => {
      component.instanceName = 'TestInstance';
      component.isEnabled = true;
      component.receiveFolder = '/test/folder';
      component.fileMask = '*.csv';
      component.batchSize = 200;
      component.fieldSeparator = '|';

      component.onSave();

      expect(dialogRef.close).toHaveBeenCalledWith(jasmine.objectContaining({
        instanceName: 'TestInstance',
        isEnabled: true,
        receiveFolder: '/test/folder',
        fileMask: '*.csv',
        batchSize: 200,
        fieldSeparator: '|'
      }));
    });

    it('should trim instance name and use default if empty', () => {
      component.instanceName = '   ';
      component.data.adapterType = 'Source';
      component.onSave();

      expect(dialogRef.close).toHaveBeenCalledWith(jasmine.objectContaining({
        instanceName: 'Source'
      }));
    });

    it('should use default instance name for Destination adapter', () => {
      component.instanceName = '';
      component.data.adapterType = 'Destination';
      component.onSave();

      expect(dialogRef.close).toHaveBeenCalledWith(jasmine.objectContaining({
        instanceName: 'Destination'
      }));
    });

    it('should handle SqlServer adapter properties', () => {
      const sqlData = createMockData({
        adapterName: 'SqlServer',
        sqlServerName: 'test-server',
        sqlDatabaseName: 'test-db',
        sqlUserName: 'test-user',
        sqlPassword: 'test-password'
      });
      component.data = sqlData;
      component.ngOnInit();
      component.sqlServerName = 'test-server';
      component.sqlDatabaseName = 'test-db';
      component.sqlUserName = 'test-user';
      component.sqlPassword = 'test-password';

      component.onSave();

      expect(dialogRef.close).toHaveBeenCalledWith(jasmine.objectContaining({
        sqlServerName: 'test-server',
        sqlDatabaseName: 'test-db',
        sqlUserName: 'test-user',
        sqlPassword: 'test-password'
      }));
    });

    it('should include SFTP properties when csvAdapterType is SFTP', () => {
      const mockData = createMockData({ adapterName: 'CSV', adapterType: 'Source', csvAdapterType: 'SFTP' });
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        imports: [AdapterPropertiesDialogComponent],
        providers: [
          provideAnimations(),
          { provide: MatDialogRef, useValue: dialogRef },
          { provide: MAT_DIALOG_DATA, useValue: mockData }
        ]
      });
      fixture = TestBed.createComponent(AdapterPropertiesDialogComponent);
      component = fixture.componentInstance;
      component.sftpHost = 'sftp.example.com';
      component.sftpPort = 2222;
      component.sftpUsername = 'sftp-user';
      component.ngOnInit();

      component.onSave();

      expect(dialogRef.close).toHaveBeenCalledWith(jasmine.objectContaining({
        csvAdapterType: 'SFTP',
        sftpHost: 'sftp.example.com',
        sftpPort: 2222,
        sftpUsername: 'sftp-user'
      }));
    });

    it('should include CSV polling interval when provided', () => {
      component.csvPollingInterval = 45;
      component.ngOnInit();
      component.onSave();

      expect(dialogRef.close).toHaveBeenCalledWith(jasmine.objectContaining({
        csvPollingInterval: 45
      }));
    });
  });

  describe('updateConnectionString', () => {
    beforeEach(() => {
      component.data = createMockData({ adapterName: 'SqlServer' });
      component.ngOnInit();
    });

    it('should build connection string for Azure SQL', () => {
      component.sqlServerName = 'test-server.database.windows.net';
      component.sqlDatabaseName = 'test-db';
      component.sqlUserName = 'test-user';
      component.sqlPassword = 'test-password';
      component.sqlIntegratedSecurity = false;

      component.updateConnectionString();

      expect(component.connectionString).toContain('Server=tcp:test-server.database.windows.net,1433');
      expect(component.connectionString).toContain('Initial Catalog=test-db');
      expect(component.connectionString).toContain('User ID=test-user');
      expect(component.connectionString).toContain('Password=test-password');
      expect(component.connectionString).toContain('Encrypt=True');
    });

    it('should build connection string for local SQL Server', () => {
      component.sqlServerName = 'localhost';
      component.sqlDatabaseName = 'test-db';
      component.sqlIntegratedSecurity = true;

      component.updateConnectionString();

      expect(component.connectionString).toContain('Server=localhost,1433');
      expect(component.connectionString).toContain('Initial Catalog=test-db');
      expect(component.connectionString).toContain('Integrated Security=True');
      expect(component.connectionString).toContain('Encrypt=False');
    });

    it('should clear connection string when not showing SQL Server properties', () => {
      component.data = createMockData({ adapterName: 'CSV' });
      component.ngOnInit();
      component.updateConnectionString();
      expect(component.connectionString).toBe('');
    });
  });

  describe('copyConnectionString', () => {
    beforeEach(() => {
      component.data = createMockData({ adapterName: 'SqlServer' });
      component.ngOnInit();
    });

    it('should copy connection string to clipboard', async () => {
      const clipboardSpy = spyOn(navigator.clipboard, 'writeText').and.returnValue(Promise.resolve());
      const alertSpy = spyOn(window, 'alert');
      component.connectionString = 'Server=test;Database=test;';

      component.copyConnectionString();

      await fixture.whenStable();
      expect(clipboardSpy).toHaveBeenCalledWith('Server=test;Database=test;');
      expect(alertSpy).toHaveBeenCalledWith('Connection string copied to clipboard!');
    });

    it('should handle clipboard error', async () => {
      const error = new Error('Clipboard error');
      const clipboardSpy = spyOn(navigator.clipboard, 'writeText').and.returnValue(Promise.reject(error));
      const alertSpy = spyOn(window, 'alert');
      const consoleErrorSpy = spyOn(console, 'error');
      component.connectionString = 'Server=test;';

      component.copyConnectionString();

      // Wait for the promise to reject
      await new Promise(resolve => setTimeout(resolve, 10));
      
      expect(clipboardSpy).toHaveBeenCalled();
      expect(consoleErrorSpy).toHaveBeenCalledWith('Failed to copy connection string:', error);
      expect(alertSpy).toHaveBeenCalledWith('Failed to copy connection string');
    });
  });

  describe('getters', () => {
    it('should show receive folder for CSV Source FILE adapter', () => {
      component.data = createMockData({ adapterName: 'CSV', adapterType: 'Source', csvAdapterType: 'FILE' });
      component.ngOnInit();
      expect(component.showReceiveFolder).toBe(true);
    });

    it('should not show receive folder for CSV Source SFTP adapter', () => {
      const mockData = createMockData({ adapterName: 'CSV', adapterType: 'Source', csvAdapterType: 'SFTP' });
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        imports: [AdapterPropertiesDialogComponent],
        providers: [
          provideAnimations(),
          { provide: MatDialogRef, useValue: dialogRef },
          { provide: MAT_DIALOG_DATA, useValue: mockData }
        ]
      });
      fixture = TestBed.createComponent(AdapterPropertiesDialogComponent);
      component = fixture.componentInstance;
      component.csvAdapterType = 'SFTP';
      component.ngOnInit();
      expect(component.showReceiveFolder).toBe(false);
    });

    it('should show file mask for CSV Source FILE adapter', () => {
      component.data = createMockData({ adapterName: 'CSV', adapterType: 'Source' });
      component.ngOnInit();
      expect(component.showFileMask).toBe(true);
    });

    it('should show batch size for CSV adapter', () => {
      component.data = createMockData({ adapterName: 'CSV' });
      component.ngOnInit();
      expect(component.showBatchSize).toBe(true);
    });

    it('should show field separator for CSV adapter', () => {
      component.data = createMockData({ adapterName: 'CSV' });
      component.ngOnInit();
      expect(component.showFieldSeparator).toBe(true);
    });

    it('should show destination properties for CSV Destination adapter', () => {
      component.data = createMockData({ adapterName: 'CSV', adapterType: 'Destination' });
      component.ngOnInit();
      expect(component.showDestinationProperties).toBe(true);
    });

    it('should show SQL Server properties for SqlServer adapter', () => {
      component.data = createMockData({ adapterName: 'SqlServer' });
      component.ngOnInit();
      expect(component.showSqlServerProperties).toBe(true);
    });

    it('should show SQL polling properties for SqlServer Source adapter', () => {
      component.data = createMockData({ adapterName: 'SqlServer', adapterType: 'Source' });
      component.ngOnInit();
      expect(component.showSqlPollingProperties).toBe(true);
    });

    it('should not show SQL polling properties for SqlServer Destination adapter', () => {
      component.data = createMockData({ adapterName: 'SqlServer', adapterType: 'Destination' });
      component.ngOnInit();
      expect(component.showSqlPollingProperties).toBe(false);
    });

    it('should show SFTP properties for CSV Source adapter', () => {
      component.data = createMockData({ adapterName: 'CSV', adapterType: 'Source', csvAdapterType: 'SFTP' });
      component.ngOnInit();
      expect(component.showSftpProperties).toBe(true);
    });

    it('should not show SFTP properties for CSV Destination adapter', () => {
      component.data = createMockData({ adapterName: 'CSV', adapterType: 'Destination' });
      component.ngOnInit();
      expect(component.showSftpProperties).toBe(false);
    });

    it('should show file properties for CSV Source FILE adapter', () => {
      component.data = createMockData({ adapterName: 'CSV', adapterType: 'Source', csvAdapterType: 'FILE' });
      component.ngOnInit();
      expect(component.showFileProperties).toBe(true);
    });

    it('should return correct dialog title', () => {
      component.data = createMockData({ adapterType: 'Source', adapterName: 'CSV' });
      component.ngOnInit();
      expect(component.dialogTitle).toBe('Source Adapter Properties (CSV)');
    });
  });

  describe('onSqlPropertyChange', () => {
    it('should update connection string when SQL property changes', () => {
      component.data = createMockData({ adapterName: 'SqlServer' });
      component.ngOnInit();
      spyOn(component, 'updateConnectionString');

      component.onSqlPropertyChange();

      expect(component.updateConnectionString).toHaveBeenCalled();
    });
  });
});

