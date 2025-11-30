import { Injectable } from '@angular/core';
import { Observable, BehaviorSubject } from 'rxjs';
import { TransportService } from './transport.service';
import { MatSnackBar } from '@angular/material/snack-bar';

export interface InterfaceConfiguration {
  interfaceName: string;
  sourceAdapterName?: string;
  destinationAdapterName?: string;
  sourceInstanceName?: string;
  destinationInstanceName?: string;
  sourceIsEnabled?: boolean;
  destinationIsEnabled?: boolean;
  sourceAdapterInstanceGuid?: string;
  destinationAdapterInstanceGuid?: string;
  sourceReceiveFolder?: string;
  sourceFileMask?: string;
  sourceBatchSize?: number;
  sourceFieldSeparator?: string;
  csvPollingInterval?: number;
  destinationReceiveFolder?: string;
  destinationFileMask?: string;
  csvData?: string;
  CsvData?: string;
  sources?: { [key: string]: any };
  destinations?: { [key: string]: any };
  _isPlaceholder?: boolean;
  [key: string]: any;
}

@Injectable({
  providedIn: 'root'
})
export class InterfaceManagementService {
  private readonly DEFAULT_INTERFACE_NAME = 'FromCsvToSqlServerExample';
  
  private interfacesSubject = new BehaviorSubject<InterfaceConfiguration[]>([]);
  public interfaces$ = this.interfacesSubject.asObservable();
  
  private currentInterfaceSubject = new BehaviorSubject<string>('');
  public currentInterface$ = this.currentInterfaceSubject.asObservable();

  constructor(
    private transportService: TransportService,
    private snackBar: MatSnackBar
  ) {}

  getDefaultInterfaceName(): string {
    return this.DEFAULT_INTERFACE_NAME;
  }

  getCurrentInterface(): string {
    return this.currentInterfaceSubject.value;
  }

  setCurrentInterface(interfaceName: string): void {
    this.currentInterfaceSubject.next(interfaceName);
  }

  getInterfaces(): InterfaceConfiguration[] {
    return this.interfacesSubject.value;
  }

  getInterface(interfaceName?: string): InterfaceConfiguration | undefined {
    const name = interfaceName || this.getCurrentInterface();
    return this.interfacesSubject.value.find(c => c.interfaceName === name);
  }

  loadInterfaces(): Observable<InterfaceConfiguration[]> {
    return new Observable(observer => {
      this.transportService.getInterfaceConfigurations().subscribe({
        next: (configs) => {
          // Filter out any entries with empty/null interface names
          let allConfigs = (configs || []).filter(config => 
            config && config.interfaceName && config.interfaceName.trim().length > 0
          );
          
          // Remove duplicates: if both placeholder and real exist for same name, keep only the real one
          const seenNames = new Set<string>();
          const uniqueConfigs: InterfaceConfiguration[] = [];
          
          // First pass: add all real (non-placeholder) interfaces
          for (const config of allConfigs) {
            if (!config._isPlaceholder && !seenNames.has(config.interfaceName)) {
              uniqueConfigs.push(config);
              seenNames.add(config.interfaceName);
            }
          }
          
          // Second pass: add placeholders only if no real interface with same name exists
          for (const config of allConfigs) {
            if (config._isPlaceholder && !seenNames.has(config.interfaceName)) {
              uniqueConfigs.push(config);
              seenNames.add(config.interfaceName);
            }
          }
          
          // Sort interfaces alphabetically for a stable dropdown order
          const sortedConfigs = uniqueConfigs.sort((a, b) => 
            a.interfaceName.localeCompare(b.interfaceName, undefined, { sensitivity: 'base' })
          );
          
          this.interfacesSubject.next(sortedConfigs);
          observer.next(sortedConfigs);
          observer.complete();
        },
        error: (error) => {
          console.error('Error loading interface configurations:', error);
          observer.error(error);
        }
      });
    });
  }

  ensureDefaultInterface(): Observable<InterfaceConfiguration | null> {
    return new Observable(observer => {
      const interfaces = this.getInterfaces();
      const defaultExists = interfaces.some(c => 
        c.interfaceName === this.DEFAULT_INTERFACE_NAME && !c._isPlaceholder
      );
      const defaultPlaceholderExists = interfaces.some(c => 
        c.interfaceName === this.DEFAULT_INTERFACE_NAME && c._isPlaceholder
      );
      
      if (defaultExists) {
        observer.next(null);
        observer.complete();
        return;
      }

      if (!defaultPlaceholderExists) {
        // Add placeholder first so it appears in dropdown immediately
        const placeholder: InterfaceConfiguration = {
          interfaceName: this.DEFAULT_INTERFACE_NAME,
          sourceAdapterName: 'CSV',
          destinationAdapterName: 'SqlServer',
          sourceInstanceName: 'Source',
          destinationInstanceName: 'Destination',
          sourceIsEnabled: false,
          destinationIsEnabled: false,
          csvPollingInterval: 10,
          _isPlaceholder: true
        };
        
        const updatedInterfaces = [placeholder, ...interfaces];
        this.interfacesSubject.next(updatedInterfaces);
      }
      
      // Then attempt to create the interface in the backend
      this.transportService.createInterfaceConfiguration({
        interfaceName: this.DEFAULT_INTERFACE_NAME,
        sourceAdapterName: 'CSV',
        sourceConfiguration: JSON.stringify({ source: 'csv-files/csv-incoming' }),
        destinationAdapterName: 'SqlServer',
        destinationConfiguration: JSON.stringify({ destination: 'TransportData' }),
        description: 'Default CSV to SQL Server interface'
      }).subscribe({
        next: (createdConfig) => {
          // Reload to get the real one (will replace placeholder)
          this.loadInterfaces().subscribe({
            next: () => {
              observer.next(createdConfig);
              observer.complete();
            },
            error: (err) => {
              observer.error(err);
            }
          });
        },
        error: (error) => {
          console.error('Error creating default interface:', error);
          observer.next(null);
          observer.complete();
        }
      });
    });
  }

