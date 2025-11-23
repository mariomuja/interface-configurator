import { Injectable, ErrorHandler, Injector } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { ErrorDialogComponent } from '../components/error-dialog/error-dialog.component';
import { ErrorTrackingService } from './error-tracking.service';

@Injectable()
export class GlobalErrorHandlerService implements ErrorHandler {
  private errorTrackingService?: ErrorTrackingService;
  private dialog?: MatDialog;

  constructor(private injector: Injector) {
    // Use injector to avoid circular dependencies
    setTimeout(() => {
      this.errorTrackingService = this.injector.get(ErrorTrackingService);
      this.dialog = this.injector.get(MatDialog);
    }, 0);
  }

  handleError(error: any): void {
    console.error('Global error handler caught:', error);

    // Track error
    if (this.errorTrackingService) {
      const errorReport = this.errorTrackingService.trackError(
        'GlobalErrorHandler',
        error,
        'Application',
        {
          url: window.location.href,
          userAgent: navigator.userAgent
        }
      );

      // Add application state
      this.errorTrackingService.addApplicationState('url', window.location.href);
      this.errorTrackingService.addApplicationState('timestamp', new Date().toISOString());
    }

    // Show error dialog
    if (this.dialog) {
      this.dialog.open(ErrorDialogComponent, {
        width: '800px',
        maxWidth: '90vw',
        data: {
          error: error,
          functionName: 'GlobalErrorHandler',
          component: 'Application',
          context: {
            url: window.location.href
          }
        },
        disableClose: false
      });
    }
  }
}



