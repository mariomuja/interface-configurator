import { Component, OnInit, OnDestroy, AfterViewInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatSort, MatSortModule, Sort } from '@angular/material/sort';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBarModule, MatSnackBar } from '@angular/material/snack-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { FormsModule } from '@angular/forms';
import { AdapterCardComponent } from '../adapter-card/adapter-card.component';
import { AdapterPropertiesDialogComponent, AdapterPropertiesData } from '../adapter-properties-dialog/adapter-properties-dialog.component';
import { DestinationInstancesDialogComponent, DestinationAdapterInstance } from '../destination-instances-dialog/destination-instances-dialog.component';
import { TransportService } from '../../services/transport.service';
import { TranslationService } from '../../services/translation.service';
import { CsvRecord, SqlRecord, ProcessLog } from '../../models/data.model';
import { interval, Subscription, firstValueFrom } from 'rxjs';
import { switchMap } from 'rxjs/operators';

@Component({
  selector: 'app-transport',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatTableModule,
    MatSortModule,
    MatButtonModule,
    MatCardModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatSlideToggleModule,
    MatIconModule,
    MatChipsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatExpansionModule,
    MatTooltipModule,
    MatTableModule,
    MatDialogModule,
    AdapterCardComponent,
    DestinationInstancesDialogComponent
  ],
  templateUrl: './transport.component.html',
  styleUrl: './transport.component.css'
})
export class TransportComponent implements OnInit, OnDestroy, AfterViewInit {
  @ViewChild(MatSort) sort!: MatSort;
  
  csvData: CsvRecord[] = [];
  editableCsvText: string = '';
  sqlData: SqlRecord[] = [];
  processLogs: ProcessLog[] = [];
  logDataSource = new MatTableDataSource<ProcessLog & { component?: string }>([]);
  isLoading = false;
  isTransporting = false;
  isDiagnosing = false;
  diagnosticsResult: any = null;
  interfaceConfigurations: any[] = [];
  currentInterfaceName: string = '';
  sourceInstanceName: string = 'Source';
  destinationInstanceName: string = 'Destination';
  destinationAdapterInstances: any[] = [];
  sourceIsEnabled: boolean = true;
  destinationIsEnabled: boolean = true;
  sourceReceiveFolder: string = '';
  sourceFileMask: string = '*.txt';
  sourceBatchSize: number = 100;
  sourceFieldSeparator: string = '║';
  destinationReceiveFolder: string = '';
  destinationFileMask: string = '*.txt';
  sourceAdapterInstanceGuid: string = '';
  destinationAdapterInstanceGuid: string = '';
  // SQL Server properties (shared for source and destination)
  sqlServerName: string = '';
  sqlDatabaseName: string = '';
  sqlUserName: string = '';
  sqlPassword: string = '';
  sqlIntegratedSecurity: boolean = false;
  sqlResourceGroup: string = '';
  sqlPollingStatement: string = '';
  sqlPollingInterval: number = 60;
  sqlUseTransaction: boolean = false;
  sqlBatchSize: number = 1000;
  sourceCardExpanded: boolean = true;
  destinationCardExpanded: boolean = true;
  isRestartingSource: boolean = false;
  isRestartingDestination: boolean = false;
  sourceAdapterName: 'CSV' | 'SqlServer' = 'CSV';
  private refreshSubscription?: Subscription;
  private lastErrorShown: string = '';
  private errorShownCount: number = 0;
  
  selectedComponent: string = 'all';
  availableComponents: string[] = ['all', 'Azure Function', 'Blob Storage', 'SQL Server', 'Vercel API'];
  
  readonly DEFAULT_INTERFACE_NAME = 'FromCsvToSqlServerExample';

  csvDisplayedColumns: string[] = []; // Will be populated dynamically from CSV data
  sqlDisplayedColumns: string[] = []; // Will be populated dynamically from SQL data
  logDisplayedColumns: string[] = ['timestamp', 'level', 'component', 'message', 'details'];

  constructor(
    private transportService: TransportService,
    private translationService: TranslationService,
    private snackBar: MatSnackBar,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    this.loadSampleCsvData();
    this.loadSqlData();
    this.loadProcessLogs();
    this.loadInterfaceConfigurations();
    this.loadDestinationAdapterInstances();
    this.startAutoRefresh();
    
    // Subscribe to language changes to update UI
    this.translationService.getCurrentLanguage().subscribe(() => {
      // Trigger change detection - translations will be updated via getTranslation calls
    });
  }
  
  loadInterfaceConfigurations(): void {
    this.transportService.getInterfaceConfigurations().subscribe({
      next: (configs) => {
        this.interfaceConfigurations = configs || [];
        // Update current interface name and instance names from configuration
        const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
        if (defaultConfig) {
          this.currentInterfaceName = defaultConfig.interfaceName;
          this.sourceInstanceName = defaultConfig.sourceInstanceName || 'Source';
          this.destinationInstanceName = defaultConfig.destinationInstanceName || 'Destination';
          this.sourceIsEnabled = defaultConfig.sourceIsEnabled ?? true;
          this.destinationIsEnabled = defaultConfig.destinationIsEnabled ?? true;
          this.sourceAdapterName = (defaultConfig.sourceAdapterName === 'SqlServer' ? 'SqlServer' : 'CSV') as 'CSV' | 'SqlServer';
          this.sourceReceiveFolder = defaultConfig.sourceReceiveFolder || '';
          this.sourceFileMask = defaultConfig.sourceFileMask || '*.txt';
          this.sourceBatchSize = defaultConfig.sourceBatchSize ?? 100;
          this.sourceFieldSeparator = defaultConfig.sourceFieldSeparator || '║';
          this.destinationReceiveFolder = defaultConfig.destinationReceiveFolder || '';
          this.destinationFileMask = defaultConfig.destinationFileMask || '*.txt';
          this.sourceAdapterInstanceGuid = defaultConfig.sourceAdapterInstanceGuid || '';
          this.destinationAdapterInstanceGuid = defaultConfig.destinationAdapterInstanceGuid || '';
          // SQL Server properties
          this.sqlServerName = defaultConfig.sqlServerName || '';
          this.sqlDatabaseName = defaultConfig.sqlDatabaseName || '';
          this.sqlUserName = defaultConfig.sqlUserName || '';
          this.sqlPassword = defaultConfig.sqlPassword || '';
          this.sqlIntegratedSecurity = defaultConfig.sqlIntegratedSecurity ?? false;
          this.sqlResourceGroup = defaultConfig.sqlResourceGroup || '';
          this.sqlPollingStatement = defaultConfig.sqlPollingStatement || '';
          this.sqlPollingInterval = defaultConfig.sqlPollingInterval ?? 60;
          this.sqlUseTransaction = defaultConfig.sqlUseTransaction ?? false;
          this.sqlBatchSize = defaultConfig.sqlBatchSize ?? 1000;
        } else if (!this.currentInterfaceName) {
          // Set default name if no configuration exists yet
          this.currentInterfaceName = this.DEFAULT_INTERFACE_NAME;
          this.sourceInstanceName = 'Source';
          this.destinationInstanceName = 'Destination';
          this.sourceIsEnabled = true;
          this.destinationIsEnabled = true;
          this.sourceAdapterName = 'CSV';
          this.sourceReceiveFolder = '';
          this.sourceFileMask = '*.txt';
          this.sourceBatchSize = 100;
          this.sourceFieldSeparator = '║';
          this.destinationReceiveFolder = '';
          this.destinationFileMask = '*.txt';
          this.sourceAdapterInstanceGuid = '';
          this.destinationAdapterInstanceGuid = '';
          // SQL Server properties
          this.sqlServerName = '';
          this.sqlDatabaseName = '';
          this.sqlUserName = '';
          this.sqlPassword = '';
          this.sqlIntegratedSecurity = false;
          this.sqlResourceGroup = '';
          this.sqlPollingStatement = '';
          this.sqlPollingInterval = 60;
        }
      },
      error: (error) => {
        console.error('Error loading interface configurations:', error);
        console.error('Full error object:', JSON.stringify(error, null, 2));
        
        // Extract detailed error message with all available information
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Laden der Interface-Konfigurationen');
        
        // Show as warning (less intrusive) since this is not critical for basic functionality
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
      }
    });
  }
  
