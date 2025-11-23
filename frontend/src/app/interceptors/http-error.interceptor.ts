import { Injectable } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpErrorResponse } from '@angular/common/http';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable, throwError, timer } from 'rxjs';
import { catchError, retry, retryWhen, delay, take, concatMap } from 'rxjs/operators';
import { ErrorTrackingService } from '../services/error-tracking.service';

@Injectable()
export class HttpErrorInterceptor implements HttpInterceptor {
  constructor(
    private snackBar: MatSnackBar,
    private errorTrackingService: ErrorTrackingService
  ) {}

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<any> {
    return next.handle(req).pipe(
      retry({
        count: 2,
        delay: (error, retryCount) => {
          // Only retry on network errors or 5xx errors
          if (this.isRetryableError(error)) {
            const delayMs = 1000 * retryCount;
            return timer(delayMs);
          }
          throw error;
        },
        resetOnSuccess: true
      }),
      catchError((error: HttpErrorResponse) => {
        // Track error
        this.errorTrackingService.trackError(
          `${req.method} ${req.url}`,
          error,
          'HttpInterceptor',
          {
            url: req.url,
            method: req.method,
            status: error.status,
            headers: this.sanitizeHeaders(req.headers)
          }
        );

        let errorMessage = 'Ein unbekannter Fehler ist aufgetreten';
        
        if (error.error instanceof ErrorEvent) {
          // Client-side error
          errorMessage = `Client-Fehler: ${error.error.message}`;
        } else {
          // Server-side error
          switch (error.status) {
            case 0:
              errorMessage = 'Keine Verbindung zum Server. Bitte überprüfen Sie Ihre Internetverbindung.';
              break;
            case 400:
              errorMessage = error.error?.error?.message || error.error?.message || 'Ungültige Anfrage';
              break;
            case 401:
              errorMessage = 'Nicht autorisiert. Bitte melden Sie sich an.';
              break;
            case 403:
              errorMessage = 'Zugriff verweigert';
              break;
            case 404:
              errorMessage = 'Ressource nicht gefunden';
              break;
            case 500:
              errorMessage = error.error?.error?.details || error.error?.error?.message || 'Server-Fehler. Bitte versuchen Sie es später erneut.';
              break;
            case 503:
              errorMessage = 'Service nicht verfügbar. Bitte versuchen Sie es später erneut.';
              break;
            default:
              errorMessage = error.error?.error?.message || error.error?.message || `Fehler ${error.status}: ${error.message}`;
          }
        }

        // Log error for debugging
        console.error('HTTP Error:', {
          url: req.url,
          status: error.status,
          message: errorMessage,
          error: error.error
        });

        // Show user-friendly error message
        this.snackBar.open(errorMessage, 'OK', {
          duration: 5000,
          panelClass: ['error-snackbar'],
          horizontalPosition: 'center',
          verticalPosition: 'top'
        });

        return throwError(() => error);
      })
    );
  }

  private isRetryableError(error: HttpErrorResponse): boolean {
    // Retry on network errors or 5xx server errors
    return error.status === 0 || (error.status >= 500 && error.status < 600);
  }

  private sanitizeHeaders(headers: any): any {
    const sanitized: any = {};
    headers.keys().forEach((key: string) => {
      // Don't log sensitive headers
      if (!key.toLowerCase().includes('authorization') && 
          !key.toLowerCase().includes('cookie')) {
        sanitized[key] = headers.get(key);
      }
    });
    return sanitized;
  }
}
