import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CsvRecord, SqlRecord, ProcessLog } from '../models/data.model';

@Injectable({
  providedIn: 'root'
})
export class TransportService {
  private apiUrl = '/api';

  constructor(private http: HttpClient) {}

  getSampleCsvData(): Observable<CsvRecord[]> {
    return this.http.get<CsvRecord[]>(`${this.apiUrl}/sample-csv`);
  }

  getSqlData(): Observable<SqlRecord[]> {
    return this.http.get<SqlRecord[]>(`${this.apiUrl}/sql-data`);
  }

  getProcessLogs(): Observable<ProcessLog[]> {
    return this.http.get<ProcessLog[]>(`${this.apiUrl}/process-logs`);
  }

  startTransport(csvContent?: string): Observable<{ message: string; fileId: string }> {
    return this.http.post<{ message: string; fileId: string }>(`${this.apiUrl}/start-transport`, {
      csvContent: csvContent
    });
  }

  clearTable(): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/clear-table`, {});
  }

  dropTable(): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/drop-table`, {});
  }

  clearLogs(): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/clear-logs`, {});
  }

  diagnose(): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/diagnose`);
  }
}



