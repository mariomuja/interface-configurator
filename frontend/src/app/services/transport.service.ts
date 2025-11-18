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

  getInterfaceConfigurations(): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}/interface-configurations`);
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



