import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController, HttpErrorResponse } from '@angular/common/http/testing';
import { TransportService } from './transport.service';
import { CsvRecord, SqlRecord, ProcessLog } from '../models/data.model';

describe('TransportService', () => {
  let service: TransportService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [TransportService]
    });
    service = TestBed.inject(TransportService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('Blob Container Operations', () => {
    it('should get blob container folders', () => {
      const mockFolders = [
        {
          path: '/csv-incoming',
          files: [
            { name: 'transport-2025_11_21_14_30_45_123.csv', fullPath: 'csv-incoming/transport-2025_11_21_14_30_45_123.csv', size: 1000, lastModified: '2025-11-21T14:30:45Z', contentType: 'text/csv' }
          ]
        }
      ];

      service.getBlobContainerFolders('csv-files', '').subscribe(data => {
        expect(data).toEqual(mockFolders);
      });

      const req = httpMock.expectOne((request) => 
        request.url.includes('/GetBlobContainerFolders') && 
        request.params.get('containerName') === 'csv-files'
      );
      expect(req.request.method).toBe('GET');
      req.flush(mockFolders);
    });

    it('should get blob container folders with maxFiles parameter', () => {
      service.getBlobContainerFolders('csv-files', '', 20).subscribe();
      
      const req = httpMock.expectOne((request) => 
        request.url.includes('/GetBlobContainerFolders')
      );
      expect(req.request.params.get('maxFiles')).toBe('20');
      req.flush([]);
    });

    it('should delete blob file', () => {
      const mockResponse = { success: true, message: 'Blob deleted successfully' };

      service.deleteBlobFile('csv-files', 'csv-incoming/test.csv').subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne((request) => 
        request.url.includes('/DeleteBlobFile') &&
        request.params.get('containerName') === 'csv-files' &&
        request.params.get('blobPath') === 'csv-incoming/test.csv'
      );
      expect(req.request.method).toBe('DELETE');
      req.flush(mockResponse);
    });
  });

  describe('Data Operations', () => {
    it('should get sample CSV data', () => {
      const mockData: CsvRecord[] = [
        { id: 1, name: 'Test', email: 'test@test.com', age: 30, city: 'Berlin', salary: 50000 }
      ];

      service.getSampleCsvData().subscribe(data => {
        expect(data).toEqual(mockData);
      });

      const req = httpMock.expectOne('/api/sample-csv');
      expect(req.request.method).toBe('GET');
      req.flush(mockData);
    });

    it('should get SQL data', () => {
      const mockData: SqlRecord[] = [
        { id: 1, name: 'Test', email: 'test@test.com', age: 30, city: 'Berlin', salary: 50000, createdAt: '2024-01-01' }
      ];

      service.getSqlData().subscribe(data => {
        expect(data).toEqual(mockData);
      });

      const req = httpMock.expectOne('/api/sql-data');
      expect(req.request.method).toBe('GET');
      req.flush(mockData);
    });

    it('should get process logs', () => {
      const mockLogs: ProcessLog[] = [
        { id: 1, timestamp: '2024-01-01T00:00:00Z', level: 'info', message: 'Test log' }
      ];

      service.getProcessLogs().subscribe(logs => {
        expect(logs).toEqual(mockLogs);
      });

      const req = httpMock.expectOne('/api/GetProcessLogs');
      expect(req.request.method).toBe('GET');
      req.flush(mockLogs);
    });
  });

  describe('Transport Operations', () => {
    it('should start transport', () => {
      const mockResponse = { message: 'Transport started', fileId: 'test-id' };

      service.startTransport('MyInterface', 'csv-content').subscribe(response => {
        expect(response).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/start-transport');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ interfaceName: 'MyInterface', csvContent: 'csv-content' });
      req.flush(mockResponse);
    });

    it('should start transport without csvContent', () => {
      const mockResponse = { message: 'Transport started', fileId: 'test-id' };

      service.startTransport('MyInterface').subscribe(response => {
        expect(response).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/start-transport');
      expect(req.request.body).toEqual({ interfaceName: 'MyInterface', csvContent: undefined });
      req.flush(mockResponse);
    });

    it('should clear table', () => {
      const mockResponse = { message: 'Table cleared' };

      service.clearTable().subscribe(response => {
        expect(response).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/clear-table');
      expect(req.request.method).toBe('POST');
      req.flush(mockResponse);
    });

    it('should drop table', () => {
      const mockResponse = { message: 'Table dropped' };

      service.dropTable().subscribe(response => {
        expect(response).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/drop-table');
      expect(req.request.method).toBe('POST');
      req.flush(mockResponse);
    });

    it('should clear logs', () => {
      const mockResponse = { message: 'Logs cleared' };

      service.clearLogs().subscribe(response => {
        expect(response).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/ClearProcessLogs');
      expect(req.request.method).toBe('POST');
      req.flush(mockResponse);
    });
  });

  describe('Container App Status', () => {
    it('should get container app status', () => {
      const mockStatus = { exists: true, status: 'Running', lastChecked: '2024-01-01T00:00:00Z' };

      service.getContainerAppStatus('test-guid').subscribe(data => {
        expect(data).toEqual(mockStatus);
      });

      const req = httpMock.expectOne((request) => 
        request.url.includes('/GetContainerAppStatus') &&
        request.params.get('adapterInstanceGuid') === 'test-guid'
      );
      expect(req.request.method).toBe('GET');
      req.flush(mockStatus);
    });

    it('should handle container app status error gracefully', () => {
      service.getContainerAppStatus('test-guid').subscribe(data => {
        expect(data.exists).toBe(false);
        expect(data.status).toBe('Unknown');
        expect(data.errorMessage).toBeTruthy();
      });

      const req = httpMock.expectOne((request) => request.url.includes('/GetContainerAppStatus'));
      req.error(new ErrorEvent('Network error'));
    });
  });

  describe('Statistics and Schema', () => {
    it('should get processing statistics without parameters', () => {
      const mockStats = { totalRows: 100, succeededRows: 95 };

      service.getProcessingStatistics().subscribe(data => {
        expect(data).toEqual(mockStats);
      });

      const req = httpMock.expectOne('/api/GetProcessingStatistics');
      expect(req.request.method).toBe('GET');
      req.flush(mockStats);
    });

    it('should get processing statistics with all parameters', () => {
      const mockStats = { totalRows: 100 };
      const startDate = '2024-01-01';
      const endDate = '2024-01-31';

      service.getProcessingStatistics('TestInterface', startDate, endDate).subscribe(data => {
        expect(data).toEqual(mockStats);
      });

      const req = httpMock.expectOne((request) => 
        request.url.includes('/GetProcessingStatistics') &&
        request.url.includes('interfaceName=TestInterface') &&
        request.url.includes('startDate=') &&
        request.url.includes('endDate=')
      );
      expect(req.request.method).toBe('GET');
      req.flush(mockStats);
    });

    it('should get SQL table schema', () => {
      const mockSchema = { columns: [{ name: 'Id', dataType: 'int' }] };

      service.getSqlTableSchema('TestInterface', 'TransportData').subscribe(data => {
        expect(data).toEqual(mockSchema);
      });

      const req = httpMock.expectOne((request) => 
        request.url.includes('/GetSqlTableSchema') &&
        request.url.includes('interfaceName=TestInterface') &&
        request.url.includes('tableName=TransportData')
      );
      expect(req.request.method).toBe('GET');
      req.flush(mockSchema);
    });

    it('should get SQL table schema with default table name', () => {
      service.getSqlTableSchema('TestInterface').subscribe();

      const req = httpMock.expectOne((request) => 
        request.url.includes('tableName=TransportData')
      );
      req.flush({});
    });
  });

  describe('CSV Validation and Schema Comparison', () => {
    it('should validate CSV file', () => {
      const mockResult = { valid: true, issues: [] };

      service.validateCsvFile('csv-files/test.csv').subscribe(data => {
        expect(data).toEqual(mockResult);
      });

      const req = httpMock.expectOne((request) => 
        request.url.includes('/ValidateCsvFile') &&
        request.url.includes('blobPath=csv-files%2Ftest.csv')
      );
      expect(req.request.method).toBe('GET');
      req.flush(mockResult);
    });

    it('should validate CSV file with delimiter', () => {
      service.validateCsvFile('csv-files/test.csv', ',').subscribe();

      const req = httpMock.expectOne((request) => 
        request.url.includes('delimiter=%2C')
      );
      req.flush({ valid: true });
    });

    it('should compare CSV SQL schema', () => {
      const mockResult = { matches: [], mismatches: [] };

      service.compareCsvSqlSchema('TestInterface', 'csv-files/test.csv', 'TransportData').subscribe(data => {
        expect(data).toEqual(mockResult);
      });

      const req = httpMock.expectOne((request) => 
        request.url.includes('/CompareCsvSqlSchema') &&
        request.url.includes('interfaceName=TestInterface') &&
        request.url.includes('csvBlobPath=') &&
        request.url.includes('tableName=TransportData')
      );
      expect(req.request.method).toBe('GET');
      req.flush(mockResult);
    });
  });

  describe('Diagnostics', () => {
    it('should diagnose', () => {
      const mockResult = { status: 'ok', checks: [] };

      service.diagnose().subscribe(data => {
        expect(data).toEqual(mockResult);
      });

      const req = httpMock.expectOne('/api/diagnose');
      expect(req.request.method).toBe('GET');
      req.flush(mockResult);
    });
  });

  describe('Interface Configuration Operations', () => {
    it('should get interface configurations', () => {
      const mockConfigs = [
        { interfaceName: 'Test1', isEnabled: true },
        { interfaceName: 'Test2', isEnabled: false }
      ];

      service.getInterfaceConfigurations().subscribe(data => {
        expect(data).toEqual(mockConfigs);
      });

      const req = httpMock.expectOne('/api/GetInterfaceConfigurations');
      expect(req.request.method).toBe('GET');
      req.flush(mockConfigs);
    });

    it('should get interface configuration', () => {
      const mockConfig = { interfaceName: 'TestInterface', isEnabled: true };

      service.getInterfaceConfiguration('TestInterface').subscribe(data => {
        expect(data).toEqual(mockConfig);
      });

      const req = httpMock.expectOne((request) => 
        request.url.includes('/GetInterfaceConfiguration') &&
        request.url.includes('interfaceName=TestInterface')
      );
      expect(req.request.method).toBe('GET');
      req.flush(mockConfig);
    });

    it('should create interface configuration', () => {
      const config = {
        interfaceName: 'NewInterface',
        sourceAdapterName: 'CSV',
        destinationAdapterName: 'SqlServer'
      };
      const mockResponse = { success: true };

      service.createInterfaceConfiguration(config).subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/CreateInterfaceConfiguration');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(config);
      req.flush(mockResponse);
    });

    it('should delete interface configuration', () => {
      const mockResponse = { success: true };

      service.deleteInterfaceConfiguration('TestInterface').subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne((request) => 
        request.url.includes('/DeleteInterfaceConfiguration') &&
        request.url.includes('interfaceName=TestInterface')
      );
      expect(req.request.method).toBe('DELETE');
      req.flush(mockResponse);
    });

    it('should toggle interface configuration', () => {
      const mockResponse = { success: true };

      service.toggleInterfaceConfiguration('TestInterface', 'Source', true).subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/ToggleInterfaceConfiguration');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        interfaceName: 'TestInterface',
        adapterType: 'Source',
        enabled: true
      });
      req.flush(mockResponse);
    });

    it('should update interface name', () => {
      const mockResponse = { success: true };

      service.updateInterfaceName('OldName', 'NewName').subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/UpdateInterfaceName');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        oldInterfaceName: 'OldName',
        newInterfaceName: 'NewName'
      });
      req.flush(mockResponse);
    });
  });

  describe('Instance Operations', () => {
    it('should update instance name', () => {
      const mockResponse = { success: true };

      service.updateInstanceName('TestInterface', 'Source', 'NewInstanceName').subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/UpdateInstanceName');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        interfaceName: 'TestInterface',
        instanceType: 'Source',
        instanceName: 'NewInstanceName'
      });
      req.flush(mockResponse);
    });

    it('should restart adapter', () => {
      const mockResponse = { message: 'Adapter restarted' };

      service.restartAdapter('TestInterface', 'Source').subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/RestartAdapter');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        interfaceName: 'TestInterface',
        adapterType: 'Source'
      });
      req.flush(mockResponse);
    });
  });

  describe('CSV Adapter Updates', () => {
    it('should update receive folder', () => {
      const mockResponse = { success: true };

      service.updateReceiveFolder('TestInterface', '/new/folder').subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/UpdateReceiveFolder');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        interfaceName: 'TestInterface',
        receiveFolder: '/new/folder'
      });
      req.flush(mockResponse);
    });

    it('should update file mask', () => {
      const mockResponse = { success: true };

      service.updateFileMask('TestInterface', '*.csv').subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/UpdateFileMask');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({
        interfaceName: 'TestInterface',
        fileMask: '*.csv'
      });
      req.flush(mockResponse);
    });

    it('should update batch size', () => {
      const mockResponse = { success: true };

      service.updateBatchSize('TestInterface', 500).subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/UpdateBatchSize');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({
        interfaceName: 'TestInterface',
        batchSize: 500
      });
      req.flush(mockResponse);
    });

    it('should update CSV polling interval', () => {
      const mockResponse = { success: true };

      service.updateCsvPollingInterval('TestInterface', 30).subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/UpdateCsvPollingInterval');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({
        interfaceName: 'TestInterface',
        pollingInterval: 30
      });
      req.flush(mockResponse);
    });

    it('should update field separator', () => {
      const mockResponse = { success: true };

      service.updateFieldSeparator('TestInterface', ',').subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/UpdateFieldSeparator');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({
        interfaceName: 'TestInterface',
        fieldSeparator: ','
      });
      req.flush(mockResponse);
    });

    it('should update CSV data', () => {
      const mockResponse = { success: true };

      service.updateCsvData('TestInterface', 'col1,col2\nval1,val2').subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/UpdateCsvData');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({
        interfaceName: 'TestInterface',
        csvData: 'col1,col2\nval1,val2'
      });
      req.flush(mockResponse);
    });
  });

  describe('SQL Adapter Updates', () => {
    it('should update SQL connection properties', () => {
      const mockResponse = { success: true };
      const properties = {
        serverName: 'localhost',
        databaseName: 'TestDB',
        userName: 'sa',
        password: 'password',
        integratedSecurity: false
      };

      service.updateSqlConnectionProperties(
        'TestInterface',
        properties.serverName,
        properties.databaseName,
        properties.userName,
        properties.password,
        properties.integratedSecurity
      ).subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/UpdateSqlConnectionProperties');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({
        interfaceName: 'TestInterface',
        ...properties
      });
      req.flush(mockResponse);
    });

    it('should update SQL polling properties', () => {
      const mockResponse = { success: true };

      service.updateSqlPollingProperties('TestInterface', 'SELECT * FROM Test', 60).subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/UpdateSqlPollingProperties');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({
        interfaceName: 'TestInterface',
        pollingStatement: 'SELECT * FROM Test',
        pollingInterval: 60
      });
      req.flush(mockResponse);
    });

    it('should update SQL transaction properties', () => {
      const mockResponse = { success: true };

      service.updateSqlTransactionProperties('TestInterface', true, 1000).subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/UpdateSqlTransactionProperties');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({
        interfaceName: 'TestInterface',
        useTransaction: true,
        batchSize: 1000
      });
      req.flush(mockResponse);
    });
  });

  describe('Destination Adapter Operations', () => {
    it('should get destination adapter instances', () => {
      const mockInstances = [
        { adapterInstanceGuid: 'guid-1', instanceName: 'Instance1', adapterName: 'CSV' }
      ];

      service.getDestinationAdapterInstances('TestInterface').subscribe(data => {
        expect(data).toEqual(mockInstances);
      });

      const req = httpMock.expectOne((request) => 
        request.url.includes('/GetDestinationAdapterInstances') &&
        request.url.includes('interfaceName=TestInterface')
      );
      expect(req.request.method).toBe('GET');
      req.flush(mockInstances);
    });

    it('should add destination adapter instance', () => {
      const mockResponse = { success: true, adapterInstanceGuid: 'new-guid' };

      service.addDestinationAdapterInstance('TestInterface', 'CSV', 'NewInstance', '{}').subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/AddDestinationAdapterInstance');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        interfaceName: 'TestInterface',
        adapterName: 'CSV',
        instanceName: 'NewInstance',
        configuration: '{}'
      });
      req.flush(mockResponse);
    });

    it('should remove destination adapter instance', () => {
      const mockResponse = { success: true };

      service.removeDestinationAdapterInstance('TestInterface', 'guid-123').subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne((request) => 
        request.url.includes('/RemoveDestinationAdapterInstance') &&
        request.url.includes('interfaceName=TestInterface') &&
        request.url.includes('adapterInstanceGuid=guid-123')
      );
      expect(req.request.method).toBe('DELETE');
      req.flush(mockResponse);
    });

    it('should update destination adapter instance', () => {
      const mockResponse = { success: true };

      service.updateDestinationAdapterInstance(
        'TestInterface',
        'guid-123',
        'UpdatedName',
        true,
        '{"key": "value"}'
      ).subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/UpdateDestinationAdapterInstance');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({
        interfaceName: 'TestInterface',
        adapterInstanceGuid: 'guid-123',
        instanceName: 'UpdatedName',
        isEnabled: true,
        configuration: '{"key": "value"}'
      });
      req.flush(mockResponse);
    });

    it('should update source adapter instance', () => {
      const mockResponse = { success: true };

      service.updateSourceAdapterInstance(
        'TestInterface',
        'guid-123',
        'UpdatedName',
        true,
        '{"key": "value"}'
      ).subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/UpdateSourceAdapterInstance');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({
        interfaceName: 'TestInterface',
        adapterInstanceGuid: 'guid-123',
        instanceName: 'UpdatedName',
        isEnabled: true,
        configuration: '{"key": "value"}'
      });
      req.flush(mockResponse);
    });

    it('should update destination receive folder', () => {
      const mockResponse = { success: true };

      service.updateDestinationReceiveFolder('TestInterface', '/destination/folder').subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/UpdateDestinationReceiveFolder');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({
        interfaceName: 'TestInterface',
        destinationReceiveFolder: '/destination/folder'
      });
      req.flush(mockResponse);
    });

    it('should update destination file mask', () => {
      const mockResponse = { success: true };

      service.updateDestinationFileMask('TestInterface', '*.txt').subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/UpdateDestinationFileMask');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({
        interfaceName: 'TestInterface',
        destinationFileMask: '*.txt'
      });
      req.flush(mockResponse);
    });

    it('should update destination JQ script file', () => {
      const mockResponse = { success: true };

      service.updateDestinationJQScriptFile('TestInterface', 'guid-123', 'script.jq').subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/UpdateDestinationJQScriptFile');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        interfaceName: 'TestInterface',
        adapterInstanceGuid: 'guid-123',
        jqScriptFile: 'script.jq'
      });
      req.flush(mockResponse);
    });

    it('should update destination source adapter subscription', () => {
      const mockResponse = { success: true };

      service.updateDestinationSourceAdapterSubscription('TestInterface', 'guid-123', 'subscription-name').subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/UpdateDestinationSourceAdapterSubscription');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        interfaceName: 'TestInterface',
        adapterInstanceGuid: 'guid-123',
        sourceAdapterSubscription: 'subscription-name'
      });
      req.flush(mockResponse);
    });

    it('should update destination SQL statements', () => {
      const mockResponse = { success: true };

      service.updateDestinationSqlStatements(
        'TestInterface',
        'guid-123',
        'INSERT INTO ...',
        'UPDATE ...',
        'DELETE FROM ...'
      ).subscribe(data => {
        expect(data).toEqual(mockResponse);
      });

      const req = httpMock.expectOne('/api/UpdateDestinationSqlStatements');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        interfaceName: 'TestInterface',
        adapterInstanceGuid: 'guid-123',
        insertStatement: 'INSERT INTO ...',
        updateStatement: 'UPDATE ...',
        deleteStatement: 'DELETE FROM ...'
      });
      req.flush(mockResponse);
    });
  });

  describe('Service Bus Operations', () => {
    it('should get service bus messages', () => {
      const mockMessages = [
        { messageId: 'msg-1', body: { data: 'test' } }
      ];

      service.getServiceBusMessages('TestInterface', 50).subscribe(data => {
        expect(data).toEqual(mockMessages);
      });

      const req = httpMock.expectOne((request) => 
        request.url.includes('/GetServiceBusMessages') &&
        request.url.includes('interfaceName=TestInterface') &&
        request.url.includes('maxMessages=50')
      );
      expect(req.request.method).toBe('GET');
      req.flush(mockMessages);
    });

    it('should get service bus messages with default maxMessages', () => {
      service.getServiceBusMessages('TestInterface').subscribe();

      const req = httpMock.expectOne((request) => 
        request.url.includes('maxMessages=100')
      );
      req.flush([]);
    });
  });

  describe('Error Handling', () => {
    it('should handle HTTP 500 error', () => {
      service.getBlobContainerFolders('csv-files', '').subscribe({
        next: () => fail('should have failed'),
        error: (error: HttpErrorResponse) => {
          expect(error.status).toBe(500);
        }
      });

      const req = httpMock.expectOne((request) => request.url.includes('/GetBlobContainerFolders'));
      req.flush(null, { status: 500, statusText: 'Server Error' });
    });

    it('should handle HTTP 404 error', () => {
      service.getInterfaceConfiguration('NonExistent').subscribe({
        next: () => fail('should have failed'),
        error: (error: HttpErrorResponse) => {
          expect(error.status).toBe(404);
        }
      });

      const req = httpMock.expectOne((request) => request.url.includes('/GetInterfaceConfiguration'));
      req.flush(null, { status: 404, statusText: 'Not Found' });
    });

    it('should handle HTTP 401 unauthorized error', () => {
      service.getProcessLogs().subscribe({
        next: () => fail('should have failed'),
        error: (error: HttpErrorResponse) => {
          expect(error.status).toBe(401);
        }
      });

      const req = httpMock.expectOne('/api/GetProcessLogs');
      req.flush(null, { status: 401, statusText: 'Unauthorized' });
    });

    it('should handle network error', () => {
      service.startTransport('TestInterface').subscribe({
        next: () => fail('should have failed'),
        error: (error) => {
          expect(error).toBeTruthy();
        }
      });

      const req = httpMock.expectOne('/api/start-transport');
      req.error(new ErrorEvent('Network error'));
    });

    it('should handle empty response', () => {
      service.getInterfaceConfigurations().subscribe(data => {
        expect(data).toEqual([]);
      });

      const req = httpMock.expectOne('/api/GetInterfaceConfigurations');
      req.flush([]);
    });

    it('should handle malformed JSON response', () => {
      service.getProcessLogs().subscribe({
        next: () => fail('should have failed'),
        error: (error) => {
          expect(error).toBeTruthy();
        }
      });

      const req = httpMock.expectOne('/api/GetProcessLogs');
      req.flush('invalid json', { status: 200, statusText: 'OK' });
    });

    it('should handle getSampleCsvData error gracefully', () => {
      service.getSampleCsvData().subscribe(data => {
        expect(data).toEqual([]);
      });

      const req = httpMock.expectOne('/api/sample-csv');
      req.error(new ErrorEvent('Network error'));
    });

    it('should handle getSqlData error gracefully', () => {
      service.getSqlData().subscribe(data => {
        expect(data).toEqual([]);
      });

      const req = httpMock.expectOne('/api/sql-data');
      req.error(new ErrorEvent('Network error'));
    });
  });
});



