import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface FunctionCall {
  functionName: string;
  component?: string;
  timestamp: number;
  parameters?: any;
  returnValue?: any;
  duration?: number;
  success: boolean;
  error?: {
    message: string;
    stack?: string;
    name?: string;
    details?: any;
  };
}

export interface ErrorReport {
  errorId: string;
  timestamp: number;
  userAgent: string;
  url: string;
  functionCallHistory: FunctionCall[];
  currentError: {
    functionName: string;
    component?: string;
    error: Error;
    stack: string;
    context: any;
  };
  applicationState: {
    currentInterface?: string;
    sourceEnabled?: boolean;
    destinationEnabled?: boolean;
    [key: string]: any;
  };
  environment: {
    apiUrl: string;
    browser: string;
    platform: string;
  };
}

@Injectable({
  providedIn: 'root'
})
export class ErrorTrackingService {
  private functionCallHistory: FunctionCall[] = [];
  private readonly MAX_HISTORY_SIZE = 100; // Keep last 100 function calls
  private currentErrorReport: ErrorReport | null = null;
  private apiUrl: string;

  constructor(private http: HttpClient) {
    // Determine API URL (same logic as TransportService)
    this.apiUrl = this.getApiUrl();
  }

  private getApiUrl(): string {
    if (typeof window !== 'undefined') {
      const hostname = window.location.hostname.toLowerCase();
      if (hostname === 'localhost' || hostname === '127.0.0.1') {
        return 'http://localhost:7071/api';
      }
    }
    return 'https://func-integration-main.azurewebsites.net/api';
  }

  /**
   * Track a function call (successful)
   */
  trackFunctionCall(
    functionName: string,
    component?: string,
    parameters?: any,
    returnValue?: any,
    duration?: number
  ): void {
    const call: FunctionCall = {
      functionName,
      component,
      timestamp: Date.now(),
      parameters: this.sanitizeForLogging(parameters),
      returnValue: this.sanitizeForLogging(returnValue),
      duration,
      success: true
    };

    this.addToHistory(call);
    this.saveToLocalStorage();
  }

  /**
   * Track a function call that resulted in an error
   */
  trackError(
    functionName: string,
    error: Error | any,
    component?: string,
    context?: any
  ): ErrorReport {
    const errorCall: FunctionCall = {
      functionName,
      component,
      timestamp: Date.now(),
      success: false,
      error: {
        message: error?.message || String(error),
        stack: error?.stack,
        name: error?.name,
        details: this.sanitizeForLogging(error)
      }
    };

    this.addToHistory(errorCall);

    // Create comprehensive error report
    const errorReport: ErrorReport = {
      errorId: this.generateErrorId(),
      timestamp: Date.now(),
      userAgent: navigator.userAgent,
      url: window.location.href,
      functionCallHistory: [...this.functionCallHistory],
      currentError: {
        functionName,
        component,
        error: error instanceof Error ? error : new Error(String(error)),
        stack: error?.stack || new Error().stack || '',
        context: this.sanitizeForLogging(context)
      },
      applicationState: this.captureApplicationState(),
      environment: {
        apiUrl: this.apiUrl,
        browser: this.getBrowserInfo(),
        platform: navigator.platform
      }
    };

    this.currentErrorReport = errorReport;
    this.saveErrorReportToLocalStorage(errorReport);
    
    return errorReport;
  }

  /**
   * Get the current error report
   */
  getCurrentErrorReport(): ErrorReport | null {
    return this.currentErrorReport;
  }

  /**
   * Submit error report to backend for AI processing
   */
  submitErrorToAI(errorReport: ErrorReport): Observable<any> {
    return this.http.post(`${this.apiUrl}/SubmitErrorToAI`, errorReport);
  }

  /**
   * Clear error report after it's been handled
   */
  clearErrorReport(): void {
    this.currentErrorReport = null;
    localStorage.removeItem('errorReport');
  }

  /**
   * Clear function call history
   */
  clearHistory(): void {
    this.functionCallHistory = [];
    localStorage.removeItem('functionCallHistory');
  }

  private addToHistory(call: FunctionCall): void {
    this.functionCallHistory.push(call);
    
    // Keep only last MAX_HISTORY_SIZE calls
    if (this.functionCallHistory.length > this.MAX_HISTORY_SIZE) {
      this.functionCallHistory.shift();
    }
  }

  private saveToLocalStorage(): void {
    try {
      // Only save last 50 calls to localStorage to avoid size issues
      const recentHistory = this.functionCallHistory.slice(-50);
      localStorage.setItem('functionCallHistory', JSON.stringify(recentHistory));
    } catch (e) {
      console.warn('Failed to save function call history to localStorage:', e);
    }
  }

  private saveErrorReportToLocalStorage(errorReport: ErrorReport): void {
    try {
      localStorage.setItem('errorReport', JSON.stringify(errorReport));
    } catch (e) {
      console.warn('Failed to save error report to localStorage:', e);
    }
  }

  private generateErrorId(): string {
    return `ERR-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
  }

  private sanitizeForLogging(obj: any): any {
    if (obj === null || obj === undefined) {
      return obj;
    }

    // Don't log functions
    if (typeof obj === 'function') {
      return '[Function]';
    }

    // Handle circular references and large objects
    try {
      const jsonString = JSON.stringify(obj, (key, value) => {
        // Limit string length
        if (typeof value === 'string' && value.length > 1000) {
          return value.substring(0, 1000) + '...[truncated]';
        }
        // Don't serialize functions
        if (typeof value === 'function') {
          return '[Function]';
        }
        return value;
      }, 2);

      // Limit total size
      if (jsonString.length > 50000) {
        return JSON.parse(jsonString.substring(0, 50000) + '...[truncated]');
      }

      return JSON.parse(jsonString);
    } catch (e) {
      return '[Unable to serialize]';
    }
  }

  private captureApplicationState(): any {
    // Capture relevant application state
    // This will be enhanced by components that call trackError
    return {
      timestamp: Date.now()
    };
  }

  /**
   * Allow components to add state information
   */
  addApplicationState(key: string, value: any): void {
    if (this.currentErrorReport) {
      this.currentErrorReport.applicationState[key] = this.sanitizeForLogging(value);
    }
  }

  private getBrowserInfo(): string {
    const ua = navigator.userAgent;
    if (ua.includes('Chrome')) return 'Chrome';
    if (ua.includes('Firefox')) return 'Firefox';
    if (ua.includes('Safari')) return 'Safari';
    if (ua.includes('Edge')) return 'Edge';
    return 'Unknown';
  }
}