  getTranslation(key: string): string {
    return this.translationService.translate(key);
  }
  
  ngAfterViewInit(): void {
    if (this.sort) {
      this.logDataSource.sort = this.sort;
    }
    this.setupResizableColumns();
  }
  
  setupResizableColumns(): void {
    // Wait for table to render
    setTimeout(() => {
      const table = document.querySelector('.resizable-table');
      if (!table) return;
      
      const headers = table.querySelectorAll('.resizable-header');
      headers.forEach((header, index) => {
        const handle = header.querySelector('.resize-handle') as HTMLElement;
        if (!handle) return;
        
        let startX = 0;
        let startWidth = 0;
        let isResizing = false;
        
        const startResize = (e: MouseEvent) => {
          isResizing = true;
          startX = e.pageX;
          const th = header as HTMLElement;
          startWidth = th.offsetWidth;
          document.body.style.cursor = 'col-resize';
          document.body.style.userSelect = 'none';
          e.preventDefault();
        };
        
        const doResize = (e: MouseEvent) => {
          if (!isResizing) return;
          const diff = e.pageX - startX;
          const newWidth = Math.max(100, startWidth + diff);
          (header as HTMLElement).style.width = `${newWidth}px`;
          
          // Update all cells in this column
          const cells = table.querySelectorAll(`td:nth-child(${index + 1})`);
          cells.forEach(cell => {
            (cell as HTMLElement).style.width = `${newWidth}px`;
          });
        };
        
        const stopResize = () => {
          if (isResizing) {
            isResizing = false;
            document.body.style.cursor = '';
            document.body.style.userSelect = '';
          }
        };
        
        handle.addEventListener('mousedown', startResize);
        document.addEventListener('mousemove', doResize);
        document.addEventListener('mouseup', stopResize);
      });
    }, 100);
  }

  ngOnDestroy(): void {
    if (this.refreshSubscription) {
      this.refreshSubscription.unsubscribe();
    }
  }

  loadSampleCsvData(): void {
    this.isLoading = true;
    this.transportService.getSampleCsvData().subscribe({
      next: (data) => {
        this.csvData = data;
        this.editableCsvText = this.formatCsvAsText();
        // Extract columns dynamically from CSV data
        if (data && data.length > 0) {
          this.csvDisplayedColumns = this.extractColumns(data);
        }
        this.isLoading = false;
      },
      error: (error) => {
        console.error('Error loading CSV data:', error);
        console.error('Full error object:', JSON.stringify(error, null, 2));
        
        // Extract detailed error message with all available information
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Laden der CSV-Daten');
        
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 15000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
        this.isLoading = false;
      }
    });
  }

  loadSqlData(): void {
    this.transportService.getSqlData().subscribe({
      next: (data) => {
        this.sqlData = data;
        // Extract columns dynamically from SQL data
        if (data && data.length > 0) {
          // Always include id and datetime_created/createdAt, then add all CSV columns
          const columns = this.extractColumns(data);
          // Ensure id is first, then CSV columns, then datetime_created/createdAt last
          const idColumn = columns.find(c => c.toLowerCase() === 'id');
          const dateColumns = columns.filter(c => 
            c.toLowerCase() === 'datetime_created' || 
            c.toLowerCase() === 'createdat' || 
            c.toLowerCase() === 'created_at'
          );
          const csvColumns = columns.filter(c => 
            c.toLowerCase() !== 'id' && 
            c.toLowerCase() !== 'datetime_created' && 
            c.toLowerCase() !== 'createdat' &&
            c.toLowerCase() !== 'created_at' &&
            c.toLowerCase() !== 'error'
          );
          
          this.sqlDisplayedColumns = [
            ...(idColumn ? [idColumn] : []),
            ...csvColumns,
            ...(dateColumns.length > 0 ? [dateColumns[0]] : [])
          ];
        }
        // Reset error tracking on success
        this.lastErrorShown = '';
        this.errorShownCount = 0;
      },
      error: (error) => {
        console.error('Error loading SQL data:', error);
        console.error('Full error object:', JSON.stringify(error, null, 2));
        
        // Extract detailed error message with all available information
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Laden der SQL-Daten');
        
        // Create unique error identifier
        const errorKey = `sql-data:${error.status}:${error.error?.message || error.message || 'unknown'}`;
        
        // Only show error popup if it's a different error or hasn't been shown too many times
        if (errorKey !== this.lastErrorShown || this.errorShownCount < 3) {
          this.snackBar.open(detailedMessage, 'Schließen', { 
            duration: 15000, // Longer duration for detailed messages
            panelClass: ['error-snackbar'],
            verticalPosition: 'top',
            horizontalPosition: 'center'
          });
          
          if (errorKey === this.lastErrorShown) {
            this.errorShownCount++;
          } else {
            this.lastErrorShown = errorKey;
            this.errorShownCount = 1;
          }
        }
      }
    });
  }

  /**
   * Extracts detailed error information from HTTP error responses
   * Returns a formatted error message with all available details
   */
  private extractDetailedErrorMessage(error: any, defaultMessage: string): string {
    const parts: string[] = [];
    
    // HTTP Status Code and Status Text
    if (error.status) {
      parts.push(`HTTP Status: ${error.status}`);
    }
    if (error.statusText) {
      parts.push(`Status Text: ${error.statusText}`);
    }
    
    // Error Code
    if (error.error?.code) {
      parts.push(`Code: ${error.error.code}`);
    } else if (error.code) {
      parts.push(`Code: ${error.code}`);
    }
    
    // Main Error Message
    let mainMessage = defaultMessage;
    if (error.error?.message) {
      mainMessage = error.error.message;
    } else if (error.error?.error) {
      mainMessage = error.error.error;
    } else if (error.message) {
      mainMessage = error.message;
    }
    parts.push(`Fehler: ${mainMessage}`);
    
    // Details
    if (error.error?.details) {
      parts.push(`Details: ${error.error.details}`);
    }
    
    // URL that was accessed
    if (error.error?.url) {
      parts.push(`URL: ${error.error.url}`);
    }
    
    // Request ID (if available from Azure)
    if (error.error?.requestId) {
      parts.push(`Request ID: ${error.error.requestId}`);
    }
    
    // Additional error information
    if (error.error?.errorMessage) {
      parts.push(`Error Message: ${error.error.errorMessage}`);
    }
    
    // Stack trace (only first few lines to avoid overwhelming the user)
    if (error.error?.stack) {
      const stackLines = error.error.stack.split('\n').slice(0, 3).join('\n');
      parts.push(`Stack Trace:\n${stackLines}`);
    }
    
    // Full error object as JSON (for debugging, truncated)
    if (error.error && Object.keys(error.error).length > 0) {
      try {
        const errorJson = JSON.stringify(error.error, null, 2);
        if (errorJson.length > 500) {
          parts.push(`Full Error (truncated):\n${errorJson.substring(0, 500)}...`);
        } else {
          parts.push(`Full Error:\n${errorJson}`);
        }
      } catch (e) {
        // Ignore JSON serialization errors
      }
    }
    
    return parts.join('\n');
  }

