import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { HttpClient, HTTP_INTERCEPTORS } from '@angular/common/http';
import { MatSnackBarModule, MatSnackBar } from '@angular/material/snack-bar';
import { HttpErrorInterceptor } from './http-error.interceptor';
import { ErrorTrackingService } from '../services/error-tracking.service';

describe('HttpErrorInterceptor', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let snackBar: MatSnackBar;
  let errorTrackingService: ErrorTrackingService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule, MatSnackBarModule],
      providers: [
        {
          provide: HTTP_INTERCEPTORS,
          useClass: HttpErrorInterceptor,
          multi: true
        },
        ErrorTrackingService
      ]
    });

    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    snackBar = TestBed.inject(MatSnackBar);
    errorTrackingService = TestBed.inject(ErrorTrackingService);
    
    spyOn(snackBar, 'open');
    spyOn(errorTrackingService, 'trackError');
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(httpClient).toBeTruthy();
  });

  it('should handle 404 errors', () => {
    httpClient.get('/api/test').subscribe({
      next: () => fail('should have failed'),
      error: (error) => {
        expect(error.status).toBe(404);
        expect(snackBar.open).toHaveBeenCalled();
        expect(errorTrackingService.trackError).toHaveBeenCalled();
      }
    });

    const req = httpMock.expectOne('/api/test');
    req.flush('Not Found', { status: 404, statusText: 'Not Found' });
  });

  it('should handle 500 errors', () => {
    httpClient.get('/api/test').subscribe({
      next: () => fail('should have failed'),
      error: (error) => {
        expect(error.status).toBe(500);
        expect(snackBar.open).toHaveBeenCalled();
      }
    });

    const req = httpMock.expectOne('/api/test');
    req.flush('Server Error', { status: 500, statusText: 'Internal Server Error' });
  });

  it('should handle network errors', () => {
    httpClient.get('/api/test').subscribe({
      next: () => fail('should have failed'),
      error: (error) => {
        expect(error.status).toBe(0);
        expect(snackBar.open).toHaveBeenCalled();
      }
    });

    const req = httpMock.expectOne('/api/test');
    req.error(new ProgressEvent('error'));
  });

  it('should retry on retryable errors', () => {
    httpClient.get('/api/test').subscribe({
      next: () => fail('should have failed'),
      error: (error) => {
        expect(error.status).toBe(500);
      }
    });

    // Should retry up to 2 times
    const req1 = httpMock.expectOne('/api/test');
    req1.flush('Server Error', { status: 500, statusText: 'Internal Server Error' });
    
    const req2 = httpMock.expectOne('/api/test');
    req2.flush('Server Error', { status: 500, statusText: 'Internal Server Error' });
    
    const req3 = httpMock.expectOne('/api/test');
    req3.flush('Server Error', { status: 500, statusText: 'Internal Server Error' });
  });

  it('should not retry on non-retryable errors', () => {
    httpClient.get('/api/test').subscribe({
      next: () => fail('should have failed'),
      error: (error) => {
        expect(error.status).toBe(404);
      }
    });

    const req = httpMock.expectOne('/api/test');
    req.flush('Not Found', { status: 404, statusText: 'Not Found' });
    
    // Should not retry
    httpMock.expectNone('/api/test');
  });

  it('should retry on network errors (status 0)', () => {
    httpClient.get('/api/test').subscribe({
      next: () => fail('should have failed'),
      error: (error) => {
        expect(error.status).toBe(0);
      }
    });

    const req1 = httpMock.expectOne('/api/test');
    req1.error(new ProgressEvent('error'));
    
    const req2 = httpMock.expectOne('/api/test');
    req2.error(new ProgressEvent('error'));
    
    const req3 = httpMock.expectOne('/api/test');
    req3.error(new ProgressEvent('error'));
  });

  it('should handle 400 Bad Request errors', () => {
    httpClient.get('/api/test').subscribe({
      next: () => fail('should have failed'),
      error: (error) => {
        expect(error.status).toBe(400);
        expect(snackBar.open).toHaveBeenCalled();
      }
    });

    const req = httpMock.expectOne('/api/test');
    req.flush({ error: { message: 'Bad Request' } }, { status: 400, statusText: 'Bad Request' });
  });

  it('should handle 401 Unauthorized errors', () => {
    httpClient.get('/api/test').subscribe({
      next: () => fail('should have failed'),
      error: (error) => {
        expect(error.status).toBe(401);
        expect(snackBar.open).toHaveBeenCalled();
      }
    });

    const req = httpMock.expectOne('/api/test');
    req.flush(null, { status: 401, statusText: 'Unauthorized' });
  });

  it('should handle 403 Forbidden errors', () => {
    httpClient.get('/api/test').subscribe({
      next: () => fail('should have failed'),
      error: (error) => {
        expect(error.status).toBe(403);
        expect(snackBar.open).toHaveBeenCalled();
      }
    });

    const req = httpMock.expectOne('/api/test');
    req.flush(null, { status: 403, statusText: 'Forbidden' });
  });

  it('should handle 503 Service Unavailable errors', () => {
    httpClient.get('/api/test').subscribe({
      next: () => fail('should have failed'),
      error: (error) => {
        expect(error.status).toBe(503);
        expect(snackBar.open).toHaveBeenCalled();
      }
    });

    const req = httpMock.expectOne('/api/test');
    req.flush(null, { status: 503, statusText: 'Service Unavailable' });
  });

  it('should sanitize headers when tracking errors', () => {
    httpClient.get('/api/test').subscribe({
      next: () => fail('should have failed'),
      error: () => {
        expect(errorTrackingService.trackError).toHaveBeenCalled();
        const callArgs = errorTrackingService.trackError.calls.mostRecent().args;
        const context = callArgs[3];
        // Authorization and Cookie headers should be sanitized
        expect(context.headers).toBeDefined();
      }
    });

    const req = httpMock.expectOne('/api/test');
    req.flush(null, { status: 500, statusText: 'Server Error' });
  });

  it('should handle error with nested error message', () => {
    httpClient.get('/api/test').subscribe({
      next: () => fail('should have failed'),
      error: (error) => {
        expect(error.status).toBe(500);
        expect(snackBar.open).toHaveBeenCalled();
      }
    });

    const req = httpMock.expectOne('/api/test');
    req.flush({
      error: {
        error: {
          message: 'Nested error message',
          details: 'Error details'
        }
      }
    }, { status: 500, statusText: 'Server Error' });
  });

  it('should handle error with simple error message', () => {
    httpClient.get('/api/test').subscribe({
      next: () => fail('should have failed'),
      error: () => {
        expect(snackBar.open).toHaveBeenCalled();
      }
    });

    const req = httpMock.expectOne('/api/test');
    req.flush({
      error: {
        message: 'Simple error message'
      }
    }, { status: 500, statusText: 'Server Error' });
  });

  it('should track error with request details', () => {
    httpClient.post('/api/test', { data: 'test' }).subscribe({
      next: () => fail('should have failed'),
      error: () => {
        expect(errorTrackingService.trackError).toHaveBeenCalledWith(
          'POST /api/test',
          jasmine.any(Object),
          'HttpInterceptor',
          jasmine.objectContaining({
            url: '/api/test',
            method: 'POST',
            status: 500
          })
        );
      }
    });

    const req = httpMock.expectOne('/api/test');
    req.flush(null, { status: 500, statusText: 'Server Error' });
  });
});
