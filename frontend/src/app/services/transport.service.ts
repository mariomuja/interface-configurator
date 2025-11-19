import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { CsvRecord, SqlRecord, ProcessLog } from '../models/data.model';
import { SessionService } from './session.service';

@Injectable({
  providedIn: 'root'
})
export class TransportService {
  // Use Azure Function App URL from environment or fall back to relative /api
  // In production (Vercel), this will use the Azure Function App URL directly
  // In development, it will use /api which can be proxied via Angular proxy config
  private apiUrl = this.getApiUrl();

  constructor(
    private http: HttpClient,
    private sessionService: SessionService
  ) {}

  private getApiUrl(): string {
    // Always use relative /api path
    // This will be handled by Vercel serverless functions (api/*.js) in production
    // or Angular proxy config in development
    // The Vercel serverless functions handle CORS and proxy to Azure Functions
    return '/api';
  }

  getSampleCsvData(): Observable<CsvRecord[]> {
    // Note: This endpoint doesn't exist yet - returning empty array for now
    // TODO: Create sample-csv endpoint or remove this call
    return this.http.get<CsvRecord[]>(`${this.apiUrl}/sample-csv`).pipe(
      catchError(() => of([] as CsvRecord[]))
    );
  }

  getSqlData(): Observable<SqlRecord[]> {
    // Note: This endpoint doesn't exist yet - returning empty array for now
    // TODO: Create sql-data endpoint or remove this call
    return this.http.get<SqlRecord[]>(`${this.apiUrl}/sql-data`).pipe(
      catchError(() => of([] as SqlRecord[]))
    );
  }

  getProcessLogs(): Observable<ProcessLog[]> {
    return this.http.get<ProcessLog[]>(`${this.apiUrl}/GetProcessLogs`);
  }

