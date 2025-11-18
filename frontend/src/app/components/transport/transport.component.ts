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
import { InterfaceJsonViewDialogComponent } from '../interface-json-view-dialog/interface-json-view-dialog.component';
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
  @ViewChild('csvEditor', { static: false }) csvEditor?: any;
  
  csvData: CsvRecord[] = [];
  editableCsvText: string = '';
  formattedCsvHtml: string = '';
  sqlData: SqlRecord[] = [];
  processLogs: ProcessLog[] = [];
  logDataSource = new MatTableDataSource<ProcessLog & { component?: string }>([]);
  isLoading = false;
  isTransporting = false;
  isDiagnosing = false;
  diagnosticsResult: any = null;
  interfaceConfigurations: any[] = [];
  currentInterfaceName: string = '';
  selectedInterfaceConfig: any = null;
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
  private destinationCardExpandedStates: Map<string, boolean> = new Map();
  isRestartingSource: boolean = false;
  isRestartingDestination: boolean = false;
  sourceAdapterName: 'CSV' | 'SqlServer' = 'CSV';
  private refreshSubscription?: Subscription;
  private lastErrorShown: string = '';
  private errorShownCount: number = 0;
  
  selectedComponent: string = 'all';
  availableComponents: string[] = ['all', 'Azure Function', 'Blob Storage', 'SQL Server', 'Vercel API'];
  
  readonly DEFAULT_INTERFACE_NAME = 'FromCsvToSqlServerExample';
  
  // Track if table exists (based on whether we have columns loaded)
  tableExists: boolean = false;

  csvDisplayedColumns: string[] = []; // Will be populated dynamically from CSV data
  sqlDisplayedColumns: string[] = []; // Will be populated dynamically from SQL data
  logDisplayedColumns: string[] = ['timestamp', 'level', 'component', 'message', 'details'];

  constructor(
    private transportService: TransportService,
    private translationService: TranslationService,
    private snackBar: MatSnackBar,
    public dialog: MatDialog
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
        
        // If no current interface is set, select the first available or default
        if (!this.currentInterfaceName) {
          if (this.interfaceConfigurations.length > 0) {
            // Select first available interface
            this.currentInterfaceName = this.interfaceConfigurations[0].interfaceName;
            this.selectedInterfaceConfig = this.interfaceConfigurations[0];
            this.loadInterfaceData();
          } else {
            // No interfaces exist, use default name (will be created when needed)
            this.currentInterfaceName = this.DEFAULT_INTERFACE_NAME;
            this.selectedInterfaceConfig = null;
          }
        } else {
          // Current interface is set, verify it still exists
          const currentConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.currentInterfaceName);
          if (currentConfig) {
            this.selectedInterfaceConfig = currentConfig;
            this.loadInterfaceData();
          } else {
            // Current interface no longer exists, select first available or default
            if (this.interfaceConfigurations.length > 0) {
              this.currentInterfaceName = this.interfaceConfigurations[0].interfaceName;
              this.selectedInterfaceConfig = this.interfaceConfigurations[0];
              this.loadInterfaceData();
            } else {
              this.currentInterfaceName = this.DEFAULT_INTERFACE_NAME;
              this.selectedInterfaceConfig = null;
            }
          }
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
        // Use data if available, otherwise generate sample data locally
        if (!data || data.length === 0) {
          data = this.generateSampleCsvData();
        }
        
        this.csvData = data;
        const csvText = this.formatCsvAsText();
        this.editableCsvText = csvText;
        this.formattedCsvHtml = this.formatCsvAsHtml();
        
        // Update the contenteditable div with formatted HTML
        setTimeout(() => {
          if (this.csvEditor?.nativeElement) {
            this.csvEditor.nativeElement.innerHTML = this.formattedCsvHtml;
          }
        }, 0);
        
        // Extract columns dynamically from CSV data
        if (data && data.length > 0) {
          this.csvDisplayedColumns = this.extractColumns(data);
        }
        
        // Set CsvData property on adapter if interface is configured
        if (this.currentInterfaceName && csvText && csvText !== this.translationService.translate('no.data.csv')) {
          this.updateCsvDataProperty(csvText);
        }
        
        // Ensure field separator is set to ║
        if (this.sourceFieldSeparator !== '║') {
          this.updateFieldSeparator('║');
        }
        
        this.isLoading = false;
      },
      error: (error) => {
        console.error('Error loading CSV data:', error);
        // Generate sample data locally as fallback
        const fallbackData = this.generateSampleCsvData();
        this.csvData = fallbackData;
        const csvText = this.formatCsvAsText();
        this.editableCsvText = csvText;
        this.formattedCsvHtml = this.formatCsvAsHtml();
        
        // Update the contenteditable div with formatted HTML
        setTimeout(() => {
          if (this.csvEditor?.nativeElement) {
            this.csvEditor.nativeElement.innerHTML = this.formattedCsvHtml;
          }
        }, 0);
        
        // Extract columns dynamically from CSV data
        if (fallbackData && fallbackData.length > 0) {
          this.csvDisplayedColumns = this.extractColumns(fallbackData);
        }
        
        this.isLoading = false;
      }
    });
  }

  generateSampleCsvData(): CsvRecord[] {
    const data: CsvRecord[] = [];
    const names = ['Max Mustermann', 'Anna Schmidt', 'Peter Müller', 'Lisa Weber', 'Thomas Fischer'];
    const cities = ['Berlin', 'München', 'Hamburg', 'Köln', 'Frankfurt'];
    
    for (let i = 1; i <= 50; i++) {
      const name = names[Math.floor(Math.random() * names.length)];
      const city = cities[Math.floor(Math.random() * cities.length)];
      data.push({
        id: i,
        name: `${name} ${i}`,
        email: `user${i}@example.com`,
        age: Math.floor(Math.random() * 40) + 20,
        city: city,
        salary: Math.floor(Math.random() * 50000) + 30000
      } as CsvRecord);
    }
    
    return data;
  }
  
  /**
   * Update CsvData property on the adapter
   */
  updateCsvDataProperty(csvText: string): void {
    if (!this.currentInterfaceName) {
      return;
    }
    
    this.transportService.updateCsvData(this.currentInterfaceName, csvText).subscribe({
      next: () => {
        // Successfully updated CsvData property
        console.log('CsvData property updated successfully');
      },
      error: (error) => {
        console.error('Error updating CsvData property:', error);
        // Don't show error to user - this is a background operation
      }
    });
  }

  /**
   * Handle file selection for CSV data
   */
  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    
    if (!file) {
      return;
    }

    // Check file extension
    if (!file.name.toLowerCase().endsWith('.txt')) {
      this.snackBar.open('Bitte wählen Sie eine .txt Datei aus.', 'OK', {
        duration: 3000,
        panelClass: ['error-snackbar']
      });
      // Reset file input
      input.value = '';
      return;
    }

    const reader = new FileReader();
    reader.onload = (e) => {
      try {
        const content = e.target?.result as string;
        if (content) {
          this.editableCsvText = content;
          this.formattedCsvHtml = this.formatCsvAsHtml();
          // Update the contenteditable div with formatted HTML
          setTimeout(() => {
            if (this.csvEditor?.nativeElement) {
              this.csvEditor.nativeElement.innerHTML = this.formattedCsvHtml;
            }
          }, 0);
          // Trigger CSV text change handler to parse and update CsvData property
          this.onCsvTextChange();
          this.snackBar.open(`Datei "${file.name}" erfolgreich geladen.`, 'OK', {
            duration: 2000
          });
        }
      } catch (error) {
        console.error('Error reading file:', error);
        this.snackBar.open('Fehler beim Lesen der Datei.', 'OK', {
          duration: 3000,
          panelClass: ['error-snackbar']
        });
      }
      // Reset file input to allow selecting the same file again
      input.value = '';
    };

    reader.onerror = () => {
      this.snackBar.open('Fehler beim Lesen der Datei.', 'OK', {
        duration: 3000,
        panelClass: ['error-snackbar']
      });
      input.value = '';
    };

    reader.readAsText(file, 'UTF-8');
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
          // Table exists if we have columns (even if empty data)
          this.tableExists = this.sqlDisplayedColumns.length > 0;
        } else {
          // Check if we have columns even with empty data (table exists but is empty)
          // If sqlDisplayedColumns is empty, table likely doesn't exist
          this.tableExists = this.sqlDisplayedColumns.length > 0;
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
          this.tableExists = false;
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
    const confirmMessage = this.translationService.translate('log.clear.confirm') || 'Are you sure you want to clear all logs?';
    if (confirm(confirmMessage)) {
      this.isLoading = true;
      this.transportService.clearLogs().subscribe({
        next: (response) => {
          this.snackBar.open(response?.message || 'Logs cleared successfully', 'Schließen', { duration: 3000 });
          this.loadProcessLogs();
          this.isLoading = false;
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
          this.isLoading = false;
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

  updateSqlConnectionProperties(
    serverName?: string,
    databaseName?: string,
    userName?: string,
    password?: string,
    integratedSecurity?: boolean,
    resourceGroup?: string
  ): void {
    const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
    
    if (!defaultConfig) {
      return;
    }

    // Update local properties immediately for responsive UI
    if (serverName !== undefined) this.sqlServerName = serverName;
    if (databaseName !== undefined) this.sqlDatabaseName = databaseName;
    if (userName !== undefined) this.sqlUserName = userName;
    if (password !== undefined) this.sqlPassword = password;
    if (integratedSecurity !== undefined) this.sqlIntegratedSecurity = integratedSecurity;
    if (resourceGroup !== undefined) this.sqlResourceGroup = resourceGroup;

    this.transportService.updateSqlConnectionProperties(
      this.DEFAULT_INTERFACE_NAME,
      serverName,
      databaseName,
      userName,
      password,
      integratedSecurity,
      resourceGroup
    ).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
        this.snackBar.open('SQL Server Verbindungseigenschaften aktualisiert', 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating SQL connection properties:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren der SQL Server Verbindungseigenschaften');
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
        // Restore previous values
        this.loadInterfaceConfigurations();
      }
    });
  }

  updateSqlPollingProperties(
    pollingStatement?: string,
    pollingInterval?: number
  ): void {
    const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
    
    if (!defaultConfig) {
      return;
    }

    // Update local properties immediately for responsive UI
    if (pollingStatement !== undefined) this.sqlPollingStatement = pollingStatement;
    if (pollingInterval !== undefined) this.sqlPollingInterval = pollingInterval;

    this.transportService.updateSqlPollingProperties(
      this.DEFAULT_INTERFACE_NAME,
      pollingStatement,
      pollingInterval
    ).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
        this.snackBar.open('SQL Server Polling-Eigenschaften aktualisiert', 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating SQL polling properties:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren der SQL Server Polling-Eigenschaften');
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
        // Restore previous values
        this.loadInterfaceConfigurations();
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
          this.updateSourceInstanceName();
        }

        // Update enabled status if changed
        if (result.isEnabled !== undefined && result.isEnabled !== this.sourceIsEnabled) {
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

  getDestinationCardExpanded(instanceGuid: string): boolean {
    if (!this.destinationCardExpandedStates.has(instanceGuid)) {
      // Default to expanded state
      this.destinationCardExpandedStates.set(instanceGuid, true);
    }
    return this.destinationCardExpandedStates.get(instanceGuid) ?? true;
  }

  setDestinationCardExpanded(instanceGuid: string, expanded: boolean): void {
    this.destinationCardExpandedStates.set(instanceGuid, expanded);
  }
  
  /**
   * Check if SQL Server settings are complete
   * Settings are complete if:
   * - sqlServerName is set
   * - sqlDatabaseName is set
   * - If not using integrated security: sqlUserName and sqlPassword are set
   */
  areSqlSettingsComplete(): boolean {
    if (!this.sqlServerName || !this.sqlDatabaseName) {
      return false;
    }
    
    // If using integrated security, only server and database are required
    if (this.sqlIntegratedSecurity) {
      return true;
    }
    
    // If not using integrated security, username and password are required
    return !!(this.sqlUserName && this.sqlPassword);
  }
  
  /**
   * Check if the "Tabelle löschen" button should be disabled
   * Disabled if:
   * - Table does not exist, OR
   * - SQL settings are not complete
   */
  isDropTableDisabled(): boolean {
    return !this.tableExists || !this.areSqlSettingsComplete();
  }

  onInterfaceSelectionChange(event: any): void {
    const selectedName = event.value;
    const config = this.interfaceConfigurations.find(c => c.interfaceName === selectedName);
    if (config) {
      this.selectedInterfaceConfig = config;
      this.loadInterfaceData();
    }
  }

  loadInterfaceData(): void {
    if (!this.currentInterfaceName) return;
    
    const config = this.interfaceConfigurations.find(c => c.interfaceName === this.currentInterfaceName);
    if (config) {
      this.selectedInterfaceConfig = config;
      // Load all data for the selected interface
      this.sourceInstanceName = config.sourceInstanceName || 'Source';
      this.destinationInstanceName = config.destinationInstanceName || 'Destination';
      this.sourceIsEnabled = config.sourceIsEnabled ?? true;
      this.destinationIsEnabled = config.destinationIsEnabled ?? true;
      this.sourceAdapterName = (config.sourceAdapterName === 'SqlServer' ? 'SqlServer' : 'CSV') as 'CSV' | 'SqlServer';
      this.sourceReceiveFolder = config.sourceReceiveFolder || '';
      this.sourceFileMask = config.sourceFileMask || '*.txt';
      this.sourceBatchSize = config.sourceBatchSize ?? 100;
      this.sourceFieldSeparator = config.sourceFieldSeparator || '║';
      this.destinationReceiveFolder = config.destinationReceiveFolder || '';
      this.destinationFileMask = config.destinationFileMask || '*.txt';
      this.sourceAdapterInstanceGuid = config.sourceAdapterInstanceGuid || '';
      this.destinationAdapterInstanceGuid = config.destinationAdapterInstanceGuid || '';
      
      // Load SQL Server properties
      this.sqlServerName = config.sqlServerName || '';
      this.sqlDatabaseName = config.sqlDatabaseName || '';
      this.sqlUserName = config.sqlUserName || '';
      this.sqlPassword = config.sqlPassword || '';
      this.sqlIntegratedSecurity = config.sqlIntegratedSecurity ?? false;
      this.sqlResourceGroup = config.sqlResourceGroup || '';
      this.sqlPollingStatement = config.sqlPollingStatement || '';
      
      // Reload adapter instances and data
      this.loadDestinationAdapterInstances();
      this.loadSqlData();
      this.loadSampleCsvData();
    }
  }

  openAddInterfaceDialog(): void {
    // Simple prompt for now - can be replaced with a proper dialog component
    const interfaceName = prompt('Enter interface name:');
    if (!interfaceName || interfaceName.trim() === '') return;

    const trimmedName = interfaceName.trim();
    
    // Check if name already exists
    if (this.interfaceConfigurations.some(c => c.interfaceName === trimmedName)) {
      this.snackBar.open('Interface name already exists', 'OK', { duration: 3000 });
      return;
    }

    // Create new interface configuration
    this.transportService.createInterfaceConfiguration({
      interfaceName: trimmedName,
      sourceAdapterName: 'CSV',
      destinationAdapterName: 'SqlServer'
    }).subscribe({
      next: () => {
        this.snackBar.open('Interface created successfully', 'OK', { duration: 3000 });
        this.loadInterfaceConfigurations();
        this.currentInterfaceName = trimmedName;
        setTimeout(() => this.loadInterfaceData(), 500);
      },
      error: (error) => {
        console.error('Error creating interface:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Erstellen des Interfaces');
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
      }
    });
  }

  removeCurrentInterface(): void {
    if (!this.currentInterfaceName) return;
    
    const confirmMessage = `Are you sure you want to delete interface "${this.currentInterfaceName}"?`;
    if (!confirm(confirmMessage)) return;

    this.transportService.deleteInterfaceConfiguration(this.currentInterfaceName).subscribe({
      next: () => {
        this.snackBar.open('Interface deleted successfully', 'OK', { duration: 3000 });
        this.loadInterfaceConfigurations();
        // Select first available interface or default
        if (this.interfaceConfigurations.length > 0) {
          this.currentInterfaceName = this.interfaceConfigurations[0].interfaceName;
          setTimeout(() => this.loadInterfaceData(), 500);
        } else {
          this.currentInterfaceName = this.DEFAULT_INTERFACE_NAME;
          this.selectedInterfaceConfig = null;
        }
      },
      error: (error) => {
        console.error('Error deleting interface:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Löschen des Interfaces');
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
      }
    });
  }

  showInterfaceJson(): void {
    if (!this.currentInterfaceName) return;

    this.transportService.getInterfaceConfiguration(this.currentInterfaceName).subscribe({
      next: (config) => {
        // Also get destination adapter instances
        this.transportService.getDestinationAdapterInstances(this.currentInterfaceName).subscribe({
          next: (instances) => {
            const fullConfig = {
              ...config,
              destinationAdapterInstances: instances || []
            };
            this.openJsonViewDialog(fullConfig);
          },
          error: (error) => {
            console.error('Error loading destination adapter instances:', error);
            // Show dialog with just the interface config
            this.openJsonViewDialog(config);
          }
        });
      },
      error: (error) => {
        console.error('Error loading interface configuration:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Laden der Interface-Konfiguration');
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
      }
    });
  }

  private openJsonViewDialog(config: any): void {
    const jsonString = JSON.stringify(config, null, 2);
    const dialogRef = this.dialog.open(InterfaceJsonViewDialogComponent, {
      width: '800px',
      maxWidth: '90vw',
      data: { interfaceName: this.currentInterfaceName, jsonString: jsonString }
    });
  }

  loadDestinationAdapterInstances(): void {
    const interfaceName = this.currentInterfaceName || this.DEFAULT_INTERFACE_NAME;
    this.transportService.getDestinationAdapterInstances(interfaceName).subscribe({
      next: (instances) => {
        this.destinationAdapterInstances = instances || [];
        
        // Ensure SqlServer instances have TransportData configuration
        this.destinationAdapterInstances.forEach(instance => {
          if (instance.adapterName === 'SqlServer') {
            let config = instance.configuration || {};
            if (typeof config === 'string') {
              try {
                config = JSON.parse(config);
              } catch (e) {
                config = {};
              }
            }
            
            // Update if missing TransportData configuration
            if (!config.destination || config.destination !== 'TransportData') {
              config = {
                ...config,
                destination: 'TransportData',
                tableName: 'TransportData'
              };
              
              // Save updated configuration
              this.transportService.updateDestinationAdapterInstance(
                this.DEFAULT_INTERFACE_NAME,
                instance.adapterInstanceGuid,
                instance.instanceName,
                instance.isEnabled,
                JSON.stringify(config)
              ).subscribe({
                next: () => {
                  instance.configuration = config;
                },
                error: (error) => {
                  console.warn('Failed to update SqlServer adapter configuration:', error);
                }
              });
            }
          }
          
          // Initialize expansion states for new instances
          if (!this.destinationCardExpandedStates.has(instance.adapterInstanceGuid)) {
            this.destinationCardExpandedStates.set(instance.adapterInstanceGuid, true);
          }
        });
        // If no instances exist, create a default SqlServerAdapter instance pointing to TransferData table
        if (this.destinationAdapterInstances.length === 0) {
          const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
          
          // Create default SqlServerAdapter destination instance pointing to TransportData table in app database
          const defaultSqlServerInstance = {
            adapterInstanceGuid: this.generateGuid(),
            instanceName: 'SQL Server Destination',
            adapterName: 'SqlServer',
            isEnabled: true,
            configuration: JSON.stringify({
              destination: 'TransportData',
              tableName: 'TransportData'
            })
          };
          
          // Add the default instance via API
          this.transportService.addDestinationAdapterInstance(
            this.DEFAULT_INTERFACE_NAME,
            defaultSqlServerInstance.adapterName,
            defaultSqlServerInstance.instanceName,
            defaultSqlServerInstance.configuration
          ).subscribe({
            next: (createdInstance) => {
              this.destinationAdapterInstances = [createdInstance];
              // Update interface configuration with SQL Server connection properties if available
              if (defaultConfig) {
                if (defaultConfig.sqlServerName && defaultConfig.sqlDatabaseName) {
                  // SQL connection properties are already set in the interface configuration
                  // The SqlServerAdapter will use these from the interface configuration
                }
              }
            },
            error: (error) => {
              console.error('Error creating default SqlServerAdapter instance:', error);
              // Fallback: use legacy properties if API call fails
              if (defaultConfig && defaultConfig.destinationAdapterName) {
                this.destinationAdapterInstances = [{
                  adapterInstanceGuid: defaultConfig.destinationAdapterInstanceGuid || this.generateGuid(),
                  instanceName: defaultConfig.destinationInstanceName || 'Destination',
                  adapterName: defaultConfig.destinationAdapterName,
                  isEnabled: defaultConfig.destinationIsEnabled ?? true,
                  configuration: defaultConfig.destinationConfiguration || '{}'
                }];
              } else {
                // Use the default SqlServerAdapter instance locally even if API call fails
                this.destinationAdapterInstances = [defaultSqlServerInstance];
              }
            }
          });
        }
      },
      error: (error) => {
        console.error('Error loading destination adapter instances:', error);
        this.destinationAdapterInstances = [];
      }
    });
  }

  addDestinationAdapter(adapterName: 'CSV' | 'SqlServer'): void {
    // Set default configuration based on adapter type
    let defaultConfiguration: any = {};
    if (adapterName === 'SqlServer') {
      defaultConfiguration = {
        destination: 'TransportData',
        tableName: 'TransportData'
      };
    }
    
    // Generate default instance name
    const totalCount = this.destinationAdapterInstances.length;
    let counter = totalCount + 1;
    let instanceName = `Destination ${counter}`;
    
    // Ensure uniqueness by checking if name already exists
    const existingNames = this.destinationAdapterInstances.map(i => i.instanceName);
    while (existingNames.includes(instanceName)) {
      counter++;
      instanceName = `Destination ${counter}`;
    }
    
    // Generate GUID
    const adapterInstanceGuid = this.generateGuid();
    
    // Add the instance via API
    this.transportService.addDestinationAdapterInstance(
      this.currentInterfaceName || this.DEFAULT_INTERFACE_NAME,
      adapterName,
      instanceName,
      JSON.stringify(defaultConfiguration)
    ).subscribe({
      next: (createdInstance) => {
        this.snackBar.open(`Destination adapter "${instanceName}" added successfully`, 'OK', { duration: 3000 });
        this.loadDestinationAdapterInstances();
      },
      error: (error) => {
        console.error('Error adding destination adapter:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Hinzufügen des Destination Adapters');
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
      }
    });
  }

  removeDestinationAdapter(adapterInstanceGuid: string, instanceName: string): void {
    const confirmMessage = `Are you sure you want to remove destination adapter "${instanceName}"?`;
    if (!confirm(confirmMessage)) return;

    this.transportService.removeDestinationAdapterInstance(
      this.currentInterfaceName || this.DEFAULT_INTERFACE_NAME,
      adapterInstanceGuid
    ).subscribe({
      next: () => {
        this.snackBar.open(`Destination adapter "${instanceName}" removed successfully`, 'OK', { duration: 3000 });
        this.loadDestinationAdapterInstances();
      },
      error: (error) => {
        console.error('Error removing destination adapter:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Entfernen des Destination Adapters');
        this.snackBar.open(detailedMessage, 'Schließen', { 
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
      }
    });
  }

  private generateGuid(): string {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
      const r = Math.random() * 16 | 0;
      const v = c === 'x' ? r : (r & 0x3 | 0x8);
      return v.toString(16);
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
      
      // Normalize configurations for comparison
      const currConfig = typeof curr.configuration === 'string' ? JSON.parse(curr.configuration || '{}') : (curr.configuration || {});
      const newConfig = typeof newInst.configuration === 'string' ? JSON.parse(newInst.configuration || '{}') : (newInst.configuration || {});
      
      return curr.instanceName !== newInst.instanceName || 
             curr.isEnabled !== newInst.isEnabled ||
             JSON.stringify(currConfig) !== JSON.stringify(newConfig);
    });
    
    // Execute operations
    const operations: Promise<any>[] = [];
    
    // Add new instances
    toAdd.forEach(instance => {
      // Ensure SqlServer instances have TransportData configuration
      let config = instance.configuration || {};
      if (typeof config === 'string') {
        try {
          config = JSON.parse(config);
        } catch (e) {
          config = {};
        }
      }
      
      if (instance.adapterName === 'SqlServer') {
        config = {
          ...config,
          destination: 'TransportData',
          tableName: 'TransportData'
        };
      }
      
      operations.push(
        firstValueFrom(
          this.transportService.addDestinationAdapterInstance(
            this.DEFAULT_INTERFACE_NAME,
            instance.adapterName,
            instance.instanceName,
            JSON.stringify(config)
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
      // Ensure SqlServer instances have TransportData configuration
      let config = instance.configuration || {};
      if (typeof config === 'string') {
        try {
          config = JSON.parse(config);
        } catch (e) {
          config = {};
        }
      }
      
      if (instance.adapterName === 'SqlServer') {
        config = {
          ...config,
          destination: 'TransportData',
          tableName: 'TransportData'
        };
      }
      
      operations.push(
        firstValueFrom(
          this.transportService.updateDestinationAdapterInstance(
            this.DEFAULT_INTERFACE_NAME,
            instance.adapterInstanceGuid,
            instance.instanceName,
            instance.isEnabled,
            JSON.stringify(config)
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
    
    // Parse configuration for SqlServer adapter to get table name
    let sqlTableName = 'TransportData'; // Default
    if (instance.adapterName === 'SqlServer' && instance.configuration) {
      try {
        const config = typeof instance.configuration === 'string' 
          ? JSON.parse(instance.configuration) 
          : instance.configuration;
        sqlTableName = config.destination || config.tableName || 'TransportData';
      } catch (e) {
        console.warn('Failed to parse destination adapter configuration:', e);
      }
    }
    
    const dialogData: AdapterPropertiesData = {
      adapterType: 'Destination',
      adapterName: instance.adapterName,
      instanceName: instance.instanceName,
      isEnabled: instance.isEnabled ?? true,
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
    
    // Build configuration JSON
    let configuration = instance.configuration || {};
    if (typeof configuration === 'string') {
      try {
        configuration = JSON.parse(configuration);
      } catch (e) {
        configuration = {};
      }
    }
    
    // Update configuration for CSV adapters
    if (properties.destinationReceiveFolder !== undefined && instance.adapterName === 'CSV') {
      configuration = { ...configuration, destination: properties.destinationReceiveFolder };
    }
    
    // Ensure SqlServer adapters have TransportData table configuration
    if (instance.adapterName === 'SqlServer') {
      configuration = {
        ...configuration,
        destination: 'TransportData',
        tableName: 'TransportData'
      };
    }
    
    // Update enabled property immediately in local array for UI responsiveness
    if (properties.isEnabled !== undefined) {
      instance.isEnabled = properties.isEnabled;
    }
    
    // Update instance name immediately in local array for UI responsiveness
    if (properties.instanceName !== undefined) {
      instance.instanceName = properties.instanceName;
    }
    
    // Update instance properties via API
    this.transportService.updateDestinationAdapterInstance(
      this.DEFAULT_INTERFACE_NAME,
      instanceGuid,
      properties.instanceName || instance.instanceName,
      properties.isEnabled !== undefined ? properties.isEnabled : instance.isEnabled,
      JSON.stringify(configuration)
    ).subscribe({
      next: () => {
        // Reload from API to ensure consistency
        this.loadDestinationAdapterInstances();
        this.snackBar.open('Destination adapter instance updated', 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating destination adapter instance:', error);
        // Revert local changes on error
        this.loadDestinationAdapterInstances();
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren der Destination Adapter Instance');
        this.snackBar.open(detailedMessage, 'Schließen', {
          duration: 10000,
          panelClass: ['error-snackbar'],
          verticalPosition: 'top',
          horizontalPosition: 'center'
        });
      }
    });
  }

  getAdapterIcon(adapterName: 'CSV' | 'SqlServer'): string {
    return adapterName === 'CSV' ? 'description' : 'storage';
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

  // Color palette for CSV columns - dark, readable colors
  private readonly COLUMN_COLORS = [
    '#1a237e', // Dark blue
    '#b71c1c', // Dark red
    '#004d40', // Dark teal
    '#e65100', // Dark orange
    '#4a148c', // Dark purple
    '#006064', // Dark cyan
    '#3e2723', // Dark brown
    '#1b5e20', // Dark green
    '#880e4f', // Dark pink
    '#212121', // Dark gray
    '#0d47a1', // Blue
    '#c62828', // Red
    '#00695c', // Teal
    '#e64a19', // Orange
    '#6a1b9a', // Purple
  ];

  /**
   * Format CSV data as HTML with colored columns
   */
  formatCsvAsHtml(): string {
    if (!this.editableCsvText || this.editableCsvText.trim() === '') {
      return '';
    }

    try {
      const lines = this.editableCsvText.split('\n').filter(line => line.trim() !== '');
      if (lines.length === 0) {
        return '';
      }

      // Build HTML with colored columns
      const htmlLines = lines.map((line) => {
        const values = this.parseCsvLine(line);
        const cells = values.map((value, colIndex) => {
          const color = this.COLUMN_COLORS[colIndex % this.COLUMN_COLORS.length];
          const escapedValue = this.escapeHtml(value.trim().replace(/^"|"$/g, ''));
          return `<span style="color: ${color};">${escapedValue}</span>`;
        });
        
        // Add separator between cells
        const separator = `<span style="color: #999;">${this.FIELD_SEPARATOR}</span>`;
        return cells.join(separator);
      });

      return htmlLines.join('<br>');
    } catch (error) {
      console.error('Error formatting CSV as HTML:', error);
      return this.escapeHtml(this.editableCsvText);
    }
  }

  /**
   * Escape HTML special characters
   */
  private escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }

  /**
   * Handle blur event on contenteditable div
   */
  onCsvTextBlur(): void {
    // Extract plain text from HTML content
    if (this.csvEditor?.nativeElement) {
      const plainText = this.csvEditor.nativeElement.textContent || this.csvEditor.nativeElement.innerText || '';
      if (plainText !== this.editableCsvText) {
        this.editableCsvText = plainText;
      }
      // Reformat with colors after editing
      this.formattedCsvHtml = this.formatCsvAsHtml();
      if (this.csvEditor?.nativeElement) {
        this.csvEditor.nativeElement.innerHTML = this.formattedCsvHtml;
      }
      this.onCsvTextChange();
    }
  }

  /**
   * Handle CSV text changes from editable contenteditable div
   */
  onCsvTextChange(): void {
    // Extract plain text from contenteditable div
    if (this.csvEditor?.nativeElement) {
      const plainText = this.csvEditor.nativeElement.textContent || this.csvEditor.nativeElement.innerText || '';
      if (plainText !== this.editableCsvText) {
        this.editableCsvText = plainText;
      }
    }

    // Parse the edited CSV text back into csvData
    if (!this.editableCsvText || this.editableCsvText.trim() === '') {
      this.csvData = [];
      this.formattedCsvHtml = '';
      // Update CsvData property when cleared
      if (this.currentInterfaceName) {
        this.updateCsvDataProperty('');
      }
      return;
    }

    try {
      const lines = this.editableCsvText.split('\n').filter(line => line.trim() !== '');
      if (lines.length === 0) {
        this.csvData = [];
        this.formattedCsvHtml = '';
        // Update CsvData property when cleared
        if (this.currentInterfaceName) {
          this.updateCsvDataProperty('');
        }
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
      
      // Update formatted HTML with colored columns
      this.formattedCsvHtml = this.formatCsvAsHtml();
      
      // Update CsvData property when CSV text changes
      if (this.currentInterfaceName && this.editableCsvText) {
        this.updateCsvDataProperty(this.editableCsvText);
      }
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