  loadProcessLogs(): void {
    this.transportService.getProcessLogs().subscribe({
      next: (logs) => {
        // Enrich logs with component information
        const enrichedLogs = logs.map(log => ({
          ...log,
          component: this.extractComponent(log.message, log.details)
        }));
        
        this.processLogs = enrichedLogs.sort((a, b) => 
          new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()
        );
        
        this.updateLogDataSource();
        // Reset error tracking on success
        this.lastErrorShown = '';
        this.errorShownCount = 0;
      },
      error: (error) => {
        console.error('Error loading process logs:', error);
        console.error('Full error object:', JSON.stringify(error, null, 2));
        
        // Extract detailed error message with all available information
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Laden der Prozess-Logs');
        
        // Create unique error identifier
        const errorKey = `process-logs:${error.status}:${error.error?.message || error.message || 'unknown'}`;
        
        // Only show error popup if it's a different error or hasn't been shown too many times
        if (errorKey !== this.lastErrorShown || this.errorShownCount < 3) {
          this.snackBar.open(detailedMessage, 'Schließen', { 
            duration: 15000, // Longer duration for detailed messages
            panelClass: ['error-snackbar'],
            verticalPosition: 'top',
            horizontalPosition: 'center'
          });
          
          if (errorKey === this.lastErrorShown) {
            this.errorShownCount++;
          } else {
            this.lastErrorShown = errorKey;
            this.errorShownCount = 1;
          }
        }
      }
    });
  }
  
  extractComponent(message: string, details?: string): string {
    const text = `${message} ${details || ''}`.toLowerCase();
    
    if (text.includes('azure function') || text.includes('function triggered') || text.includes('chunk')) {
      return 'Azure Function';
    } else if (text.includes('blob storage') || text.includes('blob') || text.includes('csv file detected')) {
      return 'Blob Storage';
    } else if (text.includes('sql server') || text.includes('database') || text.includes('transaction') || text.includes('connection')) {
      return 'SQL Server';
    } else if (text.includes('vercel') || text.includes('api')) {
      return 'Vercel API';
    }
    
    return 'Unknown';
  }
  
  updateLogDataSource(): void {
    let filtered = [...this.processLogs];
    
    if (this.selectedComponent && this.selectedComponent !== 'all') {
      filtered = filtered.filter(log => log.component === this.selectedComponent);
    }
    
    this.logDataSource.data = filtered;
    this.logDataSource.sort = this.sort;
  }
  
  onComponentFilterChange(): void {
    this.updateLogDataSource();
  }
  
  announceSortChange(sortState: Sort): void {
    // This is called when sort changes
  }

  startTransport(): void {
    this.isTransporting = true;
    
    // Ensure interface configuration exists before starting transport
    const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
    
    if (!defaultConfig) {
      // Create default interface configuration
      this.transportService.createInterfaceConfiguration({
        interfaceName: this.DEFAULT_INTERFACE_NAME,
        sourceAdapterName: 'CSV',
        sourceConfiguration: JSON.stringify({ source: 'csv-files/csv-incoming' }),
        destinationAdapterName: 'SqlServer',
        destinationConfiguration: JSON.stringify({ destination: 'TransportData' }),
        description: 'Default CSV to SQL Server interface'
      }).subscribe({
        next: () => {
          this.loadInterfaceConfigurations();
          this.uploadAndStartTransport();
        },
        error: (error) => {
          console.error('Error creating interface configuration:', error);
          console.error('Full error object:', JSON.stringify(error, null, 2));
          
          // Extract detailed error message with all available information
          const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Erstellen der Interface-Konfiguration');
          
          // Show error but continue anyway (non-blocking)
          this.snackBar.open(detailedMessage + '\n\nTransport wird trotzdem gestartet...', 'OK', { 
            duration: 12000,
            panelClass: ['error-snackbar'],
            verticalPosition: 'top',
            horizontalPosition: 'center'
          });
          
          this.uploadAndStartTransport();
        }
      });
    } else {
      // Ensure both Source and Destination are enabled
      if (!defaultConfig.sourceIsEnabled || !defaultConfig.destinationIsEnabled) {
        // Enable Source if disabled
        if (!defaultConfig.sourceIsEnabled) {
          this.transportService.toggleInterfaceConfiguration(this.DEFAULT_INTERFACE_NAME, 'Source', true).subscribe({
            next: () => {
              this.loadInterfaceConfigurations();
              // Continue to enable Destination if needed
              if (!defaultConfig.destinationIsEnabled) {
                this.transportService.toggleInterfaceConfiguration(this.DEFAULT_INTERFACE_NAME, 'Destination', true).subscribe({
                  next: () => {
                    this.loadInterfaceConfigurations();
                    this.uploadAndStartTransport();
                  },
                  error: (error) => {
                    console.error('Error enabling Destination adapter:', error);
                    const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktivieren des Destination-Adapters');
                    this.snackBar.open(detailedMessage + '\n\nTransport wird trotzdem gestartet...', 'OK', { 
                      duration: 12000,
                      panelClass: ['error-snackbar'],
                      verticalPosition: 'top',
                      horizontalPosition: 'center'
                    });
                    this.uploadAndStartTransport();
                  }
                });
              } else {
                this.uploadAndStartTransport();
              }
            },
            error: (error) => {
              console.error('Error enabling Source adapter:', error);
              const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktivieren des Source-Adapters');
              this.snackBar.open(detailedMessage + '\n\nTransport wird trotzdem gestartet...', 'OK', { 
                duration: 12000,
                panelClass: ['error-snackbar'],
                verticalPosition: 'top',
                horizontalPosition: 'center'
              });
              this.uploadAndStartTransport();
            }
          });
        } else if (!defaultConfig.destinationIsEnabled) {
          // Only Destination needs to be enabled
          this.transportService.toggleInterfaceConfiguration(this.DEFAULT_INTERFACE_NAME, 'Destination', true).subscribe({
            next: () => {
              this.loadInterfaceConfigurations();
              this.uploadAndStartTransport();
            },
            error: (error) => {
              console.error('Error enabling Destination adapter:', error);
              const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktivieren des Destination-Adapters');
              this.snackBar.open(detailedMessage + '\n\nTransport wird trotzdem gestartet...', 'OK', { 
                duration: 12000,
                panelClass: ['error-snackbar'],
                verticalPosition: 'top',
                horizontalPosition: 'center'
              });
              this.uploadAndStartTransport();
            }
          });
        }
      } else {
        this.uploadAndStartTransport();
      }
    }
  }
  
