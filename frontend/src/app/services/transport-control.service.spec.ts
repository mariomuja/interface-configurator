import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { TransportControlService } from './transport-control.service';
import { TransportService } from './transport.service';

describe('TransportControlService', () => {
  let service: TransportControlService;
  let transportService: jasmine.SpyObj<TransportService>;

  beforeEach(() => {
    const transportSpy = jasmine.createSpyObj('TransportService', [
      'startTransport',
      'restartAdapter',
      'dropTable',
      'clearLogs'
    ]);

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule, MatSnackBarModule],
      providers: [
        TransportControlService,
        { provide: TransportService, useValue: transportSpy }
      ]
    });
    service = TestBed.inject(TransportControlService);
    transportService = TestBed.inject(TransportService) as jasmine.SpyObj<TransportService>;
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should start transport', (done) => {
    transportService.startTransport.and.returnValue(of({ message: 'Started', fileId: 'file-123' }));

    service.startTransport('TestInterface').subscribe(result => {
      expect(transportService.startTransport).toHaveBeenCalledWith('TestInterface', undefined);
      expect(result.message).toBe('Started');
      done();
    });
  });

  it('should start transport with CSV content', (done) => {
    transportService.startTransport.and.returnValue(of({ message: 'Started', fileId: 'file-123' }));

    service.startTransport('TestInterface', 'csv,data').subscribe(result => {
      expect(transportService.startTransport).toHaveBeenCalledWith('TestInterface', 'csv,data');
      expect(result.message).toBe('Started');
      done();
    });
  });

  it('should restart adapter', (done) => {
    transportService.restartAdapter.and.returnValue(of({ message: 'Restarted' }));

    service.restartAdapter('TestInterface', 'Source').subscribe(result => {
      expect(transportService.restartAdapter).toHaveBeenCalledWith('TestInterface', 'Source');
      expect(result.message).toBe('Restarted');
      done();
    });
  });

  it('should restart destination adapter', (done) => {
    transportService.restartAdapter.and.returnValue(of({ message: 'Restarted' }));

    service.restartAdapter('TestInterface', 'Destination').subscribe(result => {
      expect(transportService.restartAdapter).toHaveBeenCalledWith('TestInterface', 'Destination');
      expect(result.message).toBe('Restarted');
      done();
    });
  });

  it('should drop table', (done) => {
    transportService.dropTable.and.returnValue(of({ message: 'Dropped' }));

    service.dropTable().subscribe(result => {
      expect(transportService.dropTable).toHaveBeenCalled();
      expect(result.message).toBe('Dropped');
      done();
    });
  });

  it('should clear logs', (done) => {
    transportService.clearLogs.and.returnValue(of({ message: 'Cleared' }));

    service.clearLogs().subscribe(result => {
      expect(transportService.clearLogs).toHaveBeenCalled();
      expect(result.message).toBe('Cleared');
      done();
    });
  });
});

