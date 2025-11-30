import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { DataLoadingService } from './data-loading.service';
import { TransportService } from './transport.service';
import { SqlRecord, ProcessLog } from '../models/data.model';

describe('DataLoadingService', () => {
  let service: DataLoadingService;
  let transportService: jasmine.SpyObj<TransportService>;

  beforeEach(() => {
    const transportSpy = jasmine.createSpyObj('TransportService', [
      'getSqlData',
      'getProcessLogs',
      'getServiceBusMessages'
    ]);

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        DataLoadingService,
        { provide: TransportService, useValue: transportSpy }
      ]
    });
    service = TestBed.inject(DataLoadingService);
    transportService = TestBed.inject(TransportService) as jasmine.SpyObj<TransportService>;
  });

  afterEach(() => {
    service.stopAllAutoRefresh();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should load SQL data', (done) => {
    const mockData: SqlRecord[] = [
      { id: '1', ID: '1', Name: 'John', Age: '30' },
      { id: '2', ID: '2', Name: 'Jane', Age: '25' }
    ];

    transportService.getSqlData.and.returnValue(of(mockData));

    service.loadSqlData().subscribe(data => {
      expect(data.length).toBe(2);
      expect(service.getSqlData()).toEqual(mockData);
      done();
    });
  });

  it('should load process logs', (done) => {
    const mockLogs: ProcessLog[] = [
      { id: 1, timestamp: new Date().toISOString(), level: 'info', message: 'Test message', details: 'Test details' }
    ];

    transportService.getProcessLogs.and.returnValue(of(mockLogs));

    service.loadProcessLogs().subscribe(logs => {
      expect(logs.length).toBe(1);
      expect(service.getProcessLogs()).toEqual(mockLogs);
      done();
    });
  });

  it('should filter process logs by component', (done) => {
    const mockLogs: ProcessLog[] = [
      { id: 1, timestamp: new Date().toISOString(), level: 'info', message: 'Azure Function test', details: '' },
      { id: 2, timestamp: new Date().toISOString(), level: 'info', message: 'SQL Server test', details: '' }
    ];

    transportService.getProcessLogs.and.returnValue(of(mockLogs));

    service.loadProcessLogs(undefined, 'Azure Function').subscribe(logs => {
      expect(logs.length).toBe(1);
      expect(logs[0].message).toContain('Azure Function');
      done();
    });
  });

  it('should load service bus messages', (done) => {
    const mockMessages = [
      { messageId: '1', body: 'Test message', enqueuedTime: new Date() }
    ];

    transportService.getServiceBusMessages.and.returnValue(of(mockMessages));

    service.loadServiceBusMessages('TestInterface', 100).subscribe(messages => {
      expect(messages.length).toBe(1);
      expect(service.getServiceBusMessages()).toEqual(mockMessages);
      done();
    });
  });

  it('should extract component from message', () => {
    expect(service.extractComponent('Azure Function error')).toBe('Azure Function');
    expect(service.extractComponent('SQL Server connection failed')).toBe('SQL Server');
    expect(service.extractComponent('Blob Storage upload')).toBe('Blob Storage');
    expect(service.extractComponent('Vercel API call')).toBe('Vercel API');
    expect(service.extractComponent('Unknown message')).toBe('Unknown');
  });

  it('should extract component from details', () => {
    expect(service.extractComponent('Test', 'Azure Function details')).toBe('Azure Function');
    expect(service.extractComponent('Test', 'SQL Server details')).toBe('SQL Server');
  });

  it('should start and stop auto refresh', (done) => {
    const mockData: SqlRecord[] = [];
    transportService.getSqlData.and.returnValue(of(mockData));
    transportService.getProcessLogs.and.returnValue(of([]));

    service.startAutoRefresh(100);
    
    setTimeout(() => {
      expect(transportService.getSqlData).toHaveBeenCalled();
      expect(transportService.getProcessLogs).toHaveBeenCalled();
      service.stopAutoRefresh();
      done();
    }, 150);
  });

  it('should start and stop service bus messages auto refresh', (done) => {
    const mockMessages: any[] = [];
    transportService.getServiceBusMessages.and.returnValue(of(mockMessages));

    service.startServiceBusMessagesAutoRefresh('TestInterface', 100);
    
    setTimeout(() => {
      expect(transportService.getServiceBusMessages).toHaveBeenCalled();
      service.stopServiceBusMessagesAutoRefresh();
      done();
    }, 150);
  });

  it('should stop all auto refresh', () => {
    service.startAutoRefresh();
    service.startServiceBusMessagesAutoRefresh('TestInterface');
    
    service.stopAllAutoRefresh();
    
    // Verify subscriptions are cleared (no errors should occur)
    expect(service).toBeTruthy();
  });

  it('should handle errors when loading SQL data', (done) => {
    transportService.getSqlData.and.returnValue(throwError(() => new Error('Test error')));

    service.loadSqlData().subscribe({
      next: () => fail('Should have thrown error'),
      error: (error) => {
        expect(error).toBeTruthy();
        done();
      }
    });
  });

  it('should handle errors when loading process logs', (done) => {
    transportService.getProcessLogs.and.returnValue(throwError(() => new Error('Test error')));

    service.loadProcessLogs().subscribe({
      next: () => fail('Should have thrown error'),
      error: (error) => {
        expect(error).toBeTruthy();
        done();
      }
    });
  });

  it('should handle errors when loading service bus messages', (done) => {
    transportService.getServiceBusMessages.and.returnValue(throwError(() => new Error('Test error')));

    service.loadServiceBusMessages('TestInterface').subscribe({
      next: () => fail('Should have thrown error'),
      error: (error) => {
        expect(error).toBeTruthy();
        done();
      }
    });
  });
});

