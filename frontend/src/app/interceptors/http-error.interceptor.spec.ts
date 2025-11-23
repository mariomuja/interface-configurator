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
});
