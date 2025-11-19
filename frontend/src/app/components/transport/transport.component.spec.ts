import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { TransportComponent } from './transport.component';
import { TransportService } from '../../services/transport.service';
import { provideHttpClient } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';
import { SqlRecord, ProcessLog } from '../../models/data.model';

describe('TransportComponent', () => {
  let component: TransportComponent;
  let fixture: ComponentFixture<TransportComponent>;
  let transportService: jasmine.SpyObj<TransportService>;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  const mockSqlData: SqlRecord[] = [
    { id: 1, name: 'Test User', email: 'test@test.com', age: 30, city: 'Berlin', salary: 50000, createdAt: '2024-01-01' }
  ];

  const mockLogs: ProcessLog[] = [
    { id: 1, timestamp: '2024-01-01T00:00:00Z', level: 'info', message: 'Test log' }
  ];

  const mockInterfaceConfigs = [
    {
      interfaceName: 'FromCsvToSqlServerExample',
      sourceAdapterName: 'CSV',
      destinationAdapterName: 'SqlServer',
      sourceInstanceName: 'Source',
      destinationInstanceName: 'Destination',
      sourceIsEnabled: true,
      destinationIsEnabled: true,
      csvData: 'Id║Name\n1║Test User',
      csvPollingInterval: 10
    }
  ];

  beforeEach(async () => {
    const transportServiceSpy = jasmine.createSpyObj('TransportService', [
      'getSqlData',
      'getProcessLogs',
      'startTransport',
      'getInterfaceConfigurations',
      'createInterfaceConfiguration',
      'getDestinationAdapterInstances',
      'getInterfaceConfiguration',
      'updateCsvData',
      'updateCsvPollingInterval',
      'addDestinationAdapterInstance'
    ]);
    const snackBarSpy = jasmine.createSpyObj('MatSnackBar', ['open']);

    await TestBed.configureTestingModule({
      imports: [TransportComponent],
      providers: [
        provideHttpClient(),
        provideAnimations(),
        { provide: TransportService, useValue: transportServiceSpy },
        { provide: MatSnackBar, useValue: snackBarSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TransportComponent);
    component = fixture.componentInstance;
    transportService = TestBed.inject(TransportService) as jasmine.SpyObj<TransportService>;
    snackBar = TestBed.inject(MatSnackBar) as jasmine.SpyObj<MatSnackBar>;

    transportService.getSqlData.and.returnValue(of(mockSqlData));
    transportService.getProcessLogs.and.returnValue(of(mockLogs));
    transportService.getInterfaceConfigurations.and.returnValue(of(mockInterfaceConfigs));
    transportService.createInterfaceConfiguration.and.returnValue(of(mockInterfaceConfigs[0]));
    transportService.getDestinationAdapterInstances.and.returnValue(of([]));
    transportService.getInterfaceConfiguration.and.returnValue(of(mockInterfaceConfigs[0]));
    transportService.updateCsvData.and.returnValue(of({}));
    transportService.updateCsvPollingInterval.and.returnValue(of({}));
    transportService.addDestinationAdapterInstance.and.returnValue(of({}));

    component['refreshSubscription']?.unsubscribe();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load SQL data on init', () => {
    fixture.detectChanges();
    expect(transportService.getSqlData).toHaveBeenCalled();
    expect(component.sqlData).toEqual(mockSqlData);
  });

  it('should load process logs on init', () => {
    fixture.detectChanges();
    expect(transportService.getProcessLogs).toHaveBeenCalled();
    // Logs are enriched with component property, so we check for length
    expect(component.processLogs.length).toBeGreaterThanOrEqual(mockLogs.length);
  });

  it('should start transport', () => {
    transportService.startTransport.and.returnValue(of({ message: 'Success', fileId: 'test-id' }));
    component.startTransport();
    expect(transportService.startTransport).toHaveBeenCalledWith('FromCsvToSqlServerExample', jasmine.any(String));
  });

  it('should handle transport error', fakeAsync(() => {
    transportService.startTransport.and.returnValue(throwError(() => new Error('Test error')));
    expect(component.isTransporting).toBe(false);
    component.startTransport();
    tick(); // Process async operations
    expect(component.isTransporting).toBe(false); // Should be false after error handling
    expect(snackBar.open).toHaveBeenCalled();
  }));

  // clearTable test removed - method was removed, table clearing is now handled by dropTable

  it('should get level color correctly', () => {
    expect(component.getLevelColor('error')).toBe('warn');
    expect(component.getLevelColor('warning')).toBe('accent');
    expect(component.getLevelColor('info')).toBe('primary');
  });

  it('should extract component from log message', () => {
    expect(component.extractComponent('Azure Function triggered', '')).toBe('Azure Function');
    expect(component.extractComponent('CSV file detected in blob storage', '')).toBe('Blob Storage');
    expect(component.extractComponent('Database connection established', '')).toBe('SQL Server');
    expect(component.extractComponent('Vercel API called', '')).toBe('Vercel API');
    expect(component.extractComponent('Unknown message', '')).toBe('Unknown');
  });

  it('should filter logs by component', () => {
    const logsWithComponents = [
      { ...mockLogs[0], component: 'Azure Function' },
      { ...mockLogs[0], component: 'Blob Storage', id: 2 }
    ];
    component.processLogs = logsWithComponents as any;
    component.selectedComponent = 'Azure Function';
    component.updateLogDataSource();
    expect(component.logDataSource.data.length).toBe(1);
    expect(component.logDataSource.data[0].component).toBe('Azure Function');
  });

  it('should show all logs when filter is set to all', () => {
    const logsWithComponents = [
      { ...mockLogs[0], component: 'Azure Function' },
      { ...mockLogs[0], component: 'Blob Storage', id: 2 }
    ];
    component.processLogs = logsWithComponents as any;
    component.selectedComponent = 'all';
    component.updateLogDataSource();
    expect(component.logDataSource.data.length).toBe(2);
  });

  it('should update data source when component filter changes', () => {
    component.processLogs = mockLogs as any;
    component.selectedComponent = 'Azure Function';
    component.onComponentFilterChange();
    expect(component.logDataSource.data).toBeDefined();
  });
});

