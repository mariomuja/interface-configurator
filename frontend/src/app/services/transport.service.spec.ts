import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
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

  it('should clear table', () => {
    const mockResponse = { message: 'Table cleared' };

    service.clearTable().subscribe(response => {
      expect(response).toEqual(mockResponse);
    });

    const req = httpMock.expectOne('/api/clear-table');
    expect(req.request.method).toBe('POST');
    req.flush(mockResponse);
  });
});