  createInterface(interfaceName: string): Observable<InterfaceConfiguration> {
    return new Observable(observer => {
      this.transportService.createInterfaceConfiguration({
        interfaceName: interfaceName,
        sourceAdapterName: 'CSV',
        destinationAdapterName: 'SqlServer'
      }).subscribe({
        next: (createdConfig) => {
          // Normalize the config to match our expected format
          const normalizedConfig: InterfaceConfiguration = {
            interfaceName: createdConfig.interfaceName || createdConfig.InterfaceName || interfaceName,
            sourceAdapterName: createdConfig.sourceAdapterName || createdConfig.SourceAdapterName || 'CSV',
            destinationAdapterName: createdConfig.destinationAdapterName || createdConfig.DestinationAdapterName || 'SqlServer',
            sourceInstanceName: createdConfig.sourceInstanceName || createdConfig.SourceInstanceName || 'Source',
            destinationInstanceName: createdConfig.destinationInstanceName || createdConfig.DestinationInstanceName || 'Destination',
            sourceIsEnabled: createdConfig.sourceIsEnabled !== undefined ? createdConfig.sourceIsEnabled : 
                            (createdConfig.SourceIsEnabled !== undefined ? createdConfig.SourceIsEnabled : false),
            destinationIsEnabled: createdConfig.destinationIsEnabled !== undefined ? createdConfig.destinationIsEnabled :
                                 (createdConfig.DestinationIsEnabled !== undefined ? createdConfig.DestinationIsEnabled : false),
            _isPlaceholder: false
          };
          
          // Handle new hierarchical structure (Sources/Destinations)
          if (createdConfig.sources || createdConfig.Sources) {
            const sources = createdConfig.sources || createdConfig.Sources || {};
            const sourceKeys = Object.keys(sources);
            if (sourceKeys.length > 0) {
              const firstSource = sources[sourceKeys[0]];
              normalizedConfig.sourceInstanceName = firstSource.instanceName || firstSource.InstanceName || sourceKeys[0];
              normalizedConfig.sourceAdapterName = firstSource.adapterName || firstSource.AdapterName || 'CSV';
              normalizedConfig.sourceIsEnabled = firstSource.isEnabled !== undefined ? firstSource.isEnabled :
                                                (firstSource.IsEnabled !== undefined ? firstSource.IsEnabled : false);
            }
          }
          
          if (createdConfig.destinations || createdConfig.Destinations) {
            const destinations = createdConfig.destinations || createdConfig.Destinations || {};
            const destKeys = Object.keys(destinations);
            if (destKeys.length > 0) {
              const firstDest = destinations[destKeys[0]];
              normalizedConfig.destinationInstanceName = firstDest.instanceName || firstDest.InstanceName || destKeys[0];
              normalizedConfig.destinationAdapterName = firstDest.adapterName || firstDest.AdapterName || 'SqlServer';
              normalizedConfig.destinationIsEnabled = firstDest.isEnabled !== undefined ? firstDest.isEnabled :
                                                     (firstDest.IsEnabled !== undefined ? firstDest.IsEnabled : false);
            }
          }
          
          // Add to list and sort
          const updatedInterfaces = [
            ...this.getInterfaces().filter(c => c.interfaceName !== interfaceName || !c._isPlaceholder),
            normalizedConfig
          ].sort((a, b) => a.interfaceName.localeCompare(b.interfaceName, undefined, { sensitivity: 'base' }));
          
          this.interfacesSubject.next(updatedInterfaces);
          observer.next(normalizedConfig);
          observer.complete();
        },
        error: (error) => {
          observer.error(error);
        }
      });
    });
  }

  deleteInterface(interfaceName: string): Observable<void> {
    return new Observable(observer => {
      this.transportService.deleteInterfaceConfiguration(interfaceName).subscribe({
        next: () => {
          const updatedInterfaces = this.getInterfaces().filter(c => c.interfaceName !== interfaceName);
          this.interfacesSubject.next(updatedInterfaces);
          
          // If deleted interface was current, clear it
          if (this.getCurrentInterface() === interfaceName) {
            this.setCurrentInterface('');
          }
          
          observer.next();
          observer.complete();
        },
        error: (error) => {
          observer.error(error);
        }
      });
    });
  }

  updateInterfaceName(oldName: string, newName: string): Observable<void> {
    return new Observable(observer => {
      this.transportService.updateInterfaceName(oldName, newName).subscribe({
        next: () => {
          const interfaces = this.getInterfaces();
          const index = interfaces.findIndex(c => c.interfaceName === oldName);
          if (index >= 0) {
            interfaces[index].interfaceName = newName;
            this.interfacesSubject.next([...interfaces]);
            
            // Update current interface if it was the renamed one
            if (this.getCurrentInterface() === oldName) {
              this.setCurrentInterface(newName);
            }
          }
          
          observer.next();
          observer.complete();
        },
        error: (error) => {
          observer.error(error);
        }
      });
    });
  }

  validateInterface(interfaceName: string): boolean {
    const config = this.getInterface(interfaceName);
    if (!config) {
      return false;
    }

    // Check if source adapter exists
    const hasSource = config.sourceAdapterInstanceGuid && config.sourceAdapterInstanceGuid.trim().length > 0;
    
    // Check if there are destination adapters
    const hasDestinations = config.destinations && Object.keys(config.destinations).length > 0;
    
    // If there are destinations, source must exist
    if (hasDestinations && !hasSource) {
      return false;
    }

    return true;
  }
}











