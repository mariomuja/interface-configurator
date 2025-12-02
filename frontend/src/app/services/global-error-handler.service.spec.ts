import { TestBed, inject } from '@angular/core/testing';
import { ErrorHandler } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { GlobalErrorHandlerService } from './global-error-handler.service';
import { ErrorTrackingService } from './error-tracking.service';
import { ErrorDialogComponent } from '../components/error-dialog/error-dialog.component';

describe('GlobalErrorHandlerService', () => {
  let service: GlobalErrorHandlerService;
  let errorTrackingService: jasmine.SpyObj<ErrorTrackingService>;
  let dialog: jasmine.SpyObj<MatDialog>;

  beforeEach(() => {
    const errorTrackingSpy = jasmine.createSpyObj('ErrorTrackingService', [
      'trackError',
      'addApplicationState'
    ]);
    const dialogSpy = jasmine.createSpyObj('MatDialog', ['open']);

    TestBed.configureTestingModule({
      providers: [
        GlobalErrorHandlerService,
        { provide: ErrorTrackingService, useValue: errorTrackingSpy },
        { provide: MatDialog, useValue: dialogSpy }
      ]
    });

    service = TestBed.inject(GlobalErrorHandlerService);
    errorTrackingService = TestBed.inject(ErrorTrackingService) as jasmine.SpyObj<ErrorTrackingService>;
    dialog = TestBed.inject(MatDialog) as jasmine.SpyObj<MatDialog>;
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('handleError', () => {
    beforeEach(() => {
      // Wait for setTimeout to complete
      jasmine.clock().install();
    });

    afterEach(() => {
      jasmine.clock().uninstall();
    });

    it('should track error when ErrorTrackingService is available', () => {
      const testError = new Error('Test error');
      errorTrackingService.trackError.and.returnValue({
        id: 'error-123',
        timestamp: new Date(),
        functionName: 'GlobalErrorHandler',
        error: testError,
        component: 'Application',
        context: {}
      });

      service.handleError(testError);
      jasmine.clock().tick(1);

      expect(errorTrackingService.trackError).toHaveBeenCalledWith(
        'GlobalErrorHandler',
        testError,
        'Application',
        jasmine.objectContaining({
          url: jasmine.any(String),
          userAgent: jasmine.any(String)
        })
      );
    });

    it('should add application state when tracking error', () => {
      const testError = new Error('Test error');
      errorTrackingService.trackError.and.returnValue({
        id: 'error-123',
        timestamp: new Date(),
        functionName: 'GlobalErrorHandler',
        error: testError,
        component: 'Application',
        context: {}
      });

      service.handleError(testError);
      jasmine.clock().tick(1);

      expect(errorTrackingService.addApplicationState).toHaveBeenCalledWith('url', jasmine.any(String));
      expect(errorTrackingService.addApplicationState).toHaveBeenCalledWith('timestamp', jasmine.any(String));
    });

    it('should open error dialog when MatDialog is available', () => {
      const testError = new Error('Test error');
      errorTrackingService.trackError.and.returnValue({
        id: 'error-123',
        timestamp: new Date(),
        functionName: 'GlobalErrorHandler',
        error: testError,
        component: 'Application',
        context: {}
      });

      service.handleError(testError);
      jasmine.clock().tick(1);

      expect(dialog.open).toHaveBeenCalledWith(
        ErrorDialogComponent,
        jasmine.objectContaining({
          width: '800px',
          maxWidth: '90vw',
          data: jasmine.objectContaining({
            error: testError,
            functionName: 'GlobalErrorHandler',
            component: 'Application',
            context: jasmine.objectContaining({
              url: jasmine.any(String)
            })
          }),
          disableClose: false
        })
      );
    });

    it('should handle errors gracefully when services are not available', () => {
      const testError = new Error('Test error');
      
      // Create service without dependencies
      const isolatedService = new GlobalErrorHandlerService(TestBed.inject(ErrorHandler as any));
      
      expect(() => isolatedService.handleError(testError)).not.toThrow();
    });

    it('should log error to console', () => {
      const testError = new Error('Test error');
      spyOn(console, 'error');

      service.handleError(testError);
      jasmine.clock().tick(1);

      expect(console.error).toHaveBeenCalledWith('Global error handler caught:', testError);
    });

    it('should handle string errors', () => {
      const testError = 'String error';
      errorTrackingService.trackError.and.returnValue({
        id: 'error-123',
        timestamp: new Date(),
        functionName: 'GlobalErrorHandler',
        error: testError,
        component: 'Application',
        context: {}
      });

      service.handleError(testError);
      jasmine.clock().tick(1);

      expect(errorTrackingService.trackError).toHaveBeenCalled();
      expect(dialog.open).toHaveBeenCalled();
    });

    it('should handle object errors', () => {
      const testError = { message: 'Object error', code: 500 };
      errorTrackingService.trackError.and.returnValue({
        id: 'error-123',
        timestamp: new Date(),
        functionName: 'GlobalErrorHandler',
        error: testError,
        component: 'Application',
        context: {}
      });

      service.handleError(testError);
      jasmine.clock().tick(1);

      expect(errorTrackingService.trackError).toHaveBeenCalled();
      expect(dialog.open).toHaveBeenCalled();
    });
  });
});