  startTransport(interfaceName: string, csvContent?: string): Observable<{ message: string; fileId: string }> {
    return this.http.post<{ message: string; fileId: string }>(`${this.apiUrl}/start-transport`, {
      interfaceName,
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

  // Processing Statistics API
  getProcessingStatistics(interfaceName?: string, startDate?: string, endDate?: string): Observable<any> {
    let url = `${this.apiUrl}/GetProcessingStatistics`;
    const params: string[] = [];
    if (interfaceName) params.push(`interfaceName=${encodeURIComponent(interfaceName)}`);
    if (startDate) params.push(`startDate=${encodeURIComponent(startDate)}`);
    if (endDate) params.push(`endDate=${encodeURIComponent(endDate)}`);
    if (params.length > 0) url += '?' + params.join('&');
    return this.http.get<any>(url);
  }

  // SQL Schema API
  getSqlTableSchema(interfaceName: string, tableName: string = 'TransportData'): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/GetSqlTableSchema?interfaceName=${encodeURIComponent(interfaceName)}&tableName=${encodeURIComponent(tableName)}`);
  }

  // CSV Validation API
  validateCsvFile(blobPath: string, delimiter?: string): Observable<any> {
    let url = `${this.apiUrl}/ValidateCsvFile?blobPath=${encodeURIComponent(blobPath)}`;
    if (delimiter) url += `&delimiter=${encodeURIComponent(delimiter)}`;
    return this.http.get<any>(url);
  }

  // Schema Comparison API
  compareCsvSqlSchema(interfaceName: string, csvBlobPath: string, tableName: string = 'TransportData'): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/CompareCsvSqlSchema?interfaceName=${encodeURIComponent(interfaceName)}&csvBlobPath=${encodeURIComponent(csvBlobPath)}&tableName=${encodeURIComponent(tableName)}`);
  }

  diagnose(): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/diagnose`);
  }

  getInterfaceConfigurations(): Observable<any[]> {
    const sessionId = this.sessionService.getSessionId();
    return this.http.get<any[]>(`${this.apiUrl}/GetInterfaceConfigurations?sessionId=${encodeURIComponent(sessionId)}`);
  }

  createInterfaceConfiguration(config: {
    interfaceName: string;
    sourceAdapterName?: string;
    sourceConfiguration?: string;
    destinationAdapterName?: string;
    destinationConfiguration?: string;
    description?: string;
  }): Observable<any> {
    const sessionId = this.sessionService.getSessionId();
    return this.http.post<any>(`${this.apiUrl}/CreateInterfaceConfiguration`, {
      ...config,
      sessionId
    });
  }

  deleteInterfaceConfiguration(interfaceName: string): Observable<any> {
    const sessionId = this.sessionService.getSessionId();
    return this.http.delete<any>(`${this.apiUrl}/DeleteInterfaceConfiguration?interfaceName=${encodeURIComponent(interfaceName)}&sessionId=${encodeURIComponent(sessionId)}`);
  }

  getInterfaceConfiguration(interfaceName: string): Observable<any> {
    const sessionId = this.sessionService.getSessionId();
    return this.http.get<any>(`${this.apiUrl}/GetInterfaceConfiguration?interfaceName=${encodeURIComponent(interfaceName)}&sessionId=${encodeURIComponent(sessionId)}`);
  }

  toggleInterfaceConfiguration(interfaceName: string, adapterType: 'Source' | 'Destination', enabled: boolean): Observable<any> {
    const sessionId = this.sessionService.getSessionId();
    return this.http.post<any>(`${this.apiUrl}/ToggleInterfaceConfiguration`, {
      interfaceName,
      adapterType,
      enabled,
      sessionId
    });
  }

  updateInterfaceName(oldInterfaceName: string, newInterfaceName: string): Observable<any> {
    const sessionId = this.sessionService.getSessionId();
    return this.http.post<any>(`${this.apiUrl}/UpdateInterfaceName`, {
      oldInterfaceName,
      newInterfaceName,
      sessionId
    });
  }

  updateInstanceName(interfaceName: string, instanceType: 'Source' | 'Destination', instanceName: string): Observable<any> {
    const sessionId = this.sessionService.getSessionId();
    return this.http.post<any>(`${this.apiUrl}/UpdateInstanceName`, {
      interfaceName,
      instanceType,
      instanceName,
      sessionId
    });
  }

  restartAdapter(interfaceName: string, adapterType: 'Source' | 'Destination'): Observable<{ message: string }> {
    const sessionId = this.sessionService.getSessionId();
    return this.http.post<{ message: string }>(`${this.apiUrl}/RestartAdapter`, {
      interfaceName,
      adapterType,
      sessionId
    });
  }

  updateReceiveFolder(interfaceName: string, receiveFolder: string): Observable<any> {
    const sessionId = this.sessionService.getSessionId();
    return this.http.post<any>(`${this.apiUrl}/UpdateReceiveFolder`, {
      interfaceName,
      receiveFolder,
      sessionId
    });
  }

  updateFileMask(interfaceName: string, fileMask: string): Observable<any> {
    const sessionId = this.sessionService.getSessionId();
    return this.http.put<any>(`${this.apiUrl}/UpdateFileMask`, {
      interfaceName,
      fileMask,
      sessionId
    });
  }

  updateBatchSize(interfaceName: string, batchSize: number): Observable<any> {
    const sessionId = this.sessionService.getSessionId();
    return this.http.put<any>(`${this.apiUrl}/UpdateBatchSize`, {
      interfaceName,
      batchSize,
      sessionId
    });
  }

  updateSqlConnectionProperties(
    interfaceName: string,
    serverName?: string,
    databaseName?: string,
    userName?: string,
    password?: string,
    integratedSecurity?: boolean,
    resourceGroup?: string
  ): Observable<any> {
    const sessionId = this.sessionService.getSessionId();
    return this.http.put<any>(`${this.apiUrl}/UpdateSqlConnectionProperties`, {
      interfaceName,
      serverName,
      databaseName,
      userName,
      password,
      integratedSecurity,
      resourceGroup,
      sessionId
    });
  }

  updateSqlPollingProperties(
    interfaceName: string,
    pollingStatement?: string,
    pollingInterval?: number
  ): Observable<any> {
    const sessionId = this.sessionService.getSessionId();
    return this.http.put<any>(`${this.apiUrl}/UpdateSqlPollingProperties`, {
      interfaceName,
      pollingStatement,
      pollingInterval,
      sessionId
    });
  }

  updateCsvPollingInterval(interfaceName: string, pollingInterval: number): Observable<any> {
    const sessionId = this.sessionService.getSessionId();
    return this.http.put<any>(`${this.apiUrl}/UpdateCsvPollingInterval`, {
      interfaceName,
      pollingInterval,
      sessionId
    });
  }

  updateFieldSeparator(interfaceName: string, fieldSeparator: string): Observable<any> {
    const sessionId = this.sessionService.getSessionId();
    return this.http.put<any>(`${this.apiUrl}/UpdateFieldSeparator`, {
      interfaceName,
      fieldSeparator,
      sessionId
    });
  }

  updateCsvData(interfaceName: string, csvData: string): Observable<any> {
    const sessionId = this.sessionService.getSessionId();
    return this.http.put<any>(`${this.apiUrl}/UpdateCsvData`, {
      interfaceName,
      csvData,
      sessionId
    });
  }

  updateDestinationReceiveFolder(interfaceName: string, destinationReceiveFolder: string): Observable<any> {
    const sessionId = this.sessionService.getSessionId();
    return this.http.put<any>(`${this.apiUrl}/UpdateDestinationReceiveFolder`, {
      interfaceName,
      destinationReceiveFolder,
      sessionId
    });
  }

  updateDestinationFileMask(interfaceName: string, destinationFileMask: string): Observable<any> {
    const sessionId = this.sessionService.getSessionId();
    return this.http.put<any>(`${this.apiUrl}/UpdateDestinationFileMask`, {
      interfaceName,
      destinationFileMask,
      sessionId
    });
  }

  getMessageBoxMessages(interfaceName: string, adapterInstanceGuid: string, adapterType: string = 'Source'): Observable<any[]> {
    const params = new URLSearchParams({
      interfaceName,
      adapterInstanceGuid,
      adapterType
    });
    return this.http.get<any[]>(`${this.apiUrl}/GetMessageBoxMessages?${params.toString()}`);
  }

  getDestinationAdapterInstances(interfaceName: string): Observable<any[]> {
    const sessionId = this.sessionService.getSessionId();
    return this.http.get<any[]>(`${this.apiUrl}/GetDestinationAdapterInstances?interfaceName=${encodeURIComponent(interfaceName)}&sessionId=${encodeURIComponent(sessionId)}`);
  }

  addDestinationAdapterInstance(interfaceName: string, adapterName: string, instanceName?: string, configuration?: string): Observable<any> {
    const sessionId = this.sessionService.getSessionId();
    return this.http.post<any>(`${this.apiUrl}/AddDestinationAdapterInstance`, {
      interfaceName,
      adapterName,
      instanceName,
      configuration,
      sessionId
    });
  }

  removeDestinationAdapterInstance(interfaceName: string, adapterInstanceGuid: string): Observable<any> {
    const sessionId = this.sessionService.getSessionId();
    // Use query parameters for DELETE request (more standard than body)
    return this.http.delete<any>(`${this.apiUrl}/RemoveDestinationAdapterInstance?interfaceName=${encodeURIComponent(interfaceName)}&adapterInstanceGuid=${encodeURIComponent(adapterInstanceGuid)}&sessionId=${encodeURIComponent(sessionId)}`);
  }

  updateDestinationAdapterInstance(
    interfaceName: string,
    adapterInstanceGuid: string,
    instanceName?: string,
    isEnabled?: boolean,
    configuration?: string
  ): Observable<any> {
    const sessionId = this.sessionService.getSessionId();
    return this.http.put<any>(`${this.apiUrl}/UpdateDestinationAdapterInstance`, {
      interfaceName,
      adapterInstanceGuid,
      instanceName,
      isEnabled,
      configuration,
      sessionId
    });
  }

  updateSqlTransactionProperties(
    interfaceName: string,
    useTransaction?: boolean,
    batchSize?: number
  ): Observable<any> {
    const sessionId = this.sessionService.getSessionId();
    return this.http.put<any>(`${this.apiUrl}/UpdateSqlTransactionProperties`, {
      interfaceName,
      useTransaction,
      batchSize,
      sessionId
    });
  }
}



