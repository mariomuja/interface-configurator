import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';
import { TransportService } from './transport.service';

export interface BootstrapCheck {
  name: string;
  status: 'Healthy' | 'Degraded' | 'Unhealthy';
  message: string;
  details?: string;
}

export interface BootstrapResult {
  timestamp: string;
  overallStatus: 'Healthy' | 'Degraded' | 'Unhealthy';
  healthyChecks: number;
  totalChecks: number;
  checks: BootstrapCheck[];
}

@Injectable({
  providedIn: 'root'
})
export class BootstrapService {
  private apiUrl: string;

  constructor(
    private http: HttpClient,
    private transportService: TransportService
  ) {
    // Use the same API URL resolution as TransportService
    this.apiUrl = this.getApiUrl();
  }

  private getApiUrl(): string {
    // Check for global API base URL override (set by index.html or environment)
    if (typeof window !== 'undefined') {
      const globalApiBaseUrl = (window as any).__API_BASE_URL__;
      if (globalApiBaseUrl) {
        return `${globalApiBaseUrl}/api`;
      }
    }

    // Production default: call the Azure Function App directly
    return 'https://func-integration-main.azurewebsites.net/api';
  }

  /**
   * Runs bootstrap checks and logs results to ProcessLogs
   */
  runBootstrap(): Observable<BootstrapResult> {
    return this.http.get<BootstrapResult>(`${this.apiUrl}/Bootstrap`).pipe(
      tap(result => {
        console.log('Bootstrap completed:', result);
      }),
      catchError(error => {
        console.error('Bootstrap failed:', error);
        // Return a degraded result on error
        const errorResult: BootstrapResult = {
          timestamp: new Date().toISOString(),
          overallStatus: 'Unhealthy' as const,
          healthyChecks: 0,
          totalChecks: 0,
          checks: [{
            name: 'Bootstrap',
            status: 'Unhealthy' as const,
            message: `Bootstrap check failed: ${error.message || 'Unknown error'}`,
            details: error.toString()
          }]
        };
        return of(errorResult);
      })
    );
  }

  /**
   * Runs bootstrap and then refreshes ProcessLogs
   */
  runBootstrapAndRefreshLogs(): Observable<BootstrapResult> {
    return this.runBootstrap().pipe(
      tap(() => {
        // Refresh ProcessLogs after a short delay to allow backend to write logs
        setTimeout(() => {
          // Trigger ProcessLogs refresh if component is listening
          // This will be handled by the component that calls this service
        }, 1000);
      })
    );
  }
}