  private uploadAndStartTransport(): void {
    // Use edited CSV text if available, otherwise use formatted CSV from csvData
    const csvContent = this.editableCsvText || this.formatCsvAsText();
    this.transportService.startTransport(csvContent).subscribe({
      next: (response) => {
        this.snackBar.open('Transport gestartet: ' + response.message, 'Schließen', { duration: 5000 });
        this.isTransporting = false;
        // Refresh immediately - auto-refresh (every 5 seconds) will pick up changes as they happen
        // The timer functions run every minute, so data will appear within 1-2 minutes
        // But we refresh immediately to show any existing data and start monitoring
        this.loadSqlData();
        this.loadProcessLogs();
        this.loadInterfaceConfigurations();
      },
      error: (error) => {
        console.error('Error starting transport:', error);
        console.error('Full error object:', JSON.stringify(error, null, 2));
        this.isTransporting = false;
        
        // Extract detailed error message with all available information
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Starten des Transports');
        
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 15000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
        this.isTransporting = false;
      }
    });
  }

  dropTable(): void {
    const confirmMessage = this.translationService.translate('table.drop.confirm');
    if (confirm(confirmMessage)) {
      this.transportService.dropTable().subscribe({
        next: (response) => {
          this.snackBar.open(response.message, 'Schließen', { duration: 5000 });
          // Clear SQL data and columns - table structure will be recreated from next CSV
          this.sqlData = [];
          this.sqlDisplayedColumns = [];
          this.loadProcessLogs();
        },
        error: (error) => {
          console.error('Error dropping table:', error);
          console.error('Full error object:', JSON.stringify(error, null, 2));
          
          // Extract detailed error message with all available information
          const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Löschen der Tabelle');
          
          this.snackBar.open(detailedMessage, 'Schließen', { 
            duration: 15000,
            panelClass: ['error-snackbar'],
            verticalPosition: 'top',
            horizontalPosition: 'center'
          });
        }
      });
    }
  }

  clearLogs(): void {
    const confirmMessage = this.translationService.translate('log.clear.confirm');
    if (confirm(confirmMessage)) {
      this.transportService.clearLogs().subscribe({
        next: (response) => {
          this.snackBar.open(response.message, 'Schließen', { duration: 3000 });
          this.loadProcessLogs();
        },
        error: (error) => {
          console.error('Error clearing logs:', error);
          console.error('Full error object:', JSON.stringify(error, null, 2));
          
          // Extract detailed error message with all available information
          const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Leeren der Protokolltabelle');
          
          this.snackBar.open(detailedMessage, 'Schließen', { 
            duration: 15000,
            panelClass: ['error-snackbar'],
            verticalPosition: 'top',
            horizontalPosition: 'center'
          });
        }
      });
    }
  }

  runDiagnostics(): void {
    this.isDiagnosing = true;
    this.diagnosticsResult = null;
    
    this.transportService.diagnose().subscribe({
      next: (result) => {
        this.diagnosticsResult = result;
        this.isDiagnosing = false;
        
        // Show summary in snackbar
        const summary = result.summary;
        const message = `Diagnose abgeschlossen: ${summary.passed}/${summary.totalChecks} Checks erfolgreich`;
        this.snackBar.open(message, 'OK', { duration: 5000 });
        
        // Log details to console
        console.log('Diagnostics Result:', result);
      },
      error: (error) => {
        console.error('Error running diagnostics:', error);
        console.error('Full error object:', JSON.stringify(error, null, 2));
        this.isDiagnosing = false;
        
        // Extract detailed error message with all available information
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler bei der Diagnose');
        
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 15000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
      }
    });
  }

  private startAutoRefresh(): void {
    // Refresh every 3 seconds to catch changes immediately after they're written
    // This ensures the UI updates as soon as data appears in TransportData or ProcessLogs
    this.refreshSubscription = interval(3000).subscribe(() => {
      this.loadSqlData();
      this.loadProcessLogs();
      this.loadInterfaceConfigurations();
    });
  }
  
  getDefaultInterfaceStatus(): { exists: boolean; sourceEnabled: boolean; destinationEnabled: boolean } {
    const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
    return {
      exists: !!defaultConfig,
      sourceEnabled: defaultConfig?.sourceIsEnabled ?? false,
      destinationEnabled: defaultConfig?.destinationIsEnabled ?? false
    };
  }

