import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { BootstrapService, BootstrapResult, BootstrapCheck } from './bootstrap.service';
import { TransportService } from './transport.service';

describe('BootstrapService', () => {
  let service: BootstrapService;
  let httpMock: HttpTestingController;
  let transportService: jasmine.SpyObj<TransportService>;

  beforeEach(() => {
    const transportServiceSpy = jasmine.createSpyObj('TransportService', ['loadProcessLogs']);

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        BootstrapService,
        { provide: TransportService, useValue: transportServiceSpy }
      ]
    });
    service = TestBed.inject(BootstrapService);
    httpMock = TestBed.inject(HttpTestingController);
    transportService = TestBed.inject(TransportService) as jasmine.SpyObj<TransportService>;
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should run bootstrap successfully', (done) => {
    const mockResult: BootstrapResult = {
      timestamp: new Date().toISOString(),
      overallStatus: 'Healthy',
      healthyChecks: 5,
      totalChecks: 5,
      checks: [
        {
          name: 'Database',
          status: 'Healthy',
          message: 'Database connection successful'
        },
        {
          name: 'Blob Storage',
          status: 'Healthy',
          message: 'Blob Storage accessible'
        }
      ]
    };

    service.runBootstrap().subscribe({
      next: (result) => {
        expect(result).toEqual(mockResult);
        expect(result.overallStatus).toBe('Healthy');
        expect(result.healthyChecks).toBe(5);
        done();
      },
      error: done.fail
    });

    const req = httpMock.expectOne((request) => 
      request.url.includes('/api/Bootstrap')
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('should handle bootstrap errors gracefully', (done) => {
    service.runBootstrap().subscribe({
      next: (result) => {
        expect(result.overallStatus).toBe('Unhealthy');
        expect(result.healthyChecks).toBe(0);
        expect(result.checks.length).toBe(1);
        expect(result.checks[0].name).toBe('Bootstrap');
        expect(result.checks[0].status).toBe('Unhealthy');
        done();
      },
      error: done.fail
    });

    const req = httpMock.expectOne((request) => 
      request.url.includes('/api/Bootstrap')
    );
    req.error(new ErrorEvent('Network error'), { status: 500 });
  });

  it('should run bootstrap and refresh logs', (done) => {
    const mockResult: BootstrapResult = {
      timestamp: new Date().toISOString(),
      overallStatus: 'Healthy',
      healthyChecks: 5,
      totalChecks: 5,
      checks: []
    };

    service.runBootstrapAndRefreshLogs().subscribe({
      next: (result) => {
        expect(result).toEqual(mockResult);
        done();
      },
      error: done.fail
    });

    const req = httpMock.expectOne((request) => 
      request.url.includes('/api/Bootstrap')
    );
    req.flush(mockResult);
  });

  it('should handle degraded status', (done) => {
    const mockResult: BootstrapResult = {
      timestamp: new Date().toISOString(),
      overallStatus: 'Degraded',
      healthyChecks: 3,
      totalChecks: 5,
      checks: [
        {
          name: 'Database',
          status: 'Healthy',
          message: 'Database connection successful'
        },
        {
          name: 'Service Bus',
          status: 'Degraded',
          message: 'Service Bus connection slow'
        }
      ]
    };

    service.runBootstrap().subscribe({
      next: (result) => {
        expect(result.overallStatus).toBe('Degraded');
        expect(result.healthyChecks).toBe(3);
        expect(result.totalChecks).toBe(5);
        done();
      },
      error: done.fail
    });

    const req = httpMock.expectOne((request) => 
      request.url.includes('/api/Bootstrap')
    );
    req.flush(mockResult);
  });
});










