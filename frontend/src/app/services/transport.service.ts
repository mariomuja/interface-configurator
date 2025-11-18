import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { CsvRecord, SqlRecord, ProcessLog } from '../models/data.model';

@Injectable({
  providedIn: 'root'
})
export class TransportService {
  // Use Azure Function App URL if available, otherwise fall back to relative /api
  // This allows the app to work both locally (with proxy) and on Vercel (with Azure Functions)
  private apiUrl = this.getApiUrl();

  constructor(private http: HttpClient) {}

  private getApiUrl(): string {
    // Vercel will proxy /api/* requests to Azure Functions via vercel.json
    // This allows the frontend to use relative paths while Vercel handles the proxying
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
    return this.http.get<any[]>(`${this.apiUrl}/GetInterfaceConfigurations`);
  }

  createInterfaceConfiguration(config: {
    interfaceName: string;
    sourceAdapterName?: string;
    sourceConfiguration?: string;
    destinationAdapterName?: string;
    destinationConfiguration?: string;
    description?: string;
  }): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/create-interface-config`, config);
  }

  toggleInterfaceConfiguration(interfaceName: string, adapterType: 'Source' | 'Destination', enabled: boolean): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/toggle-interface-config`, {
      interfaceName,
      adapterType,
      enabled
    });
  }

  updateInterfaceName(oldInterfaceName: string, newInterfaceName: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/update-interface-name`, {
      oldInterfaceName,
      newInterfaceName
    });
  }

  updateInstanceName(interfaceName: string, instanceType: 'Source' | 'Destination', instanceName: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/update-instance-name`, {
      interfaceName,
      instanceType,
      instanceName
    });
  }

  restartAdapter(interfaceName: string, adapterType: 'Source' | 'Destination'): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/restart-adapter`, {
      interfaceName,
      adapterType
    });
  }

  updateReceiveFolder(interfaceName: string, receiveFolder: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/update-receive-folder`, {
      interfaceName,
      receiveFolder
    });
  }

  updateFileMask(interfaceName: string, fileMask: string): Observable<any> {
    return this.http.put<any>(`${this.apiUrl}/UpdateFileMask`, {
      interfaceName,
      fileMask
    });
  }

  updateBatchSize(interfaceName: string, batchSize: number): Observable<any> {
    return this.http.put<any>(`${this.apiUrl}/UpdateBatchSize`, {
      interfaceName,
      batchSize
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
    return this.http.put<any>(`${this.apiUrl}/UpdateSqlConnectionProperties`, {
      interfaceName,
      serverName,
      databaseName,
      userName,
      password,
      integratedSecurity,
      resourceGroup
    });
  }

  updateSqlPollingProperties(
    interfaceName: string,
    pollingStatement?: string,
    pollingInterval?: number
  ): Observable<any> {
    return this.http.put<any>(`${this.apiUrl}/UpdateSqlPollingProperties`, {
      interfaceName,
      pollingStatement,
      pollingInterval
    });
  }

  updateFieldSeparator(interfaceName: string, fieldSeparator: string): Observable<any> {
    return this.http.put<any>(`${this.apiUrl}/UpdateFieldSeparator`, {
      interfaceName,
      fieldSeparator
    });
  }

  updateDestinationReceiveFolder(interfaceName: string, destinationReceiveFolder: string): Observable<any> {
    return this.http.put<any>(`${this.apiUrl}/UpdateDestinationReceiveFolder`, {
      interfaceName,
      destinationReceiveFolder
    });
  }

  updateDestinationFileMask(interfaceName: string, destinationFileMask: string): Observable<any> {
    return this.http.put<any>(`${this.apiUrl}/UpdateDestinationFileMask`, {
      interfaceName,
      destinationFileMask
    });
  }

  getDestinationAdapterInstances(interfaceName: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}/GetDestinationAdapterInstances?interfaceName=${encodeURIComponent(interfaceName)}`);
  }

  addDestinationAdapterInstance(interfaceName: string, adapterName: string, instanceName?: string, configuration?: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/AddDestinationAdapterInstance`, {
      interfaceName,
      adapterName,
      instanceName,
      configuration
    });
  }

  removeDestinationAdapterInstance(interfaceName: string, adapterInstanceGuid: string): Observable<any> {
    return this.http.delete<any>(`${this.apiUrl}/RemoveDestinationAdapterInstance`, {
      body: {
        interfaceName,
        adapterInstanceGuid
      }
    });
  }

  updateDestinationAdapterInstance(
    interfaceName: string,
    adapterInstanceGuid: string,
    instanceName?: string,
    isEnabled?: boolean,
    configuration?: string
  ): Observable<any> {
    return this.http.put<any>(`${this.apiUrl}/UpdateDestinationAdapterInstance`, {
      interfaceName,
      adapterInstanceGuid,
      instanceName,
      isEnabled,
      configuration
    });
  }

  updateSqlTransactionProperties(
    interfaceName: string,
    useTransaction?: boolean,
    batchSize?: number
  ): Observable<any> {
    return this.http.put<any>(`${this.apiUrl}/UpdateSqlTransactionProperties`, {
      interfaceName,
      useTransaction,
      batchSize
    });
  }
}



