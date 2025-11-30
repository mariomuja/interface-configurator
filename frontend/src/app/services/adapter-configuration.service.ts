import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { TransportService } from './transport.service';
import { MatSnackBar } from '@angular/material/snack-bar';

@Injectable({
  providedIn: 'root'
})
export class AdapterConfigurationService {
  constructor(
    private transportService: TransportService,
    private snackBar: MatSnackBar
  ) {}

  updateReceiveFolder(interfaceName: string, receiveFolder: string): Observable<any> {
    return this.transportService.updateReceiveFolder(interfaceName, receiveFolder);
  }

  updateFileMask(interfaceName: string, fileMask: string): Observable<any> {
    return this.transportService.updateFileMask(interfaceName, fileMask);
  }

  updateBatchSize(interfaceName: string, batchSize: number): Observable<any> {
    return this.transportService.updateBatchSize(interfaceName, batchSize);
  }

  updateFieldSeparator(interfaceName: string, fieldSeparator: string): Observable<any> {
    return this.transportService.updateFieldSeparator(interfaceName, fieldSeparator);
  }

  updateCsvPollingInterval(interfaceName: string, pollingInterval: number): Observable<any> {
    return this.transportService.updateCsvPollingInterval(interfaceName, pollingInterval);
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
    return this.transportService.updateSqlConnectionProperties(
      interfaceName,
      serverName,
      databaseName,
      userName,
      password,
      integratedSecurity,
      resourceGroup
    );
  }

  updateSqlPollingProperties(
    interfaceName: string,
    pollingStatement?: string,
    pollingInterval?: number
  ): Observable<any> {
    return this.transportService.updateSqlPollingProperties(interfaceName, pollingStatement, pollingInterval);
  }

  updateSqlTransactionProperties(
    interfaceName: string,
    useTransaction?: boolean,
    batchSize?: number
  ): Observable<any> {
    return this.transportService.updateSqlTransactionProperties(interfaceName, useTransaction, batchSize);
  }

  updateDestinationReceiveFolder(interfaceName: string, destinationReceiveFolder: string): Observable<any> {
    return this.transportService.updateDestinationReceiveFolder(interfaceName, destinationReceiveFolder);
  }

  updateDestinationFileMask(interfaceName: string, destinationFileMask: string): Observable<any> {
    return this.transportService.updateDestinationFileMask(interfaceName, destinationFileMask);
  }

  updateDestinationJQScriptFile(interfaceName: string, adapterInstanceGuid: string, jqScriptFile: string): Observable<any> {
    return this.transportService.updateDestinationJQScriptFile(interfaceName, adapterInstanceGuid, jqScriptFile);
  }

  updateDestinationSourceAdapterSubscription(
    interfaceName: string,
    adapterInstanceGuid: string,
    sourceAdapterSubscription: string
  ): Observable<any> {
    return this.transportService.updateDestinationSourceAdapterSubscription(
      interfaceName,
      adapterInstanceGuid,
      sourceAdapterSubscription
    );
  }

  updateDestinationSqlStatements(
    interfaceName: string,
    adapterInstanceGuid: string,
    insertStatement: string,
    updateStatement: string,
    deleteStatement: string
  ): Observable<any> {
    return this.transportService.updateDestinationSqlStatements(
      interfaceName,
      adapterInstanceGuid,
      insertStatement,
      updateStatement,
      deleteStatement
    );
  }

  updateSourceAdapterInstance(
    interfaceName: string,
    adapterInstanceGuid: string,
    instanceName?: string,
    isEnabled?: boolean,
    configuration?: string
  ): Observable<any> {
    return this.transportService.updateSourceAdapterInstance(
      interfaceName,
      adapterInstanceGuid,
      instanceName,
      isEnabled,
      configuration
    );
  }

  updateDestinationAdapterInstance(
    interfaceName: string,
    adapterInstanceGuid: string,
    instanceName?: string,
    isEnabled?: boolean,
    configuration?: string
  ): Observable<any> {
    return this.transportService.updateDestinationAdapterInstance(
      interfaceName,
      adapterInstanceGuid,
      instanceName,
      isEnabled,
      configuration
    );
  }

  updateSourceEnabled(interfaceName: string, adapterInstanceGuid: string, isEnabled: boolean): Observable<any> {
    return this.updateSourceAdapterInstance(interfaceName, adapterInstanceGuid, undefined, isEnabled);
  }

  updateDestinationEnabled(
    interfaceName: string,
    adapterInstanceGuid: string,
    isEnabled: boolean
  ): Observable<any> {
    return this.updateDestinationAdapterInstance(interfaceName, adapterInstanceGuid, undefined, isEnabled);
  }

  restartAdapter(interfaceName: string, adapterType: 'Source' | 'Destination'): Observable<any> {
    return this.transportService.restartAdapter(interfaceName, adapterType);
  }
}