  updateInterfaceName(): void {
    if (!this.currentInterfaceName || this.currentInterfaceName.trim() === '') {
      this.snackBar.open('Interface-Name darf nicht leer sein', 'OK', { duration: 3000 });
      // Restore previous name
      const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
      this.currentInterfaceName = defaultConfig?.interfaceName || this.DEFAULT_INTERFACE_NAME;
      return;
    }

    const trimmedName = this.currentInterfaceName.trim();
    const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
    
    if (!defaultConfig) {
      // Configuration doesn't exist yet, will be created when transport starts
      return;
    }

    if (trimmedName === defaultConfig.interfaceName) {
      // No change
      return;
    }

    // Update interface name via API
    this.transportService.updateInterfaceName(this.DEFAULT_INTERFACE_NAME, trimmedName).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
        this.snackBar.open('Interface-Name aktualisiert', 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating interface name:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren des Interface-Namens');
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
        // Restore previous name
        this.currentInterfaceName = defaultConfig.interfaceName;
      }
    });
  }

  updateSourceInstanceName(): void {
    const trimmedName = this.sourceInstanceName.trim() || 'Source';
    const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
    
    if (!defaultConfig) {
      return;
    }

    if (trimmedName === defaultConfig.sourceInstanceName) {
      return;
    }

    this.transportService.updateInstanceName(this.DEFAULT_INTERFACE_NAME, 'Source', trimmedName).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
      },
      error: (error) => {
        console.error('Error updating source instance name:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren des Source-Instanz-Namens');
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
        // Restore previous name
        this.sourceInstanceName = defaultConfig.sourceInstanceName || 'Source';
      }
    });
  }

  updateDestinationInstanceName(name?: string): void {
    const trimmedName = (name || this.destinationInstanceName).trim() || 'Destination';
    const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
    
    if (!defaultConfig) {
      return;
    }

    if (trimmedName === defaultConfig.destinationInstanceName) {
      return;
    }

    this.destinationInstanceName = trimmedName; // Update immediately for responsive UI

    this.transportService.updateInstanceName(this.DEFAULT_INTERFACE_NAME, 'Destination', trimmedName).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
      },
      error: (error) => {
        console.error('Error updating destination instance name:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren des Destination-Instanz-Namens');
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
        // Restore previous name
        this.destinationInstanceName = defaultConfig.destinationInstanceName || 'Destination';
      }
    });
  }

  onSourceEnabledChange(): void {
    const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
    
    if (!defaultConfig) {
      // Restore previous value
      this.sourceIsEnabled = true;
      return;
    }

    if (this.sourceIsEnabled === defaultConfig.sourceIsEnabled) {
      return;
    }

    this.transportService.toggleInterfaceConfiguration(this.DEFAULT_INTERFACE_NAME, 'Source', this.sourceIsEnabled).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
        this.snackBar.open(
          `Source adapter ${this.sourceIsEnabled ? 'aktiviert' : 'deaktiviert'}. ${this.sourceIsEnabled ? 'Der Prozess wird beim nächsten Timer-Trigger gestartet.' : 'Der Prozess stoppt sofort.'}`,
          'OK',
          { duration: 5000 }
        );
      },
      error: (error) => {
        console.error('Error toggling source adapter:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktivieren/Deaktivieren des Source-Adapters');
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
        // Restore previous value
        this.sourceIsEnabled = defaultConfig.sourceIsEnabled ?? true;
      }
    });
  }

  onDestinationEnabledChange(): void {
    const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
    
    if (!defaultConfig) {
      // Restore previous value
      this.destinationIsEnabled = true;
      return;
    }

    if (this.destinationIsEnabled === defaultConfig.destinationIsEnabled) {
      return;
    }

    this.transportService.toggleInterfaceConfiguration(this.DEFAULT_INTERFACE_NAME, 'Destination', this.destinationIsEnabled).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
        this.snackBar.open(
          `Destination adapter ${this.destinationIsEnabled ? 'aktiviert' : 'deaktiviert'}. ${this.destinationIsEnabled ? 'Der Prozess wird beim nächsten Timer-Trigger gestartet.' : 'Der Prozess stoppt sofort.'}`,
          'OK',
          { duration: 5000 }
        );
      },
      error: (error) => {
        console.error('Error toggling destination adapter:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktivieren/Deaktivieren des Destination-Adapters');
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
        // Restore previous value
        this.destinationIsEnabled = defaultConfig.destinationIsEnabled ?? true;
      }
    });
  }

  restartSourceAdapter(): void {
    this.isRestartingSource = true;
    this.transportService.restartAdapter(this.DEFAULT_INTERFACE_NAME, 'Source').subscribe({
      next: (response) => {
        this.isRestartingSource = false;
        this.snackBar.open(response.message || 'Source adapter wird neu gestartet...', 'OK', { duration: 5000 });
        this.loadInterfaceConfigurations();
      },
      error: (error) => {
        this.isRestartingSource = false;
        console.error('Error restarting source adapter:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Neustarten des Source-Adapters');
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
      }
    });
  }

  restartDestinationAdapter(): void {
    this.isRestartingDestination = true;
    this.transportService.restartAdapter(this.DEFAULT_INTERFACE_NAME, 'Destination').subscribe({
      next: (response) => {
        this.isRestartingDestination = false;
        this.snackBar.open(response.message || 'Destination adapter wird neu gestartet...', 'OK', { duration: 5000 });
        this.loadInterfaceConfigurations();
      },
      error: (error) => {
        this.isRestartingDestination = false;
        console.error('Error restarting destination adapter:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Neustarten des Destination-Adapters');
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
      }
    });
  }

  updateReceiveFolder(folder?: string): void {
    const folderValue = folder || this.sourceReceiveFolder;
    const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
    
    if (!defaultConfig) {
      return;
    }

    if (folderValue === (defaultConfig.sourceReceiveFolder || '')) {
      return;
    }

    this.sourceReceiveFolder = folderValue; // Update immediately for responsive UI

    this.transportService.updateReceiveFolder(this.DEFAULT_INTERFACE_NAME, folderValue).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
        this.snackBar.open('Receive Folder aktualisiert', 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating receive folder:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren des Receive Folders');
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
        // Restore previous value
        this.sourceReceiveFolder = defaultConfig.sourceReceiveFolder || '';
      }
    });
  }

  updateFileMask(fileMask?: string): void {
    const fileMaskValue = fileMask || this.sourceFileMask;
    const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
    
    if (!defaultConfig) {
      return;
    }

    const normalizedMask = fileMaskValue.trim() || '*.txt';
    if (normalizedMask === (defaultConfig.sourceFileMask || '*.txt')) {
      return;
    }

    this.sourceFileMask = normalizedMask; // Update immediately for responsive UI

    this.transportService.updateFileMask(this.DEFAULT_INTERFACE_NAME, normalizedMask).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
        this.snackBar.open('File Mask aktualisiert', 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating file mask:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren der File Mask');
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
        // Restore previous value
        this.sourceFileMask = defaultConfig.sourceFileMask || '*.txt';
      }
    });
  }

  updateBatchSize(batchSize?: number): void {
    const batchSizeValue = batchSize ?? this.sourceBatchSize;
    const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
    
    if (!defaultConfig) {
      return;
    }

    const normalizedBatchSize = batchSizeValue > 0 ? batchSizeValue : 100;
    if (normalizedBatchSize === (defaultConfig.sourceBatchSize ?? 100)) {
      return;
    }

    this.sourceBatchSize = normalizedBatchSize; // Update immediately for responsive UI

    this.transportService.updateBatchSize(this.DEFAULT_INTERFACE_NAME, normalizedBatchSize).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
        this.snackBar.open('Batch Size aktualisiert', 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating batch size:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren der Batch Size');
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
        // Restore previous value
        this.sourceBatchSize = defaultConfig.sourceBatchSize ?? 100;
      }
    });
  }

  openSourceAdapterSettings(): void {
    const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
    const dialogData: AdapterPropertiesData = {
      adapterType: 'Source',
      adapterName: this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME)?.sourceAdapterName === 'SqlServer' ? 'SqlServer' : 'CSV',
      instanceName: this.sourceInstanceName,
      isEnabled: this.sourceIsEnabled,
      receiveFolder: this.sourceReceiveFolder,
      fileMask: this.sourceFileMask,
      batchSize: this.sourceBatchSize,
      fieldSeparator: this.sourceFieldSeparator,
      sqlServerName: this.sqlServerName,
      sqlDatabaseName: this.sqlDatabaseName,
      sqlUserName: this.sqlUserName,
      sqlPassword: this.sqlPassword,
      sqlIntegratedSecurity: this.sqlIntegratedSecurity,
      sqlResourceGroup: this.sqlResourceGroup,
      sqlPollingStatement: this.sqlPollingStatement,
      sqlPollingInterval: this.sqlPollingInterval,
      adapterInstanceGuid: this.sourceAdapterInstanceGuid
    };

    const dialogRef = this.dialog.open(AdapterPropertiesDialogComponent, {
      width: '600px',
      data: dialogData
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        // Update instance name if changed
        if (result.instanceName !== this.sourceInstanceName) {
          this.sourceInstanceName = result.instanceName;
          this.updateSourceInstanceName(result.instanceName);
        }

        // Update enabled status if changed
        if (result.isEnabled !== this.sourceIsEnabled) {
          this.sourceIsEnabled = result.isEnabled;
          this.onSourceEnabledChange();
        }

        // Update receive folder if changed (only for CSV adapters)
        if (result.receiveFolder !== undefined && result.receiveFolder !== this.sourceReceiveFolder) {
          this.sourceReceiveFolder = result.receiveFolder;
          this.updateReceiveFolder(result.receiveFolder);
        }

        // Update file mask if changed (only for CSV adapters)
        if (result.fileMask !== undefined && result.fileMask !== this.sourceFileMask) {
          this.sourceFileMask = result.fileMask;
          this.updateFileMask(result.fileMask);
        }

        // Update batch size if changed (only for CSV adapters)
        if (result.batchSize !== undefined && result.batchSize !== this.sourceBatchSize) {
          this.sourceBatchSize = result.batchSize;
          this.updateBatchSize(result.batchSize);
        }

        // Update SQL Server connection properties if changed
        if (result.sqlServerName !== undefined || result.sqlDatabaseName !== undefined || 
            result.sqlUserName !== undefined || result.sqlPassword !== undefined ||
            result.sqlIntegratedSecurity !== undefined || result.sqlResourceGroup !== undefined) {
          this.updateSqlConnectionProperties(
            result.sqlServerName,
            result.sqlDatabaseName,
            result.sqlUserName,
            result.sqlPassword,
            result.sqlIntegratedSecurity,
            result.sqlResourceGroup
          );
        }

        // Update SQL Server polling properties if changed (only for source adapters)
        if (result.sqlPollingStatement !== undefined || result.sqlPollingInterval !== undefined) {
          this.updateSqlPollingProperties(result.sqlPollingStatement, result.sqlPollingInterval);
        }

        // Update field separator if changed (only for CSV adapters)
        if (result.fieldSeparator !== undefined && result.fieldSeparator !== this.sourceFieldSeparator) {
          this.sourceFieldSeparator = result.fieldSeparator;
          this.updateFieldSeparator(result.fieldSeparator);
        }
      }
    });
  }

  openDestinationAdapterSettings(): void {
    const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
    const dialogData: AdapterPropertiesData = {
      adapterType: 'Destination',
      adapterName: this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME)?.destinationAdapterName === 'CSV' ? 'CSV' : 'SqlServer',
      instanceName: this.destinationInstanceName,
      isEnabled: this.destinationIsEnabled,
      receiveFolder: this.destinationReceiveFolder,
      fileMask: this.destinationFileMask,
      fieldSeparator: this.sourceFieldSeparator,
      destinationReceiveFolder: this.destinationReceiveFolder,
      destinationFileMask: this.destinationFileMask,
      sqlServerName: this.sqlServerName,
      sqlDatabaseName: this.sqlDatabaseName,
      sqlUserName: this.sqlUserName,
      sqlPassword: this.sqlPassword,
      sqlIntegratedSecurity: this.sqlIntegratedSecurity,
      sqlResourceGroup: this.sqlResourceGroup,
      adapterInstanceGuid: this.destinationAdapterInstanceGuid
    };

    const dialogRef = this.dialog.open(AdapterPropertiesDialogComponent, {
      width: '600px',
      data: dialogData
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        // Update instance name if changed
        if (result.instanceName !== this.destinationInstanceName) {
          this.destinationInstanceName = result.instanceName;
          this.updateDestinationInstanceName(result.instanceName);
        }

        // Update enabled status if changed
        if (result.isEnabled !== this.destinationIsEnabled) {
          this.destinationIsEnabled = result.isEnabled;
          this.onDestinationEnabledChange();
        }

        // Update SQL Server connection properties if changed
        if (result.sqlServerName !== undefined || result.sqlDatabaseName !== undefined || 
            result.sqlUserName !== undefined || result.sqlPassword !== undefined ||
            result.sqlIntegratedSecurity !== undefined || result.sqlResourceGroup !== undefined) {
          this.updateSqlConnectionProperties(
            result.sqlServerName,
            result.sqlDatabaseName,
            result.sqlUserName,
            result.sqlPassword,
            result.sqlIntegratedSecurity,
            result.sqlResourceGroup
          );
        }

        // Update field separator if changed (only for CSV adapters)
        if (result.fieldSeparator !== undefined && result.fieldSeparator !== this.sourceFieldSeparator) {
          this.sourceFieldSeparator = result.fieldSeparator;
          this.updateFieldSeparator(result.fieldSeparator);
        }

        // Update destination receive folder if changed (only for CSV destination adapters)
        if (result.destinationReceiveFolder !== undefined && result.destinationReceiveFolder !== this.destinationReceiveFolder) {
          this.destinationReceiveFolder = result.destinationReceiveFolder;
          this.updateDestinationReceiveFolder(result.destinationReceiveFolder);
        }

        // Update destination file mask if changed (only for CSV destination adapters)
        if (result.destinationFileMask !== undefined && result.destinationFileMask !== this.destinationFileMask) {
          this.destinationFileMask = result.destinationFileMask;
          this.updateDestinationFileMask(result.destinationFileMask);
        }
      }
    });
  }

  updateFieldSeparator(fieldSeparator?: string): void {
    const fieldSeparatorValue = fieldSeparator ?? this.sourceFieldSeparator;
    const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
    
    if (!defaultConfig) {
      return;
    }

    const normalizedSeparator = fieldSeparatorValue.trim() || '║';
    if (normalizedSeparator === (defaultConfig.sourceFieldSeparator || '║')) {
      return;
    }

    this.sourceFieldSeparator = normalizedSeparator; // Update immediately for responsive UI

    this.transportService.updateFieldSeparator(this.DEFAULT_INTERFACE_NAME, normalizedSeparator).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
        this.snackBar.open('Field Separator aktualisiert', 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating field separator:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren des Field Separators');
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
        // Restore previous value
        this.sourceFieldSeparator = defaultConfig.sourceFieldSeparator || '║';
      }
    });
  }

  updateDestinationReceiveFolder(destinationReceiveFolder?: string): void {
    const destinationReceiveFolderValue = destinationReceiveFolder ?? this.destinationReceiveFolder;
    const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
    
    if (!defaultConfig) {
      return;
    }

    const normalizedFolder = destinationReceiveFolderValue.trim() || '';
    if (normalizedFolder === (defaultConfig.destinationReceiveFolder || '')) {
      return;
    }

    this.destinationReceiveFolder = normalizedFolder; // Update immediately for responsive UI

    this.transportService.updateDestinationReceiveFolder(this.DEFAULT_INTERFACE_NAME, normalizedFolder).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
        this.snackBar.open('Destination Receive Folder aktualisiert', 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating destination receive folder:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren des Destination Receive Folders');
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
        // Restore previous value
        this.destinationReceiveFolder = defaultConfig.destinationReceiveFolder || '';
      }
    });
  }

  updateDestinationFileMask(destinationFileMask?: string): void {
    const destinationFileMaskValue = destinationFileMask ?? this.destinationFileMask;
    const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
    
    if (!defaultConfig) {
      return;
    }

    const normalizedMask = destinationFileMaskValue.trim() || '*.txt';
    if (normalizedMask === (defaultConfig.destinationFileMask || '*.txt')) {
      return;
    }

    this.destinationFileMask = normalizedMask; // Update immediately for responsive UI

    this.transportService.updateDestinationFileMask(this.DEFAULT_INTERFACE_NAME, normalizedMask).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
        this.snackBar.open('Destination File Mask aktualisiert', 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating destination file mask:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren der Destination File Mask');
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
        // Restore previous value
        this.destinationFileMask = defaultConfig.destinationFileMask || '*.txt';
      }
    });
  }

  loadDestinationAdapterInstances(): void {
    this.transportService.getDestinationAdapterInstances(this.DEFAULT_INTERFACE_NAME).subscribe({
      next: (instances) => {
        this.destinationAdapterInstances = instances || [];
        // If no instances exist, create a default one from legacy properties for backward compatibility
        if (this.destinationAdapterInstances.length === 0) {
          const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
          if (defaultConfig && defaultConfig.destinationAdapterName) {
            this.destinationAdapterInstances = [{
              adapterInstanceGuid: defaultConfig.destinationAdapterInstanceGuid || this.generateGuid(),
              instanceName: defaultConfig.destinationInstanceName || 'Destination',
              adapterName: defaultConfig.destinationAdapterName,
              isEnabled: defaultConfig.destinationIsEnabled ?? true,
              configuration: defaultConfig.destinationConfiguration || '{}'
            }];
          }
        }
      },
      error: (error) => {
        console.error('Error loading destination adapter instances:', error);
        this.destinationAdapterInstances = [];
      }
    });
  }

  openDestinationInstancesDialog(): void {
    const dialogRef = this.dialog.open(DestinationInstancesDialogComponent, {
      width: '800px',
      maxHeight: '90vh',
      data: {
        instances: this.destinationAdapterInstances,
        availableAdapters: [
          { name: 'CSV', displayName: 'CSV', icon: 'description' },
          { name: 'SqlServer', displayName: 'SQL Server', icon: 'storage' }
        ]
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        if (result.action === 'save') {
          // Save all instances
          this.saveDestinationAdapterInstances(result.instances);
        } else if (result.action === 'settings') {
          // Open settings for a specific instance
          this.openDestinationInstanceSettings(result.instance);
        }
      }
    });
  }

  saveDestinationAdapterInstances(instances: DestinationAdapterInstance[]): void {
    const currentInstances = this.destinationAdapterInstances;
    const newInstances = instances || [];
    
    // Find instances to add (exist in new but not in current)
    const toAdd = newInstances.filter(newInst => 
      !currentInstances.some(curr => curr.adapterInstanceGuid === newInst.adapterInstanceGuid)
    );
    
    // Find instances to remove (exist in current but not in new)
    const toRemove = currentInstances.filter(curr => 
      !newInstances.some(newInst => newInst.adapterInstanceGuid === curr.adapterInstanceGuid)
    );
    
    // Find instances to update (exist in both but may have changed)
    const toUpdate = newInstances.filter(newInst => {
      const curr = currentInstances.find(c => c.adapterInstanceGuid === newInst.adapterInstanceGuid);
      if (!curr) return false;
      return curr.instanceName !== newInst.instanceName || 
             curr.isEnabled !== newInst.isEnabled ||
             JSON.stringify(curr.configuration) !== JSON.stringify(newInst.configuration);
    });
    
    // Execute operations
    const operations: Promise<any>[] = [];
    
    // Add new instances
    toAdd.forEach(instance => {
      operations.push(
        firstValueFrom(
          this.transportService.addDestinationAdapterInstance(
            this.DEFAULT_INTERFACE_NAME,
            instance.adapterName,
            instance.instanceName,
            JSON.stringify(instance.configuration || {})
          )
        ).catch(error => {
          console.error(`Error adding destination adapter instance ${instance.instanceName}:`, error);
          return null;
        })
      );
    });
    
    // Remove deleted instances
    toRemove.forEach(instance => {
      operations.push(
        firstValueFrom(
          this.transportService.removeDestinationAdapterInstance(
            this.DEFAULT_INTERFACE_NAME,
            instance.adapterInstanceGuid
          )
        ).catch(error => {
          console.error(`Error removing destination adapter instance ${instance.instanceName}:`, error);
          return null;
        })
      );
    });
    
    // Update changed instances
    toUpdate.forEach(instance => {
      operations.push(
        firstValueFrom(
          this.transportService.updateDestinationAdapterInstance(
            this.DEFAULT_INTERFACE_NAME,
            instance.adapterInstanceGuid,
            instance.instanceName,
            instance.isEnabled,
            JSON.stringify(instance.configuration || {})
          )
        ).catch(error => {
          console.error(`Error updating destination adapter instance ${instance.instanceName}:`, error);
          return null;
        })
      );
    });
    
    // Wait for all operations to complete
    Promise.all(operations).then(() => {
      this.loadDestinationAdapterInstances();
      this.snackBar.open(
        `Destination adapter instances ${toAdd.length > 0 || toRemove.length > 0 || toUpdate.length > 0 ? 'updated' : 'unchanged'}`,
        'OK',
        { duration: 3000 }
      );
    }).catch(error => {
      console.error('Error saving destination adapter instances:', error);
      const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Speichern der Destination Adapter Instances');
      this.snackBar.open(detailedMessage, 'Schließen', {
        duration: 10000,
        panelClass: ['error-snackbar'],
        verticalPosition: 'top',
        horizontalPosition: 'center'
      });
      this.loadDestinationAdapterInstances(); // Reload to restore previous state
    });
  }

  openDestinationInstanceSettings(instance: DestinationAdapterInstance): void {
    const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
    const dialogData: AdapterPropertiesData = {
      adapterType: 'Destination',
      adapterName: instance.adapterName,
      instanceName: instance.instanceName,
      isEnabled: instance.isEnabled,
      adapterInstanceGuid: instance.adapterInstanceGuid,
      receiveFolder: instance.adapterName === 'CSV' ? (defaultConfig?.destinationReceiveFolder || '') : undefined,
      fileMask: instance.adapterName === 'CSV' ? (defaultConfig?.destinationFileMask || '*.txt') : undefined,
      fieldSeparator: defaultConfig?.sourceFieldSeparator || '║',
      destinationReceiveFolder: instance.adapterName === 'CSV' ? (defaultConfig?.destinationReceiveFolder || '') : undefined,
      destinationFileMask: instance.adapterName === 'CSV' ? (defaultConfig?.destinationFileMask || '*.txt') : undefined,
      sqlServerName: defaultConfig?.sqlServerName || '',
      sqlDatabaseName: defaultConfig?.sqlDatabaseName || '',
      sqlUserName: defaultConfig?.sqlUserName || '',
      sqlPassword: defaultConfig?.sqlPassword || '',
      sqlIntegratedSecurity: defaultConfig?.sqlIntegratedSecurity ?? false,
      sqlResourceGroup: defaultConfig?.sqlResourceGroup || ''
    };

    const dialogRef = this.dialog.open(AdapterPropertiesDialogComponent, {
      width: '600px',
      data: dialogData
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        // Update the instance
        this.updateDestinationInstance(instance.adapterInstanceGuid, result);
      }
    });
  }

  updateDestinationInstance(instanceGuid: string, properties: any): void {
    const instance = this.destinationAdapterInstances.find(i => i.adapterInstanceGuid === instanceGuid);
    if (!instance) {
      console.error(`Destination adapter instance ${instanceGuid} not found`);
      return;
    }
    
    // Build configuration JSON if needed (for CSV adapters)
    let configuration = instance.configuration || {};
    if (properties.destinationReceiveFolder !== undefined && instance.adapterName === 'CSV') {
      configuration = { ...configuration, destination: properties.destinationReceiveFolder };
    }
    
    // Update instance properties
    this.transportService.updateDestinationAdapterInstance(
      this.DEFAULT_INTERFACE_NAME,
      instanceGuid,
      properties.instanceName || instance.instanceName,
      properties.isEnabled !== undefined ? properties.isEnabled : instance.isEnabled,
      JSON.stringify(configuration)
    ).subscribe({
      next: () => {
        this.loadDestinationAdapterInstances();
        this.snackBar.open('Destination adapter instance updated', 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating destination adapter instance:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren der Destination Adapter Instance');
        this.snackBar.open(detailedMessage, 'Schließen', {
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
        this.loadDestinationAdapterInstances(); // Reload to restore previous state
      }
    });
  }

  getAdapterIcon(adapterName: 'CSV' | 'SqlServer'): string {
    return adapterName === 'CSV' ? 'description' : 'storage';
  }

  generateGuid(): string {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
      const r = Math.random() * 16 | 0;
      const v = c === 'x' ? r : (r & 0x3 | 0x8);
      return v.toString(16);
    });
  }

  getLevelColor(level: string): string {
    switch (level) {
      case 'error': return 'warn';
      case 'warning': return 'accent';
      default: return 'primary';
    }
  }

  // Use a seldom-used UTF-8 character as field separator: ║ (Box Drawing Double Vertical Line, U+2551)
  private readonly FIELD_SEPARATOR = '║';

  /**
   * Format CSV data as text (for display in Courier New)
   */
  formatCsvAsText(): string {
    if (!this.csvData || this.csvData.length === 0) {
      return this.translationService.translate('no.data.csv');
    }

    // Get all columns
    const columns = this.extractColumns(this.csvData);
    
    // Build header row
    const headerRow = columns.join(this.FIELD_SEPARATOR);
    
    // Build data rows
    const dataRows = this.csvData.map(row => {
      return columns.map(col => {
        const value = this.getCellValue(row, col);
        const valueStr = String(value || '');
        // Escape separator and quotes in values
        if (valueStr.includes(this.FIELD_SEPARATOR) || valueStr.includes('"')) {
          return `"${valueStr.replace(/"/g, '""')}"`;
        }
        return valueStr;
      }).join(this.FIELD_SEPARATOR);
    });
    
    // Combine header and data rows
    return [headerRow, ...dataRows].join('\n');
  }

  /**
   * Handle CSV text changes from editable textarea
   */
  onCsvTextChange(): void {
    // Parse the edited CSV text back into csvData
    if (!this.editableCsvText || this.editableCsvText.trim() === '') {
      this.csvData = [];
      return;
    }

    try {
      const lines = this.editableCsvText.split('\n').filter(line => line.trim() !== '');
      if (lines.length === 0) {
        this.csvData = [];
        return;
      }

      // Parse header row using the field separator
      const headers = this.parseCsvLine(lines[0]).map(h => h.trim().replace(/^"|"$/g, ''));
      
      // Parse data rows
      const records: CsvRecord[] = [];
      for (let i = 1; i < lines.length; i++) {
        const values = this.parseCsvLine(lines[i]);
        const record: any = {};
        headers.forEach((header, index) => {
          record[header] = values[index] || '';
        });
        records.push(record);
      }
      
      this.csvData = records;
    } catch (error) {
      console.error('Error parsing CSV text:', error);
      // Keep the text but show a warning
      this.snackBar.open('CSV-Format könnte ungültig sein. Bitte überprüfen Sie die Syntax.', 'OK', {
        duration: 3000
      });
    }
  }

  /**
   * Parse a CSV line handling quoted values and custom field separator
   */
  private parseCsvLine(line: string): string[] {
    const values: string[] = [];
    let current = '';
    let inQuotes = false;
    
    for (let i = 0; i < line.length; i++) {
      const char = line[i];
      const nextChar = line[i + 1];
      
      if (char === '"') {
        if (inQuotes && nextChar === '"') {
          // Escaped quote
          current += '"';
          i++; // Skip next quote
        } else {
          // Toggle quote state
          inQuotes = !inQuotes;
        }
      } else if (char === this.FIELD_SEPARATOR && !inQuotes) {
        // End of value (using custom separator)
        values.push(current.trim());
        current = '';
      } else {
        current += char;
      }
    }
    
    // Add last value
    values.push(current.trim());
    
    return values;
  }

  /**
   * Extract column names dynamically from data records
   */
  extractColumns(data: any[]): string[] {
    if (!data || data.length === 0) {
      return [];
    }

    // Collect all unique keys from all records
    const allKeys = new Set<string>();
    data.forEach(record => {
      if (record && typeof record === 'object') {
        Object.keys(record).forEach(key => {
          // Exclude error and internal fields
          if (key !== 'error' && !key.startsWith('_')) {
            allKeys.add(key);
          }
        });
      }
    });

    return Array.from(allKeys);
  }

  /**
   * Get display value for a cell (handles different data types)
   */
  getCellValue(row: any, column: string): any {
    const value = row[column];
    if (value === null || value === undefined) {
      return '';
    }
    return value;
  }

  /**
   * Check if a column should be formatted as a number
   */
  isNumericColumn(column: string, data: any[]): boolean {
    if (!data || data.length === 0) return false;
    
    // Check first few non-null values
    for (const row of data.slice(0, 10)) {
      const value = row[column];
      if (value !== null && value !== undefined && value !== '') {
        return typeof value === 'number' || !isNaN(Number(value));
      }
    }
    return false;
  }

  /**
   * Get column display name (capitalize first letter, replace underscores)
   */
  getColumnDisplayName(column: string): string {
    return column
      .replace(/_/g, ' ')
      .replace(/([A-Z])/g, ' $1')
      .trim()
      .split(' ')
      .map(word => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
      .join(' ');
  }

  /**
   * Check if a column is a date column
   */
  isDateColumn(column: string): boolean {
    const lower = column.toLowerCase();
    return lower === 'datetime_created' || lower === 'createdat' || lower === 'created_at' || lower === 'timestamp';
  }

  /**
   * Check if details contain exception information
   */
  hasExceptionDetails(details: string): boolean {
    if (!details) return false;
    return details.includes('Type:') || 
           details.includes('Message:') || 
           details.includes('StackTrace:') ||
           details.includes('Source:') ||
           details.includes('TargetSite:') ||
           details.includes('Full Details:');
  }

  /**
   * Extract exception summary (first line or error message)
   */
  getExceptionSummary(details: string): string {
    if (!details) return '';
    
    // Try to extract the error message
    const messageMatch = details.match(/Message:\s*(.+?)(?:\n|Source:|$)/i);
    if (messageMatch && messageMatch[1]) {
      return messageMatch[1].trim();
    }
    
    // Try to extract "Full Details:" line
    const fullDetailsMatch = details.match(/Full Details:\s*(.+?)(?:\n|$)/i);
    if (fullDetailsMatch && fullDetailsMatch[1]) {
      return fullDetailsMatch[1].trim();
    }
    
    // Return first line
    const firstLine = details.split('\n')[0];
    return firstLine.length > 100 ? firstLine.substring(0, 100) + '...' : firstLine;
  }

  /**
   * Track expanded exception details
   */
  private expandedExceptions = new Set<number>();

  /**
   * Check if exception details are expanded
   */
  isExceptionExpanded(row: ProcessLog): boolean {
    return this.expandedExceptions.has(row.id);
  }

  /**
   * Toggle exception details expansion
   */
  toggleExceptionDetails(row: ProcessLog): void {
    if (this.expandedExceptions.has(row.id)) {
      this.expandedExceptions.delete(row.id);
    } else {
      this.expandedExceptions.add(row.id);
    }
  }
}


