import { Injectable } from '@angular/core';
import { Observable, BehaviorSubject, interval, Subscription } from 'rxjs';
import { TransportService } from './transport.service';
import { SqlRecord, ProcessLog } from '../models/data.model';

@Injectable({
  providedIn: 'root'
})
export class DataLoadingService {
  private sqlDataSubject = new BehaviorSubject<SqlRecord[]>([]);
  public sqlData$ = this.sqlDataSubject.asObservable();
  
  private processLogsSubject = new BehaviorSubject<ProcessLog[]>([]);
  public processLogs$ = this.processLogsSubject.asObservable();
  
  private serviceBusMessagesSubject = new BehaviorSubject<any[]>([]);
  public serviceBusMessages$ = this.serviceBusMessagesSubject.asObservable();
  
  private refreshSubscription?: Subscription;
  private serviceBusMessagesRefreshInterval?: Subscription;

  constructor(
    private transportService: TransportService
  ) {}

  getSqlData(): SqlRecord[] {
    return this.sqlDataSubject.value;
  }

  getProcessLogs(): ProcessLog[] {
    return this.processLogsSubject.value;
  }

  getServiceBusMessages(): any[] {
    return this.serviceBusMessagesSubject.value;
  }

  loadSqlData(interfaceName?: string): Observable<SqlRecord[]> {
    return new Observable(observer => {
      this.transportService.getSqlData().subscribe({
        next: (data) => {
          this.sqlDataSubject.next(data);
          observer.next(data);
          observer.complete();
        },
        error: (error) => {
          console.error('Error loading SQL data:', error);
          observer.error(error);
        }
      });
    });
  }

  loadProcessLogs(interfaceName?: string, component?: string): Observable<ProcessLog[]> {
    return new Observable(observer => {
      this.transportService.getProcessLogs().subscribe({
        next: (logs) => {
          // Filter by component if specified
          let filteredLogs = logs;
          if (component && component !== 'all') {
            filteredLogs = logs.filter(log => {
              const logComponent = this.extractComponent(log.message, log.details);
              return logComponent === component;
            });
          }
          
          this.processLogsSubject.next(filteredLogs);
          observer.next(filteredLogs);
          observer.complete();
        },
        error: (error) => {
          console.error('Error loading process logs:', error);
          observer.error(error);
        }
      });
    });
  }

  loadServiceBusMessages(interfaceName: string, maxMessages: number = 100): Observable<any[]> {
    return new Observable(observer => {
      this.transportService.getServiceBusMessages(interfaceName, maxMessages).subscribe({
        next: (messages) => {
          this.serviceBusMessagesSubject.next(messages);
          observer.next(messages);
          observer.complete();
        },
        error: (error) => {
          console.error('Error loading service bus messages:', error);
          observer.error(error);
        }
      });
    });
  }

  startAutoRefresh(refreshInterval: number = 3000): void {
    this.stopAutoRefresh();
    
    this.refreshSubscription = interval(refreshInterval).subscribe(() => {
      // Auto-refresh SQL data and process logs
      this.loadSqlData().subscribe();
      this.loadProcessLogs().subscribe();
    });
  }

  startServiceBusMessagesAutoRefresh(interfaceName: string, refreshInterval: number = 5000): void {
    this.stopServiceBusMessagesAutoRefresh();
    
    this.serviceBusMessagesRefreshInterval = interval(refreshInterval).subscribe(() => {
      this.loadServiceBusMessages(interfaceName).subscribe();
    });
  }

  stopAutoRefresh(): void {
    if (this.refreshSubscription) {
      this.refreshSubscription.unsubscribe();
      this.refreshSubscription = undefined;
    }
  }

  stopServiceBusMessagesAutoRefresh(): void {
    if (this.serviceBusMessagesRefreshInterval) {
      this.serviceBusMessagesRefreshInterval.unsubscribe();
      this.serviceBusMessagesRefreshInterval = undefined;
    }
  }

  stopAllAutoRefresh(): void {
    this.stopAutoRefresh();
    this.stopServiceBusMessagesAutoRefresh();
  }

  extractComponent(message: string, details?: string): string {
    if (!message) return 'Unknown';
    
    const messageLower = message.toLowerCase();
    const detailsLower = (details || '').toLowerCase();
    
    if (messageLower.includes('azure function') || detailsLower.includes('azure function')) {
      return 'Azure Function';
    }
    if (messageLower.includes('blob storage') || detailsLower.includes('blob storage')) {
      return 'Blob Storage';
    }
    if (messageLower.includes('sql server') || detailsLower.includes('sql server')) {
      return 'SQL Server';
    }
    if (messageLower.includes('vercel') || detailsLower.includes('vercel')) {
      return 'Vercel API';
    }
    
    return 'Unknown';
  }
}














