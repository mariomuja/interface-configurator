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
      'addDestinationAdapterInstance',
      'getMessageBoxMessages',
      'getBlobContainerFolders',
      'deleteBlobFile'
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
    transportService.getMessageBoxMessages.and.returnValue(of([]));
    transportService.startTransport.and.returnValue(of({ message: 'Auto start', fileId: 'auto' }));
    transportService.getBlobContainerFolders.and.returnValue(of([]));
    transportService.deleteBlobFile.and.returnValue(of({ success: true, message: 'Deleted' }));

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

  it('should auto start CSV source when enabled', () => {
    fixture.detectChanges();
    expect(transportService.startTransport).toHaveBeenCalled();
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

  describe('Blob Container Explorer', () => {
    const mockBlobFolders = [
      {
        path: '/csv-incoming',
        files: [
          {
            name: 'transport-2025_11_21_14_30_45_123.csv',
            fullPath: 'csv-incoming/transport-2025_11_21_14_30_45_123.csv',
            size: 1000,
            lastModified: '2025-11-21T14:30:45Z',
            contentType: 'text/csv'
          },
          {
            name: 'transport-2025_11_21_14_31_00_456.csv',
            fullPath: 'csv-incoming/transport-2025_11_21_14_31_00_456.csv',
            size: 2000,
            lastModified: '2025-11-21T14:31:00Z',
            contentType: 'text/csv'
          }
        ]
      }
    ];

    beforeEach(() => {
      transportService.getBlobContainerFolders.and.returnValue(of(mockBlobFolders));
      transportService.deleteBlobFile.and.returnValue(of({ success: true, message: 'Deleted' }));
    });

    it('should load blob container folders on init', () => {
      fixture.detectChanges();
      expect(transportService.getBlobContainerFolders).toHaveBeenCalledWith('csv-files', '');
    });

    it('should format file size correctly', () => {
      expect(component.formatFileSize(0)).toBe('0 B');
      expect(component.formatFileSize(1024)).toBe('1 KB');
      expect(component.formatFileSize(1048576)).toBe('1 MB');
      expect(component.formatFileSize(1536)).toContain('KB');
    });

    it('should get total file count', () => {
      component.blobContainerFolders = mockBlobFolders;
      expect(component.getTotalFileCount()).toBe(2);
    });

    it('should toggle file selection', () => {
      const filePath = 'csv-incoming/test.csv';
      expect(component.isBlobFileSelected(filePath)).toBe(false);
      component.toggleBlobFileSelection(filePath);
      expect(component.isBlobFileSelected(filePath)).toBe(true);
      component.toggleBlobFileSelection(filePath);
      expect(component.isBlobFileSelected(filePath)).toBe(false);
    });

    it('should get selected files count', () => {
      component.selectedBlobFiles.clear();
      expect(component.getSelectedBlobFilesCount()).toBe(0);
      component.selectedBlobFiles.add('file1.csv');
      component.selectedBlobFiles.add('file2.csv');
      expect(component.getSelectedBlobFilesCount()).toBe(2);
    });

    it('should check if all files in folder are selected', () => {
      component.blobContainerFolders = mockBlobFolders;
      const folder = mockBlobFolders[0];
      
      component.selectedBlobFiles.clear();
      expect(component.areAllFilesInFolderSelected(folder)).toBe(false);
      
      folder.files.forEach((file: any) => {
        component.selectedBlobFiles.add(file.fullPath);
      });
      expect(component.areAllFilesInFolderSelected(folder)).toBe(true);
    });

    it('should check if some files in folder are selected', () => {
      component.blobContainerFolders = mockBlobFolders;
      const folder = mockBlobFolders[0];
      
      component.selectedBlobFiles.clear();
      expect(component.areSomeFilesInFolderSelected(folder)).toBe(false);
      
      component.selectedBlobFiles.add(folder.files[0].fullPath);
      expect(component.areSomeFilesInFolderSelected(folder)).toBe(true);
    });

    it('should toggle folder selection', () => {
      component.blobContainerFolders = mockBlobFolders;
      const folder = mockBlobFolders[0];
      
      component.selectedBlobFiles.clear();
      component.toggleFolderSelection(folder);
      expect(component.selectedBlobFiles.size).toBe(folder.files.length);
      
      component.toggleFolderSelection(folder);
      expect(component.selectedBlobFiles.size).toBe(0);
    });

    it('should delete selected files', fakeAsync(() => {
      spyOn(window, 'confirm').and.returnValue(true);
      component.selectedBlobFiles.add('csv-incoming/file1.csv');
      component.selectedBlobFiles.add('csv-incoming/file2.csv');
      
      component.deleteSelectedBlobFiles();
      tick();
      
      expect(transportService.deleteBlobFile).toHaveBeenCalledTimes(2);
      expect(component.selectedBlobFiles.size).toBe(0);
    }));

    it('should not delete files if confirmation is cancelled', () => {
      spyOn(window, 'confirm').and.returnValue(false);
      component.selectedBlobFiles.add('csv-incoming/file1.csv');
      
      component.deleteSelectedBlobFiles();
      
      expect(transportService.deleteBlobFile).not.toHaveBeenCalled();
      expect(component.selectedBlobFiles.size).toBe(1);
    });

    it('should sort blob container folders by date descending by default', () => {
      const folders = [
        {
          path: '/csv-incoming',
          files: [
            { name: 'old.csv', fullPath: 'csv-incoming/old.csv', size: 100, lastModified: '2025-11-21T10:00:00Z' },
            { name: 'new.csv', fullPath: 'csv-incoming/new.csv', size: 200, lastModified: '2025-11-21T14:00:00Z' }
          ]
        }
      ];
      
      component.blobContainerSortBy = 'date';
      component.blobContainerSortOrder = 'desc';
      const sorted = component.sortBlobContainerFolders(folders);
      
      expect(sorted[0].files[0].name).toBe('new.csv');
      expect(sorted[0].files[1].name).toBe('old.csv');
    });

    it('should sort blob container folders by name', () => {
      const folders = [
        {
          path: '/csv-incoming',
          files: [
            { name: 'zebra.csv', fullPath: 'csv-incoming/zebra.csv', size: 100, lastModified: '2025-11-21T14:00:00Z' },
            { name: 'alpha.csv', fullPath: 'csv-incoming/alpha.csv', size: 200, lastModified: '2025-11-21T10:00:00Z' }
          ]
        }
      ];
      
      component.blobContainerSortBy = 'name';
      component.blobContainerSortOrder = 'asc';
      const sorted = component.sortBlobContainerFolders(folders);
      
      expect(sorted[0].files[0].name).toBe('alpha.csv');
      expect(sorted[0].files[1].name).toBe('zebra.csv');
    });
  });
});

