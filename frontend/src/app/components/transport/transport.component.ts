import { Component, OnInit, OnDestroy, AfterViewInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatSort, MatSortModule, Sort } from '@angular/material/sort';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBarModule, MatSnackBar, MatSnackBarHorizontalPosition, MatSnackBarVerticalPosition } from '@angular/material/snack-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { FormsModule } from '@angular/forms';
import { AdapterCardComponent } from '../adapter-card/adapter-card.component';
import { AdapterPropertiesDialogComponent, AdapterPropertiesData } from '../adapter-properties-dialog/adapter-properties-dialog.component';
import { AdapterSelectDialogComponent, AdapterSelectData } from '../adapter-select-dialog/adapter-select-dialog.component';
import { DestinationInstancesDialogComponent, DestinationAdapterInstance } from '../destination-instances-dialog/destination-instances-dialog.component';
import { InterfaceJsonViewDialogComponent } from '../interface-json-view-dialog/interface-json-view-dialog.component';
import { BlobContainerExplorerDialogComponent, BlobContainerExplorerDialogData } from '../blob-container-explorer-dialog/blob-container-explorer-dialog.component';
import { WelcomeDialogComponent } from '../welcome-dialog/welcome-dialog.component';
import { DiagnoseDialogComponent, DiagnoseDialogData } from '../diagnose-dialog/diagnose-dialog.component';
import { AddInterfaceDialogComponent, AddInterfaceDialogData } from '../add-interface-dialog/add-interface-dialog.component';
import { ServiceBusMessageDialogComponent } from '../service-bus-message-dialog/service-bus-message-dialog.component';
import { ContainerAppProgressDialogComponent, ContainerAppProgressData } from '../container-app-progress-dialog/container-app-progress-dialog.component';
import { TransportService } from '../../services/transport.service';
import { TranslationService } from '../../services/translation.service';
import { ErrorTrackingService } from '../../services/error-tracking.service';
import { ErrorDialogComponent } from '../error-dialog/error-dialog.component';
import { FeatureService, Feature } from '../../services/feature.service';
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
    MatCheckboxModule,
    AdapterCardComponent,
    DestinationInstancesDialogComponent,
    BlobContainerExplorerDialogComponent,
    DiagnoseDialogComponent,
    AddInterfaceDialogComponent,
    ContainerAppProgressDialogComponent
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
  csvDataText: string = ''; // CsvData property value (bound to adapter card)
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
  destinationAdapterInstances: DestinationAdapterInstance[] = [];
  sourceIsEnabled: boolean = false;
  destinationIsEnabled: boolean = false;
  sourceReceiveFolder: string = '';
  sourceFileMask: string = '*.txt';
  sourceBatchSize: number = 100;
  sourceFieldSeparator: string = '║';
  csvPollingInterval: number = 10;
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
  sourceAdapterName: 'CSV' | 'FILE' | 'SFTP' | 'SqlServer' | 'SAP' | 'Dynamics365' | 'CRM' = 'CSV';
  private refreshSubscription?: Subscription;
  private lastErrorShown: string = '';
  private errorShownCount: number = 0;
  private demoSampleCsvCache: { text: string; records: CsvRecord[] } | null = null;
  private autoStartedInterfaces = new Set<string>();
  
  // Blob Container Explorer
  blobContainerFolders: any[] = [];
  isLoadingBlobContainer = false;
  blobContainerExpanded: boolean = true;
  blobContainerSortBy: 'name' | 'date' | 'size' = 'date';
  blobContainerSortOrder: 'asc' | 'desc' = 'desc'; // desc = newest first
  selectedBlobFiles: Set<string> = new Set(); // Set of full paths of selected files
  isDeletingBlobFiles = false;
  blobContainerPagination: Map<string, { loadedCount: number; hasMore: boolean; isLoadingMore: boolean }> = new Map();
  readonly BLOB_FILES_PER_PAGE = 10;
  
  // Service Bus Messages
  serviceBusMessages: any[] = [];
  serviceBusMessagesDataSource = new MatTableDataSource<any>([]);
  serviceBusMessagesDisplayedColumns: string[] = ['enqueuedTime', 'adapterName', 'messageId', 'deliveryCount', 'actions'];
  isLoadingServiceBusMessages = false;
  serviceBusMessagesExpanded: boolean = true;
  private serviceBusMessagesRefreshInterval?: Subscription;
  
  selectedComponent: string = 'all';
  availableComponents: string[] = ['all', 'Azure Function', 'Blob Storage', 'SQL Server', 'Vercel API'];
  
  readonly DEFAULT_INTERFACE_NAME = 'FromCsvToSqlServerExample';
  private lastSyncedCsvData: string = '';
  private csvDataInitialization = new Set<string>();
  private isCsvDataUpdateInFlight = false;
  
  private getActiveInterfaceName(): string {
    return this.currentInterfaceName && this.currentInterfaceName.trim().length > 0
      ? this.currentInterfaceName.trim()
      : this.DEFAULT_INTERFACE_NAME;
  }

  private getInterfaceConfig(interfaceName?: string): any | undefined {
    const name = interfaceName || this.getActiveInterfaceName();
    return this.interfaceConfigurations.find(c => c.interfaceName === name);
  }
  
  private findSourceInstance(
    config: any,
    options?: { adapterInstanceGuid?: string; instanceName?: string }
  ): any {
    if (!config || !config.sources) {
      return null;
    }
    
    const sources = config.sources;
    const isArray = Array.isArray(sources);
    const lookup = isArray ? undefined : sources;
    const entries: any[] = isArray ? sources : Object.values(sources);
    
    const guidCandidates = [
      options?.adapterInstanceGuid,
      config?.sourceAdapterInstanceGuid,
      this.sourceAdapterInstanceGuid
    ].filter((value): value is string => !!value && value.length > 0);
    
    for (const guid of guidCandidates) {
      if (!guid) continue;
      if (lookup && lookup[guid]) {
        return lookup[guid];
      }
      const matchByGuid = entries.find(entry => entry?.adapterInstanceGuid === guid);
      if (matchByGuid) {
        return matchByGuid;
      }
    }
    
    const nameCandidates = [
      options?.instanceName,
      config?.sourceInstanceName,
      this.sourceInstanceName
    ].filter((value): value is string => !!value && value.length > 0);
    
    for (const name of nameCandidates) {
      if (!name) continue;
      if (lookup && lookup[name]) {
        return lookup[name];
      }
      const matchByName = entries.find(entry => entry?.instanceName === name);
      if (matchByName) {
        return matchByName;
      }
    }
    
    return entries.length > 0 ? entries[0] : null;
  }
  
  private getEffectiveSourceEnabled(
    config?: any,
    options?: { adapterInstanceGuid?: string; instanceName?: string }
  ): boolean | undefined {
    if (!config) {
      return undefined;
    }
    
    const sourceInstance = this.findSourceInstance(config, options);
    if (sourceInstance) {
      if (sourceInstance.isEnabled !== undefined) {
        return sourceInstance.isEnabled;
      }
      if (sourceInstance.IsEnabled !== undefined) {
        return sourceInstance.IsEnabled;
      }
    }
    
    if (config.sourceIsEnabled !== undefined) {
      return config.sourceIsEnabled;
    }
    if (config.SourceIsEnabled !== undefined) {
      return config.SourceIsEnabled;
    }
    
    return undefined;
  }
  
  private applyEnabledStateToSourceConfig(
    config: any,
    enabled: boolean,
    options?: { adapterInstanceGuid?: string; instanceName?: string }
  ): void {
    if (!config) {
      return;
    }
    
    config.sourceIsEnabled = enabled;
    if (config.SourceIsEnabled !== undefined) {
      config.SourceIsEnabled = enabled;
    }
    
    const sourceInstance = this.findSourceInstance(config, options);
    if (sourceInstance) {
      sourceInstance.isEnabled = enabled;
      if (sourceInstance.IsEnabled !== undefined) {
        sourceInstance.IsEnabled = enabled;
      }
    }
  }
  
  private shouldIncludeSqlProperties(adapterName?: string): boolean {
    return (adapterName || '').toLowerCase() === 'sqlserver';
  }

  private assignWhenPresent(target: any, key: string, value: any): void {
    if (value !== undefined && value !== null) {
      target[key] = value;
    }
  }
  
  private getFirstDefined<T>(...values: Array<T | undefined | null>): T | undefined {
    for (const value of values) {
      if (value !== undefined && value !== null) {
        return value;
      }
    }
    return undefined;
  }
  
  // Track if table exists (based on whether we have columns loaded)
  tableExists: boolean = false;

  csvDisplayedColumns: string[] = []; // Will be populated dynamically from CSV data
  sqlDisplayedColumns: string[] = []; // Will be populated dynamically from SQL data
  logDisplayedColumns: string[] = ['timestamp', 'level', 'component', 'message', 'details'];

  // Feature flag for Destination Adapter UI
  isDestinationAdapterUIEnabled: boolean = true; // Enable by default for SQL Server testing

  constructor(
    private transportService: TransportService,
    private translationService: TranslationService,
    private snackBar: MatSnackBar,
    public dialog: MatDialog,
    private errorTrackingService: ErrorTrackingService,
    private featureService: FeatureService
  ) {}

  /**
   * Show error dialog with AI integration
   */
  private showErrorDialog(
    error: Error | any,
    functionName: string,
    component: string = 'TransportComponent',
    context?: any
  ): void {
    // Track error
    this.errorTrackingService.trackError(functionName, error, component, context);
    
    // Add application state
    this.errorTrackingService.addApplicationState('currentInterface', this.currentInterfaceName);
    this.errorTrackingService.addApplicationState('sourceEnabled', this.sourceIsEnabled);
    this.errorTrackingService.addApplicationState('destinationEnabled', this.destinationIsEnabled);
    this.errorTrackingService.addApplicationState('sourceAdapterName', this.sourceAdapterName);
    
    // Show dialog
    this.dialog.open(ErrorDialogComponent, {
      width: '800px',
      maxWidth: '90vw',
      data: {
        error: error,
        functionName: functionName,
        component: component,
        context: context
      }
    });
  }

  ngOnInit(): void {
    // Don't populate sample CSV data here - it will be initialized when CSV adapter instance is created
    // via ensureCsvDataInitialized() in loadInterfaceData()
    this.loadSqlData();
    this.loadProcessLogs();
    this.loadInterfaceConfigurations();
    this.loadBlobContainerFolders();
    // Check feature first, then load destination adapters if enabled
    this.checkDestinationAdapterUIFeature();
    // TEMPORARILY DISABLED: Auto-refresh disabled - only manual refresh via reload button
    // this.startAutoRefresh();
    
    // Subscribe to language changes to update UI
    this.translationService.getCurrentLanguage().subscribe(() => {
      // Trigger change detection - translations will be updated via getTranslation calls
    });
    
    // Show welcome dialog on first visit
    this.showWelcomeDialogIfNeeded();
  }

  /**
   * Check if Destination Adapter UI feature is enabled
   */
  private checkDestinationAdapterUIFeature(): void {
    this.featureService.getFeatures().subscribe({
      next: (features) => {
        const destinationAdapterUIFeature = features.find(f => 
          f.title === 'Destination Adapter UI' || 
          f.featureNumber === 8
        );
        this.isDestinationAdapterUIEnabled = destinationAdapterUIFeature?.isEnabled ?? false;
        
        // Only load destination adapter instances if feature is enabled
        if (this.isDestinationAdapterUIEnabled) {
          this.loadDestinationAdapterInstances();
        } else {
          // Clear destination adapter instances if feature is disabled
          this.destinationAdapterInstances = [];
        }
      },
      error: (error) => {
        console.error('Error loading features:', error);
        // Default to disabled if feature check fails
        this.isDestinationAdapterUIEnabled = false;
        this.destinationAdapterInstances = [];
      }
    });
  }

  /**
   * Check if feature is enabled before allowing destination adapter operations
   */
  private ensureDestinationAdapterUIFeatureEnabled(): boolean {
    if (!this.isDestinationAdapterUIEnabled) {
      this.snackBar.open('Destination Adapter UI feature is not enabled. Please contact an administrator.', 'OK', { 
        duration: 5000,
        panelClass: ['error-snackbar']
      });
      return false;
    }
    return true;
  }

  showWelcomeDialogIfNeeded(): void {
    const welcomeDialogShown = localStorage.getItem('welcomeDialogShown');
    if (!welcomeDialogShown) {
      setTimeout(() => {
        this.dialog.open(WelcomeDialogComponent, {
          width: '900px',
          maxWidth: '95vw',
          maxHeight: '90vh',
          disableClose: false,
          autoFocus: false
        });
      }, 1000); // Longer delay to ensure all API calls complete before showing dialog
    }
  }
  
  loadInterfaceConfigurations(): void {
    this.transportService.getInterfaceConfigurations().subscribe({
      next: (configs) => {
        // Filter out any entries with empty/null interface names
        let allConfigs = (configs || []).filter(config => 
          config && config.interfaceName && config.interfaceName.trim().length > 0
        );
        
        // Remove duplicates: if both placeholder and real exist for same name, keep only the real one
        const seenNames = new Set<string>();
        const uniqueConfigs: any[] = [];
        
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
        this.interfaceConfigurations = uniqueConfigs.sort((a, b) => 
          a.interfaceName.localeCompare(b.interfaceName, undefined, { sensitivity: 'base' })
        );
        
        // Ensure default interface exists - create it if it doesn't
        const defaultExists = this.interfaceConfigurations.some(c => 
          c.interfaceName === this.DEFAULT_INTERFACE_NAME && !c._isPlaceholder
        );
        const defaultPlaceholderExists = this.interfaceConfigurations.some(c => 
          c.interfaceName === this.DEFAULT_INTERFACE_NAME && c._isPlaceholder
        );
        
        if (!defaultExists && !defaultPlaceholderExists) {
          // Add placeholder first so it appears in dropdown immediately
          this.interfaceConfigurations.unshift({
            interfaceName: this.DEFAULT_INTERFACE_NAME,
            sourceAdapterName: 'CSV',
            destinationAdapterName: 'SqlServer',
            sourceInstanceName: 'Source',
            destinationInstanceName: 'Destination',
            sourceIsEnabled: false,
            destinationIsEnabled: false,
            csvPollingInterval: 10,
            _isPlaceholder: true
          });
          
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
              // Reload configurations to get the real one (will replace placeholder)
              this.loadInterfaceConfigurations();
            },
            error: (error) => {
              console.error('Error creating default interface:', error);
              // Placeholder is already added, so it will appear in dropdown
              // Continue with normal flow below
            }
          });
        } else if (defaultPlaceholderExists && !defaultExists) {
          // If only placeholder exists, try to create the real interface
          this.transportService.createInterfaceConfiguration({
            interfaceName: this.DEFAULT_INTERFACE_NAME,
            sourceAdapterName: 'CSV',
            sourceConfiguration: JSON.stringify({ source: 'csv-files/csv-incoming' }),
            destinationAdapterName: 'SqlServer',
            destinationConfiguration: JSON.stringify({ destination: 'TransportData' }),
            description: 'Default CSV to SQL Server interface'
          }).subscribe({
            next: (createdConfig) => {
              // Reload configurations to get the real one (will replace placeholder)
              this.loadInterfaceConfigurations();
            },
            error: (error) => {
              console.error('Error creating default interface:', error);
              // Keep placeholder for now
            }
          });
        }
        
        this.ensureInterfaceSelection();
      },
      error: (error) => {
        console.error('Error loading interface configurations:', error);
        console.error('Full error object:', JSON.stringify(error, null, 2));
        
        // Extract detailed error message with all available information
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Laden der Interface-Konfigurationen');
        
        // Show as warning (less intrusive) since this is not critical for basic functionality
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
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

  private applyCsvDataLocally(csvText: string, records?: CsvRecord[]): void {
    this.csvDataText = csvText || '';
    this.editableCsvText = this.csvDataText;
    if (records) {
      this.csvData = records;
    }
    this.formattedCsvHtml = this.editableCsvText ? this.formatCsvAsHtml() : '';
  }

  private ensureCsvDataInitialized(interfaceName: string, existingCsvData?: string): void {
    if (!interfaceName) {
      return;
    }

    // If backend already has CSV data, use it and sync locally
    // Don't mark as initialized - backend CSV data should still be refreshable
    // NEVER replace existing CSV data - it must only be set ONCE after adapter creation
    if (existingCsvData && existingCsvData.trim().length > 0) {
      // Sync local data with backend - preserve existing data as-is
      if (existingCsvData !== this.lastSyncedCsvData) {
        this.lastSyncedCsvData = existingCsvData;
        this.applyCsvDataLocally(existingCsvData);
      }
      return;
    }

    // Prevent multiple sample data generations for the same interface
    // Only mark as initialized when we actually GENERATE sample data
    if (this.csvDataInitialization.has(interfaceName)) {
      return;
    }

    // Backend has no CSV data - generate sample data ONCE and write to backend
    // This only happens when CSV adapter instance is first created
    // Mark as initialized to prevent regenerating sample data
    this.csvDataInitialization.add(interfaceName);
    const sample = this.getDemoSampleCsv();
    this.applyCsvDataLocally(sample.text, sample.records);
    this.lastSyncedCsvData = sample.text;
    // Always write sample data to backend when initializing
    this.updateCsvDataProperty(sample.text, { force: true });
  }

  private getDemoSampleCsv(): { text: string; records: CsvRecord[] } {
    if (this.demoSampleCsvCache) {
      return this.demoSampleCsvCache;
    }
    const records = this.generateSampleCsvData();
    const text = this.convertRecordsToCsvText(records);
    this.demoSampleCsvCache = { text, records };
    return this.demoSampleCsvCache;
  }

  private populateSampleCsvForDemo(force: boolean = false): void {
    if (!force && this.csvDataText && this.csvDataText.trim().length > 0) {
      return;
    }
    const sample = this.getDemoSampleCsv();
    this.applyCsvDataLocally(sample.text, sample.records);
  }

  generateSampleCsvData(): CsvRecord[] {
    const data: CsvRecord[] = [];
    const names = ['Max Mustermann', 'Anna Schmidt', 'Peter Müller', 'Lisa Weber', 'Thomas Fischer', 'Julia Wagner', 'Michael Schulz', 'Laura Becker', 'Daniel Koch', 'Sophie Richter'];
    const cities = ['Berlin', 'München', 'Hamburg', 'Köln', 'Frankfurt', 'Stuttgart', 'Düsseldorf', 'Leipzig', 'Dortmund', 'Essen'];
    const domains = ['example.com', 'test.org', 'mail.net', 'company.io'];
    const departments = ['IT', 'HR', 'Sales', 'Marketing', 'Finance', 'Operations', 'R&D', 'Support', 'Legal', 'Admin'];
    const countries = ['Deutschland', 'Österreich', 'Schweiz', 'Frankreich', 'Italien', 'Spanien', 'Niederlande', 'Belgien', 'Polen', 'Tschechien'];
    const products = ['Product A', 'Product B', 'Product C', 'Product D', 'Product E', 'Product F', 'Product G', 'Product H', 'Product I', 'Product J'];
    const statuses = ['Active', 'Inactive', 'Pending', 'Completed', 'Cancelled', 'Processing', 'Approved', 'Rejected', 'Draft', 'Published'];

    // Generate 100 rows with 10 columns
    for (let i = 1; i <= 100; i++) {
      const name = names[Math.floor(Math.random() * names.length)];
      const city = cities[Math.floor(Math.random() * cities.length)];
      const domain = domains[Math.floor(Math.random() * domains.length)];
      const department = departments[Math.floor(Math.random() * departments.length)];
      const country = countries[Math.floor(Math.random() * countries.length)];
      const product = products[Math.floor(Math.random() * products.length)];
      const status = statuses[Math.floor(Math.random() * statuses.length)];
      
      data.push({
        id: i,
        name: `${name} ${i}`,
        email: `user${i}@${domain}`,
        age: Math.floor(Math.random() * 40) + 20,
        city: city,
        salary: Math.floor(Math.random() * 50000) + 30000,
        department: department,
        country: country,
        product: product,
        status: status,
        registrationDate: new Date(2020 + Math.floor(Math.random() * 4), Math.floor(Math.random() * 12), Math.floor(Math.random() * 28) + 1).toISOString().split('T')[0]
      } as CsvRecord);
    }
    
    return data;
  }

  private convertRecordsToCsvText(records: CsvRecord[]): string {
    if (!records || records.length === 0) {
      return '';
    }

    const columns = this.extractColumns(records);
    const headerRow = columns.join(this.FIELD_SEPARATOR);
    const dataRows = records.map(row => {
      return columns.map(col => {
        const value = (row as any)[col] ?? '';
        const valueStr = String(value);
        if (valueStr.includes(this.FIELD_SEPARATOR) || valueStr.includes('"')) {
          return `"${valueStr.replace(/"/g, '""')}"`;
        }
        return valueStr;
      }).join(this.FIELD_SEPARATOR);
    });

    return [headerRow, ...dataRows].join('\n');
  }
  
  /**
   * Update CsvData property on the adapter
   */
  updateCsvDataProperty(csvText: string, options?: { force?: boolean }): void {
    const interfaceName = this.getActiveInterfaceName();
    if (!interfaceName) {
      return;
    }

    const normalizedText = csvText ?? '';
    if (!options?.force && normalizedText === this.lastSyncedCsvData) {
      return;
    }

    this.isCsvDataUpdateInFlight = true;
    this.transportService.updateCsvData(interfaceName, normalizedText).subscribe({
      next: () => {
        this.lastSyncedCsvData = normalizedText;
        this.isCsvDataUpdateInFlight = false;
      },
      error: (error) => {
        console.error('Error updating CsvData property:', error);
        this.isCsvDataUpdateInFlight = false;
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
        this.showErrorDialog(
          error instanceof Error ? error : new Error(String(error)),
          'onFileSelected',
          'TransportComponent',
          { fileName: file.name }
        );
      }
      // Reset file input to allow selecting the same file again
      input.value = '';
    };

    reader.onerror = () => {
      const error = new Error('Fehler beim Lesen der Datei');
      this.showErrorDialog(error, 'onFileSelected', 'TransportComponent', {
        fileName: file.name,
        errorType: 'FileReaderError'
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
          this.showErrorMessageWithCopy(detailedMessage, { duration: 15000 });
          
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


  loadBlobContainerFolders(): void {
    this.isLoadingBlobContainer = true;
    this.blobContainerPagination.clear();
    this.transportService.getBlobContainerFolders('csv-files', '', this.BLOB_FILES_PER_PAGE).subscribe({
      next: (folders) => {
        // Ensure all 3 CSV folders are shown (csv-incoming, csv-processed, csv-error)
        const requiredFolders = ['/csv-incoming', '/csv-processed', '/csv-error'];
        const folderMap = new Map<string, any>();
        
        // Add existing folders from API response
        (folders || []).forEach((folder: any) => {
          folderMap.set(folder.path, folder);
        });
        
        // Ensure all required folders exist (create empty ones if missing)
        requiredFolders.forEach(folderPath => {
          if (!folderMap.has(folderPath)) {
            folderMap.set(folderPath, {
              path: folderPath,
              files: [],
              totalFileCount: 0,
              hasMoreFiles: false
            });
          }
        });
        
        // Convert map back to array and sort
        const allFolders = Array.from(folderMap.values());
        
        // Initialize pagination state for each folder
        allFolders.forEach((folder: any) => {
          this.blobContainerPagination.set(folder.path, {
            loadedCount: folder.files?.length || 0,
            hasMore: folder.hasMoreFiles || false,
            isLoadingMore: false
          });
        });
        
        // Sort folders: csv-incoming first, then csv-processed, then csv-error
        const sortedFolders = allFolders.sort((a, b) => {
          const order = ['/csv-incoming', '/csv-processed', '/csv-error'];
          const aIndex = order.indexOf(a.path);
          const bIndex = order.indexOf(b.path);
          if (aIndex !== -1 && bIndex !== -1) return aIndex - bIndex;
          if (aIndex !== -1) return -1;
          if (bIndex !== -1) return 1;
          return a.path.localeCompare(b.path);
        });
        
        this.blobContainerFolders = this.sortBlobContainerFolders(sortedFolders);
        this.isLoadingBlobContainer = false;
      },
      error: (error) => {
        console.error('Error loading blob container folders:', error);
        this.isLoadingBlobContainer = false;
        this.blobContainerFolders = [];
        this.blobContainerPagination.clear();
      }
    });
  }

  loadMoreBlobFiles(folderPath: string): void {
    const pagination = this.blobContainerPagination.get(folderPath);
    if (!pagination || !pagination.hasMore || pagination.isLoadingMore) {
      return;
    }

    pagination.isLoadingMore = true;
    const currentLoadedCount = pagination.loadedCount;
    const nextPageStart = currentLoadedCount;
    const maxFiles = nextPageStart + this.BLOB_FILES_PER_PAGE;

    this.transportService.getBlobContainerFolders('csv-files', folderPath.replace(/^\//, ''), maxFiles).subscribe({
      next: (folders) => {
        const folder = folders.find((f: any) => f.path === folderPath);
        if (folder) {
          // Update the folder's files with the new data
          const existingFolder = this.blobContainerFolders.find(f => f.path === folderPath);
          if (existingFolder) {
            existingFolder.files = folder.files;
            existingFolder.hasMoreFiles = folder.hasMoreFiles;
            existingFolder.totalFileCount = folder.totalFileCount;
          }

          // Update pagination state
          pagination.loadedCount = folder.files?.length || 0;
          pagination.hasMore = folder.hasMoreFiles || false;
          pagination.isLoadingMore = false;

          // Re-sort after loading more
          this.blobContainerFolders = this.sortBlobContainerFolders(this.blobContainerFolders);
        }
      },
      error: (error) => {
        console.error('Error loading more blob files:', error);
        pagination.isLoadingMore = false;
      }
    });
  }

  getBlobFolderPagination(folderPath: string): { loadedCount: number; hasMore: boolean; isLoadingMore: boolean } {
    return this.blobContainerPagination.get(folderPath) || { loadedCount: 0, hasMore: false, isLoadingMore: false };
  }

  sortBlobContainerFolders(folders: any[]): any[] {
    return folders.map(folder => ({
      ...folder,
      files: [...folder.files].sort((a, b) => {
        let comparison = 0;
        
        switch (this.blobContainerSortBy) {
          case 'name':
            comparison = a.name.localeCompare(b.name);
            break;
          case 'date':
            comparison = new Date(a.lastModified).getTime() - new Date(b.lastModified).getTime();
            break;
          case 'size':
            comparison = a.size - b.size;
            break;
        }
        
        return this.blobContainerSortOrder === 'asc' ? comparison : -comparison;
      })
    }));
  }

  onBlobContainerSortChange(sortBy: 'name' | 'date' | 'size'): void {
    if (this.blobContainerSortBy === sortBy) {
      // Toggle sort order if clicking same column
      this.blobContainerSortOrder = this.blobContainerSortOrder === 'asc' ? 'desc' : 'asc';
    } else {
      // Set new sort column, default to desc for date (newest first), asc for others
      this.blobContainerSortBy = sortBy;
      this.blobContainerSortOrder = sortBy === 'date' ? 'desc' : 'asc';
    }
    
    // Re-sort existing data
    this.blobContainerFolders = this.sortBlobContainerFolders(this.blobContainerFolders);
  }

  toggleBlobFileSelection(fullPath: string): void {
    if (this.selectedBlobFiles.has(fullPath)) {
      this.selectedBlobFiles.delete(fullPath);
    } else {
      this.selectedBlobFiles.add(fullPath);
    }
  }

  isBlobFileSelected(fullPath: string): boolean {
    return this.selectedBlobFiles.has(fullPath);
  }

  selectAllBlobFiles(): void {
    this.selectedBlobFiles.clear();
    this.blobContainerFolders.forEach(folder => {
      folder.files.forEach((file: any) => {
        this.selectedBlobFiles.add(file.fullPath);
      });
    });
  }

  deselectAllBlobFiles(): void {
    this.selectedBlobFiles.clear();
  }

  getSelectedBlobFilesCount(): number {
    return this.selectedBlobFiles.size;
  }

  areAllFilesInFolderSelected(folder: any): boolean {
    if (!folder.files || folder.files.length === 0) return false;
    return folder.files.every((file: any) => this.selectedBlobFiles.has(file.fullPath));
  }

  areSomeFilesInFolderSelected(folder: any): boolean {
    if (!folder.files || folder.files.length === 0) return false;
    const selectedCount = folder.files.filter((file: any) => this.selectedBlobFiles.has(file.fullPath)).length;
    return selectedCount > 0 && selectedCount < folder.files.length;
  }

  toggleFolderSelection(folder: any): void {
    const allSelected = this.areAllFilesInFolderSelected(folder);
    
    if (allSelected) {
      // Deselect all files in folder
      folder.files.forEach((file: any) => {
        this.selectedBlobFiles.delete(file.fullPath);
      });
    } else {
      // Select all files in folder
      folder.files.forEach((file: any) => {
        this.selectedBlobFiles.add(file.fullPath);
      });
    }
  }

  deleteSelectedBlobFiles(): void {
    const selectedFiles = Array.from(this.selectedBlobFiles);
    if (selectedFiles.length === 0) {
      return;
    }

    const confirmMessage = `Are you sure you want to delete ${selectedFiles.length} file(s)? This action cannot be undone.`;
    if (!confirm(confirmMessage)) {
      return;
    }

    this.isDeletingBlobFiles = true;
    
    // Delete files in parallel
    const deletePromises = selectedFiles.map(fullPath => 
      this.transportService.deleteBlobFile('csv-files', fullPath).toPromise()
        .catch(error => {
          console.error(`Error deleting file ${fullPath}:`, error);
          return { success: false, path: fullPath, error };
        })
    );

    Promise.all(deletePromises).then(results => {
      const successCount = results.filter(r => r === null || (r as any).success !== false).length;
      const failCount = results.length - successCount;

      if (failCount > 0) {
        this.snackBar.open(
          `Deleted ${successCount} file(s). ${failCount} file(s) failed to delete.`,
          'Schließen',
          { duration: 5000, panelClass: ['warning-snackbar'] }
        );
      } else {
        this.snackBar.open(
          `Successfully deleted ${successCount} file(s).`,
          'OK',
          { duration: 3000 }
        );
      }

      // Clear selection and refresh
      this.selectedBlobFiles.clear();
      this.isDeletingBlobFiles = false;
      this.loadBlobContainerFolders();
    });
  }

  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
  }

  getTotalFileCount(): number {
    return this.blobContainerFolders.reduce((total, folder) => total + (folder.files?.length || 0), 0);
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
        // Map backend datetime_created to frontend timestamp
        // Backend returns datetime_created, frontend expects timestamp
        const enrichedLogs = logs.map((log: any) => ({
          ...log,
          timestamp: log.datetime_created || log.timestamp || new Date().toISOString(),
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
          this.showErrorMessageWithCopy(detailedMessage, { duration: 15000 });
          
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

  loadServiceBusMessages(): void {
    const interfaceName = this.getActiveInterfaceName();
    if (!interfaceName) {
      this.serviceBusMessages = [];
      this.serviceBusMessagesDataSource.data = [];
      return;
    }

    this.isLoadingServiceBusMessages = true;

    this.transportService.getServiceBusMessages(interfaceName, 100).subscribe({
      next: (messages) => {
        // Sort by enqueuedTime descending (newest first)
        this.serviceBusMessages = (messages || []).sort((a: any, b: any) => {
          const dateA = new Date(a.enqueuedTime).getTime();
          const dateB = new Date(b.enqueuedTime).getTime();
          return dateB - dateA;
        });
        this.serviceBusMessagesDataSource.data = this.serviceBusMessages;
        this.isLoadingServiceBusMessages = false;
      },
      error: (error) => {
        console.error('Error loading Service Bus messages:', error);
        this.isLoadingServiceBusMessages = false;
        this.serviceBusMessages = [];
        this.serviceBusMessagesDataSource.data = [];
      }
    });
  }

  openServiceBusMessageDialog(message: any): void {
    this.dialog.open(ServiceBusMessageDialogComponent, {
      width: '800px',
      maxWidth: '95vw',
      maxHeight: '90vh',
      data: { message }
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
    const interfaceName = this.getActiveInterfaceName();
    if (!interfaceName) {
      return;
    }
    this.startTransportForInterface(interfaceName);
  }

  private startTransportForInterface(interfaceName: string): void {
    this.isTransporting = true;
    const activeConfig = this.getInterfaceConfig(interfaceName);

    if (!activeConfig) {
      this.transportService.createInterfaceConfiguration({
        interfaceName,
        sourceAdapterName: this.sourceAdapterName,
        sourceConfiguration: JSON.stringify({ source: this.sourceReceiveFolder || 'csv-files/csv-incoming' }),
        destinationAdapterName: this.destinationAdapterInstances[0]?.adapterName || 'SqlServer',
        destinationConfiguration: JSON.stringify({ destination: 'TransportData' }),
        description: 'CSV to SQL Server interface'
      }).subscribe({
        next: () => {
          this.loadInterfaceConfigurations();
          // New config defaults to disabled - user must enable adapters manually
          this.snackBar.open(
            'Interface-Konfiguration erstellt. Bitte aktivieren Sie Source- und Destination-Adapter in den Einstellungen, um den Transport zu starten.',
            'OK',
            { duration: 5000 }
          );
          this.isTransporting = false;
        },
        error: (error) => {
          console.error('Error creating interface configuration:', error);
          const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Erstellen der Interface-Konfiguration');
          this.showErrorMessageWithCopy(detailedMessage + '\n\nTransport wird trotzdem gestartet...', {
            duration: 12000
          });
          // New config defaults to disabled - user must enable adapters manually
          this.snackBar.open(
            'Interface-Konfiguration erstellt. Bitte aktivieren Sie Source- und Destination-Adapter in den Einstellungen, um den Transport zu starten.',
            'OK',
            { duration: 5000 }
          );
          this.isTransporting = false;
        }
      });
      return;
    }

    // Check if adapters are enabled - do NOT automatically enable them
    // User must explicitly enable adapters via settings dialog
    const sourceWasEnabledBeforeStart = this.getEffectiveSourceEnabled(activeConfig, {
      adapterInstanceGuid: this.sourceAdapterInstanceGuid,
      instanceName: this.sourceInstanceName
    }) ?? false;
    const destinationWasEnabledBeforeStart = activeConfig.destinationIsEnabled ?? false;

    // Only start transport if both source and destination are enabled
    if (!sourceWasEnabledBeforeStart) {
      this.snackBar.open(
        'Source-Adapter ist deaktiviert. Bitte aktivieren Sie den Source-Adapter in den Einstellungen, um den Transport zu starten.',
        'OK',
        { duration: 5000 }
      );
      this.isTransporting = false;
      return;
    }

    if (!destinationWasEnabledBeforeStart) {
      this.snackBar.open(
        'Destination-Adapter ist deaktiviert. Bitte aktivieren Sie den Destination-Adapter in den Einstellungen, um den Transport zu starten.',
        'OK',
        { duration: 5000 }
      );
      this.isTransporting = false;
      return;
    }

    // Both adapters are enabled - proceed with transport
    this.uploadAndStartTransport(interfaceName, sourceWasEnabledBeforeStart);
  }
  
  private autoStartCsvSourceIfEnabled(interfaceName?: string, options: { force?: boolean } = {}): void {
    const targetInterface = interfaceName || this.getActiveInterfaceName();
    if (!targetInterface) {
      return;
    }

    if (!options.force && this.autoStartedInterfaces.has(targetInterface)) {
      return;
    }

    const config = this.getInterfaceConfig(targetInterface);
    if (!config) {
      return;
    }

    const sourceEnabled = this.getEffectiveSourceEnabled(config, {
      adapterInstanceGuid: this.sourceAdapterInstanceGuid,
      instanceName: this.sourceInstanceName
    }) ?? false;
    if (config.sourceAdapterName !== 'CSV' || !sourceEnabled) {
      if (!sourceEnabled) {
        this.autoStartedInterfaces.delete(targetInterface);
      }
      return;
    }

    if (this.isTransporting) {
      return;
    }

    this.autoStartedInterfaces.add(targetInterface);
    this.startTransportForInterface(targetInterface);
  }

  private uploadAndStartTransport(interfaceName: string, sourceWasEnabledBeforeStart: boolean = true): void {
    // Use edited CSV text if available, otherwise use formatted CSV from csvData
    const csvContent = this.editableCsvText || this.formatCsvAsText();
    this.transportService.startTransport(interfaceName, csvContent).subscribe({
      next: (response) => {
        // Only show message if source adapter was enabled BEFORE we started transport
        // (not if we enabled it just to start transport)
        if (sourceWasEnabledBeforeStart) {
          // Show a more user-friendly message about the Service Bus architecture
          const userMessage = 'Transport gestartet. CSV-Daten werden über Service Bus verarbeitet und an alle aktivierten Zieladapter weitergeleitet.';
          this.snackBar.open(userMessage, 'Schließen', { duration: 7000 });
        }
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
        
        // Ignore errors during initial load (when welcome dialog might be shown)
        // These are often CORS or network timing issues that resolve themselves
        const isInitialLoad = !this.currentInterfaceName || this.currentInterfaceName === '';
        if (isInitialLoad) {
          console.log('Ignoring transport start error during initial load');
          return;
        }
        
        // Extract detailed error message with all available information
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Starten des Transports');
        
        this.showErrorMessageWithCopy(detailedMessage, { duration: 15000 });
        this.isTransporting = false;
        this.autoStartedInterfaces.delete(interfaceName);
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
        
        this.showErrorMessageWithCopy(detailedMessage, { duration: 15000 });
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
          
          this.showErrorMessageWithCopy(detailedMessage, { duration: 15000 });
          this.isLoading = false;
        }
      });
    }
  }

  runDiagnostics(): void {
    // Open diagnose dialog
    const dialogData: DiagnoseDialogData = { result: null };
    const dialogRef = this.dialog.open<DiagnoseDialogComponent, DiagnoseDialogData>(DiagnoseDialogComponent, {
      width: '90%',
      maxWidth: '800px',
      maxHeight: '90vh',
      data: dialogData
    });
    
    this.isDiagnosing = true;
    
    // Start diagnose immediately
    this.transportService.diagnose().subscribe({
      next: (result) => {
        this.isDiagnosing = false;
        
        // Update dialog with result
        dialogData.result = result;
        const instance = dialogRef.componentInstance;
        if (instance) {
          instance.data = dialogData;
          instance.isLoading = false;
        }
        
        // Log details to console
        console.log('Diagnostics Result:', result);
      },
      error: (error) => {
        console.error('Error running diagnostics:', error);
        this.isDiagnosing = false;
        dialogRef.close();
        
        // Extract detailed error message
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler bei der Diagnose');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 15000 });
      }
    });
  }

  private startAutoRefresh(): void {
    // Refresh every 3 seconds to catch changes immediately after they're written
    // This ensures the UI updates as soon as data appears in TransportData or ProcessLogs
    this.refreshSubscription = interval(3000).subscribe(() => {
      this.loadSqlData();
      this.loadProcessLogs();
      this.refreshCurrentInterfaceCsvData();
      this.loadBlobContainerFolders();
    });
    
    // Refresh Service Bus messages every 5 seconds (slightly less frequent)
    this.serviceBusMessagesRefreshInterval = interval(5000).subscribe(() => {
      if (this.serviceBusMessagesExpanded && this.currentInterfaceName) {
        this.loadServiceBusMessages();
      }
    });
  }

  private refreshCurrentInterfaceCsvData(): void {
    if (this.isCsvDataUpdateInFlight) {
      return;
    }

    const interfaceName = this.getActiveInterfaceName();
    if (!interfaceName) {
      return;
    }

    this.transportService.getInterfaceConfiguration(interfaceName).subscribe({
      next: (config) => {
        if (!config || config._isPlaceholder) {
          return;
        }

        const incomingCsvData = config.csvData || '';
        if (incomingCsvData && incomingCsvData.trim().length > 0) {
          // Backend has CSV data - always sync local data with backend (backend is source of truth)
          // Backend CSV data should always be refreshable, even if sample data was previously generated
          if (incomingCsvData !== this.lastSyncedCsvData) {
            this.lastSyncedCsvData = incomingCsvData;
            this.applyCsvDataLocally(incomingCsvData);
          }
        } else {
          // Backend has no CSV data - only generate sample data if we haven't already generated it
          // This prevents regenerating sample data, but allows refreshing backend CSV data
          this.ensureCsvDataInitialized(config.interfaceName, incomingCsvData);
        }
      },
      error: (error) => {
        console.error('Error refreshing interface configuration:', error);
      }
    });
  }
  
  getDefaultInterfaceStatus(): { exists: boolean; sourceEnabled: boolean; destinationEnabled: boolean } {
    const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
    return {
      exists: !!defaultConfig,
      sourceEnabled: this.getEffectiveSourceEnabled(defaultConfig) ?? false,
      destinationEnabled: defaultConfig?.destinationIsEnabled ?? false
    };
  }

  updateInterfaceName(): void {
    if (!this.currentInterfaceName || this.currentInterfaceName.trim() === '') {
      this.snackBar.open('Interface-Name darf nicht leer sein', 'OK', { duration: 3000 });
      // Restore previous name
      const activeConfig = this.getInterfaceConfig();
      this.currentInterfaceName = activeConfig?.interfaceName || this.DEFAULT_INTERFACE_NAME;
      return;
    }

    const trimmedName = this.currentInterfaceName.trim();
    const activeInterfaceName = this.getActiveInterfaceName();
    const activeConfig = this.getInterfaceConfig(activeInterfaceName);
    
    if (!activeConfig) {
      // Configuration doesn't exist yet, will be created when transport starts
      return;
    }

    if (trimmedName === activeConfig.interfaceName) {
      // No change
      return;
    }

    // Update interface name via API
    this.transportService.updateInterfaceName(activeInterfaceName, trimmedName).subscribe({
      next: () => {
        this.currentInterfaceName = trimmedName;
        this.loadInterfaceConfigurations();
        this.snackBar.open('Interface-Name aktualisiert', 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating interface name:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren des Interface-Namens');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
        // Restore previous name
        this.currentInterfaceName = activeConfig.interfaceName;
      }
    });
  }

  updateSourceInstanceName(): void {
    const trimmedName = this.sourceInstanceName.trim() || 'Source';
    const activeInterfaceName = this.getActiveInterfaceName();
    const activeConfig = this.getInterfaceConfig(activeInterfaceName);
    
    if (!activeConfig) {
      return;
    }

    if (trimmedName === (activeConfig.sourceInstanceName || 'Source')) {
      return;
    }

    this.transportService.updateInstanceName(activeInterfaceName, 'Source', trimmedName).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
      },
      error: (error) => {
        console.error('Error updating source instance name:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren des Source-Instanz-Namens');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
        // Restore previous name
        this.sourceInstanceName = activeConfig.sourceInstanceName || 'Source';
      }
    });
  }

  updateDestinationInstanceName(name?: string): void {
    const trimmedName = (name || this.destinationInstanceName).trim() || 'Destination';
    const activeInterfaceName = this.getActiveInterfaceName();
    const activeConfig = this.getInterfaceConfig(activeInterfaceName);
    
    if (!activeConfig) {
      return;
    }

    if (trimmedName === (activeConfig.destinationInstanceName || 'Destination')) {
      return;
    }

    this.destinationInstanceName = trimmedName; // Update immediately for responsive UI

    this.transportService.updateInstanceName(activeInterfaceName, 'Destination', trimmedName).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
      },
      error: (error) => {
        console.error('Error updating destination instance name:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren des Destination-Instanz-Namens');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
        // Restore previous name
        this.destinationInstanceName = activeConfig.destinationInstanceName || 'Destination';
      }
    });
  }

  // Track when we last saved enabled status to avoid race conditions
  private lastEnabledSaveTime: { [interfaceName: string]: number } = {};

  onSourceEnabledChangeFromHeader(newValue: boolean): void {
    // Update local state first
    this.sourceIsEnabled = newValue;
    // Then save to backend
    this.onSourceEnabledChange();
  }

  onSourceEnabledChange(): void {
    const activeInterfaceName = this.getActiveInterfaceName();
    if (!activeInterfaceName) {
      return;
    }

    // Get the current config to restore on error
    const activeConfig = this.getInterfaceConfig(activeInterfaceName);
    const previousEnabledValue = this.getEffectiveSourceEnabled(activeConfig, {
      adapterInstanceGuid: this.sourceAdapterInstanceGuid,
      instanceName: this.sourceInstanceName
    });
    const enabledValueToSave = this.sourceIsEnabled;
    
    // Record when we're saving
    this.lastEnabledSaveTime[activeInterfaceName] = Date.now();

    // Always save the enabled state - don't check if it changed because the dialog already updated local state
    this.transportService.toggleInterfaceConfiguration(activeInterfaceName, 'Source', enabledValueToSave).subscribe({
      next: () => {
        // Update local cache immediately so dialog reads correct value if reopened
        if (activeConfig) {
          this.applyEnabledStateToSourceConfig(activeConfig, enabledValueToSave, {
            adapterInstanceGuid: this.sourceAdapterInstanceGuid,
            instanceName: this.sourceInstanceName
          });
        }
        // Also update the interfaceConfigurations array cache
        const configIndex = this.interfaceConfigurations.findIndex(c => c.interfaceName === activeInterfaceName);
        if (configIndex >= 0) {
          this.applyEnabledStateToSourceConfig(this.interfaceConfigurations[configIndex], enabledValueToSave, {
            adapterInstanceGuid: this.sourceAdapterInstanceGuid,
            instanceName: this.sourceInstanceName
          });
        }
        // Ensure local state matches what we just saved
        this.sourceIsEnabled = enabledValueToSave;
        
        // Record when we saved - this helps avoid race conditions when reopening dialog
        this.lastEnabledSaveTime[activeInterfaceName] = Date.now();
        
        // DON'T reload configurations immediately after save - it will overwrite our cached values
        // The cache is already updated above, so we don't need to reload right away
        // Only reload after a longer delay to sync with backend, but preserve our saved value
        setTimeout(() => {
          // Save the enabled state before reload
          const savedEnabledState = enabledValueToSave;
          this.loadInterfaceConfigurations();
          // After reload completes, ensure our saved value is preserved
          setTimeout(() => {
            const reloadedConfig = this.getInterfaceConfig(activeInterfaceName);
            if (reloadedConfig) {
              const reloadedEnabled = this.getEffectiveSourceEnabled(reloadedConfig, {
                adapterInstanceGuid: this.sourceAdapterInstanceGuid,
                instanceName: this.sourceInstanceName
              });
              if (reloadedEnabled !== savedEnabledState) {
                console.log(`Restoring saved sourceIsEnabled value: ${savedEnabledState} (backend had: ${reloadedEnabled})`);
                this.applyEnabledStateToSourceConfig(reloadedConfig, savedEnabledState, {
                  adapterInstanceGuid: this.sourceAdapterInstanceGuid,
                  instanceName: this.sourceInstanceName
                });
                const configIndex = this.interfaceConfigurations.findIndex(c => c.interfaceName === activeInterfaceName);
                if (configIndex >= 0) {
                  this.applyEnabledStateToSourceConfig(this.interfaceConfigurations[configIndex], savedEnabledState, {
                    adapterInstanceGuid: this.sourceAdapterInstanceGuid,
                    instanceName: this.sourceInstanceName
                  });
                }
              }
            }
          }, 200);
        }, 2000);
        
        this.snackBar.open(
          `Source adapter ${enabledValueToSave ? 'aktiviert' : 'deaktiviert'}. ${
            enabledValueToSave
              ? 'CSV-Daten werden nun unabhängig vom Destination-Adapter in der eigenen Container App verarbeitet und an den Service Bus übergeben. Aktivieren Sie einen Destination-Adapter nur, wenn Sie die Nachrichten weiterverarbeiten möchten.'
              : 'Der Prozess stoppt sofort.'
          }`,
          'OK',
          { duration: 5000 }
        );
        // Do NOT automatically start transport - user must explicitly start it via Start Transport button
        // Only remove from auto-started list if disabled
        if (!enabledValueToSave) {
          this.autoStartedInterfaces.delete(activeInterfaceName);
        }
      },
      error: (error) => {
        console.error('Error toggling source adapter:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktivieren/Deaktivieren des Source-Adapters');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
        // Restore previous value
        const valueToRestore = previousEnabledValue !== undefined ? previousEnabledValue : !enabledValueToSave;
        this.sourceIsEnabled = valueToRestore;
        if (activeConfig) {
          this.applyEnabledStateToSourceConfig(activeConfig, valueToRestore, {
            adapterInstanceGuid: this.sourceAdapterInstanceGuid,
            instanceName: this.sourceInstanceName
          });
        }
        const configIndex = this.interfaceConfigurations.findIndex(c => c.interfaceName === activeInterfaceName);
        if (configIndex >= 0) {
          this.applyEnabledStateToSourceConfig(this.interfaceConfigurations[configIndex], valueToRestore, {
            adapterInstanceGuid: this.sourceAdapterInstanceGuid,
            instanceName: this.sourceInstanceName
          });
        }
      }
    });
  }

  onDestinationEnabledChange(): void {
    const activeInterfaceName = this.getActiveInterfaceName();
    const activeConfig = this.getInterfaceConfig(activeInterfaceName);
    
    if (!activeConfig) {
      // Restore previous value
      this.destinationIsEnabled = false;
      return;
    }

    if (this.destinationIsEnabled === (activeConfig.destinationIsEnabled ?? false)) {
      return;
    }

    this.transportService.toggleInterfaceConfiguration(activeInterfaceName, 'Destination', this.destinationIsEnabled).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
        this.snackBar.open(
          `Destination adapter ${this.destinationIsEnabled ? 'aktiviert' : 'deaktiviert'}. ${this.destinationIsEnabled ? 'Der Prozess wird gestartet, wenn auch der Source-Adapter aktiviert ist.' : 'Der Prozess stoppt sofort.'}`,
          'OK',
          { duration: 5000 }
        );
      },
      error: (error) => {
        console.error('Error toggling destination adapter:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktivieren/Deaktivieren des Destination-Adapters');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
        // Restore previous value
        this.destinationIsEnabled = activeConfig.destinationIsEnabled ?? false;
      }
    });
  }

  restartSourceAdapter(): void {
    this.isRestartingSource = true;
    const interfaceName = this.getActiveInterfaceName();
    this.transportService.restartAdapter(interfaceName, 'Source').subscribe({
      next: (response) => {
        this.isRestartingSource = false;
        this.snackBar.open(response.message || 'Source adapter wird neu gestartet...', 'OK', { duration: 5000 });
        this.loadInterfaceConfigurations();
      },
      error: (error) => {
        this.isRestartingSource = false;
        console.error('Error restarting source adapter:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Neustarten des Source-Adapters');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
      }
    });
  }

  restartDestinationAdapter(): void {
    this.isRestartingDestination = true;
    const interfaceName = this.getActiveInterfaceName();
    this.transportService.restartAdapter(interfaceName, 'Destination').subscribe({
      next: (response) => {
        this.isRestartingDestination = false;
        this.snackBar.open(response.message || 'Destination adapter wird neu gestartet...', 'OK', { duration: 5000 });
        this.loadInterfaceConfigurations();
      },
      error: (error) => {
        this.isRestartingDestination = false;
        console.error('Error restarting destination adapter:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Neustarten des Destination-Adapters');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
      }
    });
  }

  updateReceiveFolder(folder?: string): void {
    const folderValue = folder || this.sourceReceiveFolder;
    const interfaceName = this.getActiveInterfaceName();
    const activeConfig = this.getInterfaceConfig(interfaceName);
    
    if (!activeConfig) {
      return;
    }

    if (folderValue === (activeConfig.sourceReceiveFolder || '')) {
      return;
    }

    this.sourceReceiveFolder = folderValue; // Update immediately for responsive UI

    this.transportService.updateReceiveFolder(interfaceName, folderValue).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
        this.snackBar.open('Receive Folder aktualisiert', 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating receive folder:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren des Receive Folders');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
        // Restore previous value
        this.sourceReceiveFolder = activeConfig.sourceReceiveFolder || '';
      }
    });
  }

  updateFileMask(fileMask?: string): void {
    const fileMaskValue = fileMask || this.sourceFileMask;
    const interfaceName = this.getActiveInterfaceName();
    const activeConfig = this.getInterfaceConfig(interfaceName);
    
    if (!activeConfig) {
      return;
    }

    const normalizedMask = fileMaskValue.trim() || '*.txt';
    if (normalizedMask === (activeConfig.sourceFileMask || '*.txt')) {
      return;
    }

    this.sourceFileMask = normalizedMask; // Update immediately for responsive UI

    this.transportService.updateFileMask(interfaceName, normalizedMask).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
        this.snackBar.open('File Mask aktualisiert', 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating file mask:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren der File Mask');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
        // Restore previous value
        this.sourceFileMask = activeConfig.sourceFileMask || '*.txt';
      }
    });
  }

  updateBatchSize(batchSize?: number): void {
    const batchSizeValue = batchSize ?? this.sourceBatchSize;
    const interfaceName = this.getActiveInterfaceName();
    const activeConfig = this.getInterfaceConfig(interfaceName);
    
    if (!activeConfig) {
      return;
    }

    const normalizedBatchSize = batchSizeValue > 0 ? batchSizeValue : 100;
    if (normalizedBatchSize === (activeConfig.sourceBatchSize ?? 100)) {
      return;
    }

    this.sourceBatchSize = normalizedBatchSize; // Update immediately for responsive UI

    this.transportService.updateBatchSize(interfaceName, normalizedBatchSize).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
        this.snackBar.open('Batch Size aktualisiert', 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating batch size:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren der Batch Size');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
        // Restore previous value
        this.sourceBatchSize = activeConfig.sourceBatchSize ?? 100;
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
    const interfaceName = this.getActiveInterfaceName();
    const activeConfig = this.getInterfaceConfig(interfaceName);
    
    if (!activeConfig) {
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
      interfaceName,
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
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
        // Restore previous values
        this.loadInterfaceConfigurations();
      }
    });
  }

  updateSqlPollingProperties(
    pollingStatement?: string,
    pollingInterval?: number
  ): void {
    const interfaceName = this.getActiveInterfaceName();
    const activeConfig = this.getInterfaceConfig(interfaceName);
    
    if (!activeConfig) {
      return;
    }

    // Update local properties immediately for responsive UI
    if (pollingStatement !== undefined) this.sqlPollingStatement = pollingStatement;
    if (pollingInterval !== undefined) this.sqlPollingInterval = pollingInterval;

    this.transportService.updateSqlPollingProperties(
      interfaceName,
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
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
        // Restore previous values
        this.loadInterfaceConfigurations();
      }
    });
  }

  openBlobContainerExplorer(adapterType: 'Source' | 'Destination', adapterName: string, instanceName: string, adapterInstanceGuid: string): void {
    // Blob Container Explorer doesn't actually need the interface configuration or adapterInstanceGuid
    // It just lists blob container folders, which doesn't require interface-specific data
    // If adapterInstanceGuid is missing, we can still open the explorer with default values
    
    // Try to get adapterInstanceGuid from local config if not provided
    if (!adapterInstanceGuid) {
      const interfaceName = this.getActiveInterfaceName();
      const activeConfig = this.getInterfaceConfig(interfaceName);
      
      if (activeConfig) {
        if (adapterType === 'Source') {
          // Try new structure first (sources dictionary)
          if (activeConfig.sources && Object.keys(activeConfig.sources).length > 0) {
            const sourceInstance = Object.values(activeConfig.sources)[0] as any;
            adapterInstanceGuid = sourceInstance?.adapterInstanceGuid || sourceInstance?.AdapterInstanceGuid || '';
          }
          // Fallback to old structure
          if (!adapterInstanceGuid) {
            adapterInstanceGuid = activeConfig.sourceAdapterInstanceGuid || '';
          }
          if (adapterInstanceGuid) {
            this.sourceAdapterInstanceGuid = adapterInstanceGuid;
          }
        } else {
          // For destinations, try to find matching instance
          const destinationInstances = this.destinationAdapterInstances || activeConfig.destinationAdapterInstances || [];
          const matchingInstance = destinationInstances.find((inst: any) =>
            (inst.instanceName && inst.instanceName === instanceName) ||
            (inst.adapterName && inst.adapterName === adapterName)
          );
          
          if (matchingInstance?.adapterInstanceGuid) {
            adapterInstanceGuid = matchingInstance.adapterInstanceGuid;
          }
        }
      }
      
      // If still no GUID, use empty string - Blob Container Explorer doesn't actually need it
      if (!adapterInstanceGuid) {
        adapterInstanceGuid = '';
      }
    }

    const dialogData: BlobContainerExplorerDialogData = {
      adapterType: adapterType,
      adapterName: adapterName,
      instanceName: instanceName,
      adapterInstanceGuid: adapterInstanceGuid || '' // Empty GUID is acceptable - explorer doesn't need it
    };

    const dialogRef = this.dialog.open(BlobContainerExplorerDialogComponent, {
      width: '90%',
      maxWidth: '1200px',
      maxHeight: '90vh',
      data: dialogData
    });

    dialogRef.afterClosed().subscribe(result => {
      // Handle any result if needed
    });
  }

  private showErrorMessageWithCopy(
    message: string,
    options: {
      duration?: number;
      verticalPosition?: MatSnackBarVerticalPosition;
      horizontalPosition?: MatSnackBarHorizontalPosition;
    } = {}
  ): void {
    const snackRef = this.snackBar.open(message, 'Kopieren', {
      duration: options.duration ?? 10000,
      panelClass: ['error-snackbar'],
      verticalPosition: options.verticalPosition ?? 'top',
      horizontalPosition: options.horizontalPosition ?? 'center'
    });

    snackRef.onAction().subscribe(() => {
      this.copyTextToClipboard(message);
    });
  }

  private copyTextToClipboard(text: string): void {
    if (navigator && navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(() => {
        this.showCopySuccess();
      }).catch(() => {
        this.fallbackCopyText(text);
      });
    } else {
      this.fallbackCopyText(text);
    }
  }

  private fallbackCopyText(text: string): void {
    const textarea = document.createElement('textarea');
    textarea.value = text;
    textarea.style.position = 'fixed';
    textarea.style.opacity = '0';
    document.body.appendChild(textarea);
    textarea.focus();
    textarea.select();
    try {
      document.execCommand('copy');
      this.showCopySuccess();
    } catch (err) {
      console.error('Clipboard copy failed:', err);
      this.snackBar.open('Konnte Text nicht in die Zwischenablage kopieren.', 'OK', { duration: 4000, panelClass: ['error-snackbar'] });
    } finally {
      document.body.removeChild(textarea);
    }
  }

  private showCopySuccess(): void {
    this.snackBar.open('Fehlermeldung in die Zwischenablage kopiert.', 'OK', { duration: 3000, panelClass: ['success-snackbar'] });
  }

  openSourceAdapterSettings(): void {
    // Reload configuration to ensure we have the latest values from the backend
    const interfaceName = this.getActiveInterfaceName();
    if (!interfaceName) {
      return;
    }

    // Check if we recently saved enabled status (within last 2 seconds)
    // If so, use cached config to avoid race condition with backend
    const lastSaveTime = this.lastEnabledSaveTime[interfaceName];
    const timeSinceSave = lastSaveTime ? Date.now() - lastSaveTime : Infinity;
    const cachedConfig = this.getInterfaceConfig(interfaceName);
    
    if (timeSinceSave < 2000 && cachedConfig) {
      // Use cached config if we just saved (within last 2 seconds)
      // This avoids race condition where backend hasn't updated yet
      console.log('Using cached config (recent save detected)');
      console.log('cachedConfig.sourceIsEnabled:', cachedConfig?.sourceIsEnabled);
      console.log('cachedConfig.sources:', cachedConfig?.sources);
      this.openSourceAdapterSettingsDialog(cachedConfig);
      return;
    }

    // Fetch fresh configuration from backend before opening dialog
    this.transportService.getInterfaceConfiguration(interfaceName).subscribe({
      next: (freshConfig) => {
        console.log('getInterfaceConfiguration response:', freshConfig);
        console.log('freshConfig.sourceIsEnabled:', freshConfig?.sourceIsEnabled);
        if (freshConfig) {
          // Update local cache
          const index = this.interfaceConfigurations.findIndex(c => c.interfaceName === interfaceName);
          if (index >= 0) {
            this.interfaceConfigurations[index] = freshConfig;
          } else {
            this.interfaceConfigurations.push(freshConfig);
          }
          
          // Sync local state from fresh config (use adapter instance properties as source of truth)
          const sourceInstanceFromConfig = this.findSourceInstance(freshConfig, {
            adapterInstanceGuid: freshConfig?.sourceAdapterInstanceGuid,
            instanceName: freshConfig?.sourceInstanceName
          });
          const enabledFromConfig = this.getEffectiveSourceEnabled(freshConfig, {
            adapterInstanceGuid: sourceInstanceFromConfig?.adapterInstanceGuid,
            instanceName: sourceInstanceFromConfig?.instanceName
          });
          if (enabledFromConfig !== undefined) {
            this.sourceIsEnabled = enabledFromConfig;
          }
          this.sourceInstanceName = freshConfig.sourceInstanceName || this.sourceInstanceName;
          this.sourceReceiveFolder = freshConfig.sourceReceiveFolder || this.sourceReceiveFolder;
          this.sourceFileMask = freshConfig.sourceFileMask || this.sourceFileMask;
          this.sourceBatchSize = freshConfig.sourceBatchSize ?? this.sourceBatchSize;
          this.sourceFieldSeparator = freshConfig.sourceFieldSeparator || this.sourceFieldSeparator;
          this.csvPollingInterval = freshConfig.csvPollingInterval ?? this.csvPollingInterval;
          this.sourceAdapterInstanceGuid = freshConfig.sourceAdapterInstanceGuid || this.sourceAdapterInstanceGuid;
          if (sourceInstanceFromConfig?.adapterInstanceGuid) {
            this.sourceAdapterInstanceGuid = sourceInstanceFromConfig.adapterInstanceGuid;
          }
          
          console.log('After sync - this.sourceIsEnabled:', this.sourceIsEnabled);
          console.log('Opening dialog with freshConfig:', freshConfig);
          
          // Now open the dialog with fresh data
          this.openSourceAdapterSettingsDialog(freshConfig);
        } else {
          // Fallback to cached config if fresh fetch fails
          const activeConfig = this.getInterfaceConfig();
          this.openSourceAdapterSettingsDialog(activeConfig);
        }
      },
      error: (error) => {
        console.error('Error loading fresh configuration:', error);
        // Fallback to cached config if fetch fails
        const activeConfig = this.getInterfaceConfig();
        this.openSourceAdapterSettingsDialog(activeConfig);
      }
    });
  }

  private openSourceAdapterSettingsDialog(activeConfig: any): void {
    // Always prioritize config values over local state
    // Use explicit checks for undefined/null to handle empty strings, 0, and false correctly
    console.log('openSourceAdapterSettingsDialog - activeConfig:', activeConfig);
    console.log('openSourceAdapterSettingsDialog - sourceIsEnabled from config:', activeConfig?.sourceIsEnabled);
    console.log('openSourceAdapterSettingsDialog - sources hierarchy:', activeConfig?.sources);
    console.log('openSourceAdapterSettingsDialog - isEnabled from sources:', activeConfig?.sources?.[this.sourceAdapterName]?.isEnabled);
    console.log('openSourceAdapterSettingsDialog - sourceIsEnabled local:', this.sourceIsEnabled);
    
    const sourceInstance = this.findSourceInstance(activeConfig, {
      adapterInstanceGuid: this.sourceAdapterInstanceGuid || activeConfig?.sourceAdapterInstanceGuid,
      instanceName: activeConfig?.sourceInstanceName || this.sourceInstanceName
    });
    const resolvedInstanceName = sourceInstance?.instanceName !== undefined
      ? sourceInstance.instanceName
      : (activeConfig?.sourceInstanceName !== undefined ? activeConfig.sourceInstanceName : this.sourceInstanceName);
    const resolvedIsEnabled = this.getEffectiveSourceEnabled(activeConfig, {
      adapterInstanceGuid: sourceInstance?.adapterInstanceGuid,
      instanceName: sourceInstance?.instanceName
    });
    
    const dialogData: AdapterPropertiesData = {
      adapterType: 'Source',
      adapterName: this.sourceAdapterName,
      instanceName: resolvedInstanceName,
      isEnabled: resolvedIsEnabled !== undefined ? resolvedIsEnabled : this.sourceIsEnabled,
      // Use explicit undefined checks - empty string is a valid value!
      receiveFolder: activeConfig?.sourceReceiveFolder !== undefined ? activeConfig.sourceReceiveFolder : this.sourceReceiveFolder,
      fileMask: activeConfig?.sourceFileMask !== undefined ? activeConfig.sourceFileMask : this.sourceFileMask,
      batchSize: activeConfig?.sourceBatchSize !== undefined ? activeConfig.sourceBatchSize : this.sourceBatchSize,
      fieldSeparator: activeConfig?.sourceFieldSeparator !== undefined ? activeConfig.sourceFieldSeparator : this.sourceFieldSeparator,
      csvAdapterType: activeConfig?.csvAdapterType || (activeConfig?.sources && Object.keys(activeConfig.sources).length > 0 
        ? (Object.values(activeConfig.sources)[0] as any)?.csvAdapterType || (Object.values(activeConfig.sources)[0] as any)?.CsvAdapterType
        : undefined),
      csvData: this.csvDataText || activeConfig?.csvData || activeConfig?.CsvData || 
        (activeConfig?.sources && Object.keys(activeConfig.sources).length > 0 
          ? ((Object.values(activeConfig.sources)[0] as any)?.csvData || (Object.values(activeConfig.sources)[0] as any)?.CsvData || '')
          : ''),
      csvPollingInterval: activeConfig?.csvPollingInterval !== undefined ? activeConfig.csvPollingInterval : 
        (activeConfig?.sources && Object.keys(activeConfig.sources).length > 0
          ? ((Object.values(activeConfig.sources)[0] as any)?.csvPollingInterval || (Object.values(activeConfig.sources)[0] as any)?.CsvPollingInterval)
          : this.csvPollingInterval),
      // SFTP properties - use explicit undefined checks
      sftpHost: activeConfig?.sftpHost !== undefined ? activeConfig.sftpHost : '',
      sftpPort: activeConfig?.sftpPort !== undefined ? activeConfig.sftpPort : 22,
      sftpUsername: activeConfig?.sftpUsername !== undefined ? activeConfig.sftpUsername : '',
      sftpPassword: activeConfig?.sftpPassword !== undefined ? activeConfig.sftpPassword : '',
      sftpSshKey: activeConfig?.sftpSshKey !== undefined ? activeConfig.sftpSshKey : '',
      sftpFolder: activeConfig?.sftpFolder !== undefined ? activeConfig.sftpFolder : '',
      sftpFileMask: activeConfig?.sftpFileMask !== undefined ? activeConfig.sftpFileMask : '*.txt',
      sftpMaxConnectionPoolSize: activeConfig?.sftpMaxConnectionPoolSize !== undefined ? activeConfig.sftpMaxConnectionPoolSize : 5,
      sftpFileBufferSize: activeConfig?.sftpFileBufferSize !== undefined ? activeConfig.sftpFileBufferSize : 8192,
      // SQL Server properties
      sqlServerName: this.sqlServerName,
      sqlDatabaseName: this.sqlDatabaseName,
      sqlUserName: this.sqlUserName,
      sqlPassword: this.sqlPassword,
      sqlIntegratedSecurity: this.sqlIntegratedSecurity,
      sqlResourceGroup: this.sqlResourceGroup,
      sqlPollingStatement: this.sqlPollingStatement,
      sqlPollingInterval: this.sqlPollingInterval,
      sqlUseTransaction: this.sqlUseTransaction,
      sqlBatchSize: this.sqlBatchSize,
      adapterInstanceGuid: sourceInstance?.adapterInstanceGuid !== undefined
        ? sourceInstance.adapterInstanceGuid
        : (activeConfig?.sourceAdapterInstanceGuid !== undefined ? activeConfig.sourceAdapterInstanceGuid : this.sourceAdapterInstanceGuid)
    };

    console.log('openSourceAdapterSettingsDialog - dialogData.isEnabled:', dialogData.isEnabled);
    console.log('openSourceAdapterSettingsDialog - dialogData.receiveFolder:', dialogData.receiveFolder);
    console.log('openSourceAdapterSettingsDialog - dialogData.fileMask:', dialogData.fileMask);

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

        // Update enabled status if provided (always save to ensure backend is updated)
        let enabledWasChanged = false;
        if (result.isEnabled !== undefined) {
          const previousValue = this.sourceIsEnabled;
          this.sourceIsEnabled = result.isEnabled;
          enabledWasChanged = true;
          // Always call onSourceEnabledChange to save, even if value appears unchanged
          // This ensures the backend is updated with the exact value from the dialog
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

        if (result.csvPollingInterval !== undefined && result.csvPollingInterval !== this.csvPollingInterval) {
          this.csvPollingInterval = result.csvPollingInterval;
          this.updateCsvPollingInterval(result.csvPollingInterval);
        }

        // Update CSV adapter type if changed (only for CSV adapters)
        if (result.csvAdapterType !== undefined && result.csvAdapterType !== activeConfig?.csvAdapterType) {
          // Note: csvAdapterType update endpoint may need to be created
          // For now, reload configurations after other updates complete
          this.loadInterfaceConfigurations();
        }

        // Update CSV data if changed (only for CSV adapters with RAW type)
        if (result.csvData !== undefined && result.csvData !== this.csvDataText) {
          this.transportService.updateCsvData(this.getActiveInterfaceName(), result.csvData).subscribe({
            next: () => {
              this.csvDataText = result.csvData;
              this.loadInterfaceConfigurations();
              this.snackBar.open('CSV Data aktualisiert', 'OK', { duration: 3000 });
            },
            error: (error) => {
              console.error('Error updating CSV data:', error);
              const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren der CSV Data');
              this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
            }
          });
        }

        // Update SFTP properties if changed (only for CSV adapters with SFTP type)
        if (this.sourceAdapterName === 'CSV' && result.csvAdapterType === 'SFTP') {
          const sftpChanged = result.sftpHost !== undefined || result.sftpPort !== undefined ||
            result.sftpUsername !== undefined || result.sftpPassword !== undefined ||
            result.sftpSshKey !== undefined || result.sftpFolder !== undefined ||
            result.sftpFileMask !== undefined || result.sftpMaxConnectionPoolSize !== undefined ||
            result.sftpFileBufferSize !== undefined;
          
          if (sftpChanged) {
            // Note: SFTP properties update endpoint may need to be created
            // For now, reload configurations after other updates complete
            this.loadInterfaceConfigurations();
          }
        }

        // Update SQL Server transaction properties if changed (only for SqlServer adapters)
        if (this.sourceAdapterName === 'SqlServer' && 
            (result.sqlUseTransaction !== undefined || result.sqlBatchSize !== undefined)) {
          const useTransactionChanged = result.sqlUseTransaction !== undefined && 
            result.sqlUseTransaction !== this.sqlUseTransaction;
          const batchSizeChanged = result.sqlBatchSize !== undefined && 
            result.sqlBatchSize !== this.sqlBatchSize;
          
          if (useTransactionChanged || batchSizeChanged) {
            this.sqlUseTransaction = result.sqlUseTransaction !== undefined ? result.sqlUseTransaction : this.sqlUseTransaction;
            this.sqlBatchSize = result.sqlBatchSize !== undefined ? result.sqlBatchSize : this.sqlBatchSize;
            this.transportService.updateSqlTransactionProperties(
              this.getActiveInterfaceName(),
              result.sqlUseTransaction,
              result.sqlBatchSize
            ).subscribe({
              next: () => {
                this.loadInterfaceConfigurations();
                this.snackBar.open('SQL Transaction Properties aktualisiert', 'OK', { duration: 3000 });
              },
              error: (error) => {
                console.error('Error updating SQL transaction properties:', error);
                const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren der SQL Transaction Properties');
                this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
                // Restore previous values
                this.loadInterfaceConfigurations();
              }
            });
          }
        }

        // Reload configuration after all saves to ensure local state is synced with backend
        // But skip if we just saved enabled status (onSourceEnabledChange already reloads)
        const interfaceName = this.getActiveInterfaceName();
        if (interfaceName && !enabledWasChanged) {
          // Save the current enabled state before reload to preserve it
          const savedEnabledState = this.sourceIsEnabled;
          
          this.transportService.getInterfaceConfiguration(interfaceName).subscribe({
            next: (freshConfig) => {
              if (freshConfig) {
                // Update local cache
                const index = this.interfaceConfigurations.findIndex(c => c.interfaceName === interfaceName);
                if (index >= 0) {
                  this.interfaceConfigurations[index] = freshConfig;
                }
                // Sync local state from fresh config (but preserve enabled state we just saved)
                // Don't overwrite enabled if we just saved it - use the saved value instead
                this.sourceIsEnabled = savedEnabledState; // Preserve the saved enabled state
                // Also update the cache to reflect the saved enabled state
                if (index >= 0) {
                  this.applyEnabledStateToSourceConfig(this.interfaceConfigurations[index], savedEnabledState, {
                    adapterInstanceGuid: this.sourceAdapterInstanceGuid,
                    instanceName: this.sourceInstanceName
                  });
                } else {
                  this.applyEnabledStateToSourceConfig(freshConfig, savedEnabledState, {
                    adapterInstanceGuid: this.sourceAdapterInstanceGuid,
                    instanceName: this.sourceInstanceName
                  });
                }
                this.sourceInstanceName = freshConfig.sourceInstanceName || this.sourceInstanceName;
                this.sourceReceiveFolder = freshConfig.sourceReceiveFolder || this.sourceReceiveFolder;
                this.sourceFileMask = freshConfig.sourceFileMask || this.sourceFileMask;
                this.sourceBatchSize = freshConfig.sourceBatchSize ?? this.sourceBatchSize;
                this.sourceFieldSeparator = freshConfig.sourceFieldSeparator || this.sourceFieldSeparator;
                this.csvPollingInterval = freshConfig.csvPollingInterval ?? this.csvPollingInterval;
                const refreshedInstance = this.findSourceInstance(freshConfig, {
                  adapterInstanceGuid: freshConfig?.sourceAdapterInstanceGuid,
                  instanceName: freshConfig?.sourceInstanceName
                });
                if (refreshedInstance?.adapterInstanceGuid) {
                  this.sourceAdapterInstanceGuid = refreshedInstance.adapterInstanceGuid;
                } else if (freshConfig.sourceAdapterInstanceGuid) {
                  this.sourceAdapterInstanceGuid = freshConfig.sourceAdapterInstanceGuid;
                }
                
                // Always sync CSV data with backend (backend is source of truth)
                // Sample data is only initialized ONCE when CSV adapter instance is first created
                const incomingCsvData = freshConfig.csvData || '';
                if (incomingCsvData && incomingCsvData.trim().length > 0) {
                  // Backend has CSV data - sync local data with backend
                  // Don't mark as initialized - backend CSV data should remain refreshable
                  if (incomingCsvData !== this.lastSyncedCsvData) {
                    this.lastSyncedCsvData = incomingCsvData;
                    this.applyCsvDataLocally(incomingCsvData);
                  }
                } else {
                  // Backend has no CSV data - generate sample data ONCE and write to backend
                  // This only happens when CSV adapter instance is first created
                  this.ensureCsvDataInitialized(interfaceName, incomingCsvData);
                }
              }
            },
            error: (error) => {
              console.error('Error reloading configuration after save:', error);
            }
          });
        } else if (interfaceName && enabledWasChanged) {
          // If enabled status was changed, ensure the saved value is preserved in cache
          const savedEnabledState = this.sourceIsEnabled;
          const index = this.interfaceConfigurations.findIndex(c => c.interfaceName === interfaceName);
          if (index >= 0) {
            this.applyEnabledStateToSourceConfig(this.interfaceConfigurations[index], savedEnabledState, {
              adapterInstanceGuid: this.sourceAdapterInstanceGuid,
              instanceName: this.sourceInstanceName
            });
          }
        }
      }
    });
  }

  openDestinationAdapterSettings(): void {
    // Check if feature is enabled
    if (!this.ensureDestinationAdapterUIFeatureEnabled()) {
      return;
    }
    // Reload configuration to ensure we have the latest values from the backend
    const interfaceName = this.getActiveInterfaceName();
    if (!interfaceName) {
      return;
    }

    // Fetch fresh configuration from backend before opening dialog
    this.transportService.getInterfaceConfiguration(interfaceName).subscribe({
      next: (freshConfig) => {
        if (freshConfig) {
          // Update local cache
          const index = this.interfaceConfigurations.findIndex(c => c.interfaceName === interfaceName);
          if (index >= 0) {
            this.interfaceConfigurations[index] = freshConfig;
          } else {
            this.interfaceConfigurations.push(freshConfig);
          }
          
          // Sync local state from fresh config
          this.destinationIsEnabled = freshConfig.destinationIsEnabled ?? this.destinationIsEnabled;
          this.destinationInstanceName = freshConfig.destinationInstanceName || this.destinationInstanceName;
          this.destinationReceiveFolder = freshConfig.destinationReceiveFolder || this.destinationReceiveFolder;
          this.destinationFileMask = freshConfig.destinationFileMask || this.destinationFileMask;
          this.destinationAdapterInstanceGuid = freshConfig.destinationAdapterInstanceGuid || this.destinationAdapterInstanceGuid;
          
          // Now open the dialog with fresh data
          this.openDestinationAdapterSettingsDialog(freshConfig);
        } else {
          // Fallback to cached config if fresh fetch fails
          const activeConfig = this.getInterfaceConfig();
          this.openDestinationAdapterSettingsDialog(activeConfig);
        }
      },
      error: (error) => {
        console.error('Error loading fresh configuration:', error);
        // Fallback to cached config if fetch fails
        const activeConfig = this.getInterfaceConfig();
        this.openDestinationAdapterSettingsDialog(activeConfig);
      }
    });
  }

  private openDestinationAdapterSettingsDialog(activeConfig: any): void {
    // Get available source adapters for subscription selection
    const interfaceName = this.getActiveInterfaceName();
    const availableSourceAdapters: Array<{ guid: string; name: string; adapterName: string }> = [];
    
    if (interfaceName && activeConfig?.sources) {
      // Load source adapters from the interface configuration
      Object.values(activeConfig.sources).forEach((source: any) => {
        if (source && source.adapterInstanceGuid) {
          availableSourceAdapters.push({
            guid: source.adapterInstanceGuid,
            name: source.instanceName || 'Source',
            adapterName: source.adapterName || 'CSV'
          });
        }
      });
    }
    
    // Get destination adapter instance properties if available
    let jqScriptFile = '';
    let sourceAdapterSubscription = '';
    let insertStatement = '';
    let updateStatement = '';
    let deleteStatement = '';
    
    if (interfaceName && activeConfig?.destinations) {
      const destInstance = Object.values(activeConfig.destinations).find((d: any) => 
        d && d.adapterInstanceGuid === this.destinationAdapterInstanceGuid
      ) as any;
      
      if (destInstance) {
        jqScriptFile = destInstance.jqScriptFile || '';
        sourceAdapterSubscription = destInstance.sourceAdapterSubscription || '';
        insertStatement = destInstance.insertStatement || '';
        updateStatement = destInstance.updateStatement || '';
        deleteStatement = destInstance.deleteStatement || '';
      }
    }
    
    const dialogData: AdapterPropertiesData = {
      adapterType: 'Destination',
      adapterName: activeConfig?.destinationAdapterName === 'CSV' ? 'CSV' : 'SqlServer',
      instanceName: this.destinationInstanceName,
      // Read isEnabled from config (backend) instead of local state
      isEnabled: activeConfig?.destinationIsEnabled ?? this.destinationIsEnabled,
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
      adapterInstanceGuid: this.destinationAdapterInstanceGuid,
      // JQ Transformation properties
      jqScriptFile: jqScriptFile,
      sourceAdapterSubscription: sourceAdapterSubscription,
      // SQL Server custom statements
      insertStatement: insertStatement,
      updateStatement: updateStatement,
      deleteStatement: deleteStatement,
      // Available source adapters
      availableSourceAdapters: availableSourceAdapters
    };

    const dialogRef = this.dialog.open(AdapterPropertiesDialogComponent, {
      width: '600px',
      data: dialogData
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        // IMPORTANT: This Save handler ONLY updates adapter instance properties persistently.
        // It does NOT start transport or trigger any processing.
        // Processing happens automatically when adapters run on their timer schedules.
        
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

        // Update JQ Script File if changed (only for Destination adapters)
        if (result.jqScriptFile !== undefined) {
          this.updateDestinationJQScriptFile(result.jqScriptFile);
        }

        // Update Source Adapter Subscription if changed (only for Destination adapters)
        if (result.sourceAdapterSubscription !== undefined) {
          this.updateDestinationSourceAdapterSubscription(result.sourceAdapterSubscription);
        }

        // Update SQL Server custom statements if changed (only for Destination SqlServer adapters)
        if (result.insertStatement !== undefined || result.updateStatement !== undefined || result.deleteStatement !== undefined) {
          this.updateDestinationSqlStatements(
            result.insertStatement,
            result.updateStatement,
            result.deleteStatement
          );
        }

        // Reload configuration after all saves to ensure local state is synced with backend
        const interfaceName = this.getActiveInterfaceName();
        if (interfaceName) {
          this.transportService.getInterfaceConfiguration(interfaceName).subscribe({
            next: (freshConfig) => {
              if (freshConfig) {
                // Update local cache
                const index = this.interfaceConfigurations.findIndex(c => c.interfaceName === interfaceName);
                if (index >= 0) {
                  this.interfaceConfigurations[index] = freshConfig;
                }
                // Sync local state from fresh config
                this.destinationIsEnabled = freshConfig.destinationIsEnabled ?? this.destinationIsEnabled;
                this.destinationInstanceName = freshConfig.destinationInstanceName || this.destinationInstanceName;
                this.destinationReceiveFolder = freshConfig.destinationReceiveFolder || this.destinationReceiveFolder;
                this.destinationFileMask = freshConfig.destinationFileMask || this.destinationFileMask;
                this.destinationAdapterInstanceGuid = freshConfig.destinationAdapterInstanceGuid || this.destinationAdapterInstanceGuid;
              }
            },
            error: (error) => {
              console.error('Error reloading configuration after save:', error);
            }
          });
        }
      }
    });
  }

  updateFieldSeparator(fieldSeparator?: string): void {
    const fieldSeparatorValue = fieldSeparator ?? this.sourceFieldSeparator;
    const interfaceName = this.getActiveInterfaceName();
    const activeConfig = this.getInterfaceConfig(interfaceName);
    
    if (!activeConfig) {
      return;
    }

    const normalizedSeparator = fieldSeparatorValue.trim() || '║';
    if (normalizedSeparator === (activeConfig.sourceFieldSeparator || '║')) {
      return;
    }

    this.sourceFieldSeparator = normalizedSeparator; // Update immediately for responsive UI

    this.transportService.updateFieldSeparator(interfaceName, normalizedSeparator).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
        this.snackBar.open('Field Separator aktualisiert', 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating field separator:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren des Field Separators');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
        // Restore previous value
        this.sourceFieldSeparator = activeConfig.sourceFieldSeparator || '║';
      }
    });
  }

  updateCsvPollingInterval(pollingInterval?: number): void {
    const interfaceName = this.getActiveInterfaceName();
    const activeConfig = this.getInterfaceConfig(interfaceName);

    if (!activeConfig) {
      return;
    }

    const normalizedInterval = Math.max(1, Math.floor(pollingInterval ?? this.csvPollingInterval ?? 10));
    if (normalizedInterval === (activeConfig.csvPollingInterval ?? 10)) {
      this.csvPollingInterval = normalizedInterval;
      return;
    }

    this.csvPollingInterval = normalizedInterval;

    this.transportService.updateCsvPollingInterval(interfaceName, normalizedInterval).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
        this.snackBar.open('Polling-Intervall aktualisiert', 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating CSV polling interval:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren des Polling-Intervalls');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
        this.csvPollingInterval = activeConfig.csvPollingInterval ?? 10;
      }
    });
  }

  updateDestinationReceiveFolder(destinationReceiveFolder?: string): void {
    const destinationReceiveFolderValue = destinationReceiveFolder ?? this.destinationReceiveFolder;
    const interfaceName = this.getActiveInterfaceName();
    const activeConfig = this.getInterfaceConfig(interfaceName);
    
    if (!activeConfig) {
      return;
    }

    const normalizedFolder = destinationReceiveFolderValue.trim() || '';
    if (normalizedFolder === (activeConfig.destinationReceiveFolder || '')) {
      return;
    }

    this.destinationReceiveFolder = normalizedFolder; // Update immediately for responsive UI

    this.transportService.updateDestinationReceiveFolder(interfaceName, normalizedFolder).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
        this.snackBar.open('Destination Receive Folder aktualisiert', 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating destination receive folder:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren des Destination Receive Folders');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
        // Restore previous value
        this.destinationReceiveFolder = activeConfig.destinationReceiveFolder || '';
      }
    });
  }

  updateDestinationJQScriptFile(jqScriptFile?: string): void {
    const jqScriptFileValue = jqScriptFile ?? '';
    const interfaceName = this.getActiveInterfaceName();
    
    if (!interfaceName) {
      return;
    }

    this.transportService.updateDestinationJQScriptFile(interfaceName, this.destinationAdapterInstanceGuid, jqScriptFileValue).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
        this.snackBar.open('JQ Script File aktualisiert', 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating JQ Script File:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren des JQ Script Files');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
      }
    });
  }

  updateDestinationSourceAdapterSubscription(sourceAdapterSubscription?: string): void {
    const sourceAdapterSubscriptionValue = sourceAdapterSubscription ?? '';
    const interfaceName = this.getActiveInterfaceName();
    
    if (!interfaceName) {
      return;
    }

    this.transportService.updateDestinationSourceAdapterSubscription(interfaceName, this.destinationAdapterInstanceGuid, sourceAdapterSubscriptionValue).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
        this.snackBar.open('Source Adapter Subscription aktualisiert', 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating Source Adapter Subscription:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren der Source Adapter Subscription');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
      }
    });
  }

  updateDestinationSqlStatements(insertStatement?: string, updateStatement?: string, deleteStatement?: string): void {
    const interfaceName = this.getActiveInterfaceName();
    
    if (!interfaceName) {
      return;
    }

    this.transportService.updateDestinationSqlStatements(
      interfaceName,
      this.destinationAdapterInstanceGuid,
      insertStatement ?? '',
      updateStatement ?? '',
      deleteStatement ?? ''
    ).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
        this.snackBar.open('SQL Statements aktualisiert', 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating SQL Statements:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren der SQL Statements');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
      }
    });
  }

  updateDestinationFileMask(destinationFileMask?: string): void {
    const destinationFileMaskValue = destinationFileMask ?? this.destinationFileMask;
    const interfaceName = this.getActiveInterfaceName();
    const activeConfig = this.getInterfaceConfig(interfaceName);
    
    if (!activeConfig) {
      return;
    }

    const normalizedMask = destinationFileMaskValue.trim() || '*.txt';
    if (normalizedMask === (activeConfig.destinationFileMask || '*.txt')) {
      return;
    }

    this.destinationFileMask = normalizedMask; // Update immediately for responsive UI

    this.transportService.updateDestinationFileMask(interfaceName, normalizedMask).subscribe({
      next: () => {
        this.loadInterfaceConfigurations();
        this.snackBar.open('Destination File Mask aktualisiert', 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating destination file mask:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren der Destination File Mask');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
        // Restore previous value
        this.destinationFileMask = activeConfig.destinationFileMask || '*.txt';
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
    if (selectedName && selectedName !== this.currentInterfaceName) {
      this.currentInterfaceName = selectedName;
      const config = this.interfaceConfigurations.find(c => c.interfaceName === selectedName);
      if (config) {
        // Check if this is a placeholder that needs to be created
        if (config._isPlaceholder) {
          // Create the default interface configuration
          this.transportService.createInterfaceConfiguration({
            interfaceName: this.DEFAULT_INTERFACE_NAME,
            sourceAdapterName: 'CSV',
            sourceConfiguration: JSON.stringify({ source: 'csv-files/csv-incoming' }),
            destinationAdapterName: 'SqlServer',
            destinationConfiguration: JSON.stringify({ destination: 'TransportData' }),
            description: 'Default CSV to SQL Server interface'
          }).subscribe({
            next: (createdConfig) => {
              // Reload configurations to get the real one
              this.loadInterfaceConfigurations();
              this.snackBar.open('Default interface created successfully', 'OK', { duration: 3000 });
            },
            error: (error) => {
              console.error('Error creating default interface:', error);
              const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Erstellen des Standard-Interfaces');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
            }
          });
          this.selectedInterfaceConfig = null;
        } else {
          this.selectedInterfaceConfig = config;
          this.loadInterfaceData();
        }
      } else {
        this.selectedInterfaceConfig = null;
      }
    }
  }

  loadInterfaceData(): void {
    if (!this.currentInterfaceName) return;

    this.applyDefaultInterfaceState();
    
    const config = this.interfaceConfigurations.find(c => c.interfaceName === this.currentInterfaceName);
    if (!config || config._isPlaceholder) {
      this.selectedInterfaceConfig = null;
      // Don't populate sample CSV data here for placeholder - wait for real adapter instance creation
      return;
    }

    this.selectedInterfaceConfig = config;
    // Load all data for the selected interface
    this.sourceInstanceName = config.sourceInstanceName || this.sourceInstanceName;
    this.destinationInstanceName = config.destinationInstanceName || this.destinationInstanceName;
    
    const primarySourceInstance = this.findSourceInstance(config, {
      adapterInstanceGuid: config?.sourceAdapterInstanceGuid || this.sourceAdapterInstanceGuid,
      instanceName: config?.sourceInstanceName || this.sourceInstanceName
    });
    
    // Preserve enabled state if we just saved it (within last 5 seconds)
    const wasRecentlySaved = this.lastEnabledSaveTime[config.interfaceName] && 
      (Date.now() - this.lastEnabledSaveTime[config.interfaceName]) < 5000;
    if (!wasRecentlySaved) {
      const enabledFromConfig = this.getEffectiveSourceEnabled(config, {
        adapterInstanceGuid: primarySourceInstance?.adapterInstanceGuid,
        instanceName: primarySourceInstance?.instanceName
      });
      this.sourceIsEnabled = enabledFromConfig !== undefined ? enabledFromConfig : false;
    }
    this.destinationIsEnabled = config.destinationIsEnabled ?? this.destinationIsEnabled;
    this.sourceAdapterName = (config.sourceAdapterName === 'SqlServer' ? 'SqlServer' : 'CSV') as 'CSV' | 'SqlServer';
    this.sourceReceiveFolder = config.sourceReceiveFolder || this.sourceReceiveFolder;
    this.sourceFileMask = config.sourceFileMask || this.sourceFileMask;
    this.sourceBatchSize = config.sourceBatchSize ?? this.sourceBatchSize;
    this.sourceFieldSeparator = config.sourceFieldSeparator || this.sourceFieldSeparator;
    this.csvPollingInterval = config.csvPollingInterval ?? 10;
    this.destinationReceiveFolder = config.destinationReceiveFolder || this.destinationReceiveFolder;
    this.destinationFileMask = config.destinationFileMask || this.destinationFileMask;
    if (primarySourceInstance?.adapterInstanceGuid) {
      this.sourceAdapterInstanceGuid = primarySourceInstance.adapterInstanceGuid;
    } else if (config.sourceAdapterInstanceGuid) {
      // Fallback to old structure
      this.sourceAdapterInstanceGuid = config.sourceAdapterInstanceGuid;
    }
    this.destinationAdapterInstanceGuid = config.destinationAdapterInstanceGuid || '';
    
    // If source adapter is enabled but GUID is missing, reload configuration from API
    // The backend generates GUIDs automatically when loading configurations
    if (this.sourceIsEnabled && !this.sourceAdapterInstanceGuid) {
      this.transportService.getInterfaceConfiguration(this.currentInterfaceName).subscribe({
        next: (freshConfig) => {
          if (freshConfig && freshConfig.sourceAdapterInstanceGuid) {
            // Update local cache and reload interface data
            const index = this.interfaceConfigurations.findIndex(c => c.interfaceName === this.currentInterfaceName);
            if (index >= 0) {
              this.interfaceConfigurations[index] = freshConfig;
            }
            this.loadInterfaceData(); // Recursively reload to pick up the GUID
          }
        },
        error: (error) => {
          console.warn('Error reloading interface configuration to get GUID:', error);
        }
      });
      return; // Exit early, will reload after API call completes
    }
    
    // Load SQL Server properties
    this.sqlServerName = config.sqlServerName || this.sqlServerName;
    this.sqlDatabaseName = config.sqlDatabaseName || this.sqlDatabaseName;
    this.sqlUserName = config.sqlUserName || this.sqlUserName;
    this.sqlPassword = config.sqlPassword || this.sqlPassword;
    this.sqlIntegratedSecurity = config.sqlIntegratedSecurity ?? this.sqlIntegratedSecurity;
    this.sqlResourceGroup = config.sqlResourceGroup || this.sqlResourceGroup;
    this.sqlPollingStatement = config.sqlPollingStatement || this.sqlPollingStatement;
    this.sqlPollingInterval = config.sqlPollingInterval ?? this.sqlPollingInterval;
    this.sqlUseTransaction = config.sqlUseTransaction ?? this.sqlUseTransaction;
    this.sqlBatchSize = config.sqlBatchSize ?? this.sqlBatchSize;
    
    // Load CsvData property - always sync with backend
    // Sample data is only initialized ONCE when CSV adapter instance is first created
    // Try new structure first (sources dictionary)
    let incomingCsvData = '';
    if (config.sources && Object.keys(config.sources).length > 0) {
      const sourceInstance = Object.values(config.sources)[0] as any;
      incomingCsvData = sourceInstance?.csvData || sourceInstance?.CsvData || '';
    }
    // Fallback to old structure
    if (!incomingCsvData) {
      incomingCsvData = config.csvData || config.CsvData || '';
    }
    
    if (incomingCsvData && incomingCsvData.trim().length > 0) {
      // Backend has CSV data - always use it (backend is source of truth)
      // Don't mark as initialized - backend CSV data should remain refreshable
      if (incomingCsvData !== this.lastSyncedCsvData) {
        this.lastSyncedCsvData = incomingCsvData;
        this.applyCsvDataLocally(incomingCsvData);
      }
    } else {
      // Backend has no CSV data - generate sample data ONCE and write to backend
      // This only happens when CSV adapter instance is first created
      this.ensureCsvDataInitialized(config.interfaceName, incomingCsvData);
    }
    this.isCsvDataUpdateInFlight = false;
    
    // Reload adapter instances and data
    this.loadDestinationAdapterInstances();
    this.loadSqlData();
    
    // Do NOT automatically start transport - user must explicitly start it via Start Transport button
    // Transport is only started when user clicks the "Start Transport" button
  }

  openAddInterfaceDialog(): void {
    const existingNames = this.interfaceConfigurations.map(c => c.interfaceName || '').filter(n => n);
    
    const dialogData: AddInterfaceDialogData = { existingNames };
    const dialogRef = this.dialog.open<AddInterfaceDialogComponent, AddInterfaceDialogData, string>(AddInterfaceDialogComponent, {
      width: '500px',
      data: dialogData
    });

    dialogRef.afterClosed().subscribe((interfaceName: string | undefined) => {
      if (!interfaceName) {
        // User cancelled the dialog
        return;
      }

      // Create new interface configuration
      this.transportService.createInterfaceConfiguration({
        interfaceName: interfaceName,
        sourceAdapterName: 'CSV',
        destinationAdapterName: 'SqlServer'
      }).subscribe({
        next: (createdConfig) => {
          // The API returns the created configuration - use it directly
          if (createdConfig && createdConfig.interfaceName) {
            // Remove any placeholder with the same name
            this.interfaceConfigurations = this.interfaceConfigurations.filter(
              c => c.interfaceName !== interfaceName || !c._isPlaceholder
            );
            
            // Add the real configuration from the API response
            // Normalize the config to match our expected format
            const normalizedConfig: any = {
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
            this.interfaceConfigurations = [
              ...this.interfaceConfigurations,
              normalizedConfig
            ].sort((a, b) => a.interfaceName.localeCompare(b.interfaceName, undefined, { sensitivity: 'base' }));
            
            // Select the new interface
            this.currentInterfaceName = normalizedConfig.interfaceName;
            this.selectedInterfaceConfig = null;
            
            this.snackBar.open('Interface created successfully', 'OK', { duration: 3000 });
            
            // Reload to get full configuration with all properties
            this.loadInterfaceConfigurations();
          } else {
            // Fallback: if API doesn't return config, reload from server
            this.snackBar.open('Interface created successfully', 'OK', { duration: 3000 });
            this.currentInterfaceName = interfaceName;
            this.selectedInterfaceConfig = null;
            this.loadInterfaceConfigurations();
          }
        },
        error: (error) => {
          console.error('Error creating interface:', error);
          const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Erstellen des Interfaces');
          this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
        }
      });
    });
  }

  removeCurrentInterface(): void {
    if (!this.currentInterfaceName) return;
    
    const confirmMessage = `Are you sure you want to delete interface "${this.currentInterfaceName}"?`;
    if (!confirm(confirmMessage)) return;

    this.transportService.deleteInterfaceConfiguration(this.currentInterfaceName).subscribe({
      next: () => {
        this.snackBar.open('Interface deleted successfully', 'OK', { duration: 3000 });
        this.currentInterfaceName = '';
        this.selectedInterfaceConfig = null;
        this.applyDefaultInterfaceState();
        this.loadInterfaceConfigurations();
      },
      error: (error) => {
        console.error('Error deleting interface:', error);
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Löschen des Interfaces');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
      }
    });
  }

  private ensureInterfaceSelection(): void {
    if (!this.interfaceConfigurations || this.interfaceConfigurations.length === 0) {
      this.currentInterfaceName = this.DEFAULT_INTERFACE_NAME;
      this.selectedInterfaceConfig = null;
      this.applyDefaultInterfaceState();
      // Don't populate sample CSV data here - it will be initialized when CSV adapter instance is created
      return;
    }

    if (!this.currentInterfaceName) {
      this.currentInterfaceName = this.interfaceConfigurations[0].interfaceName;
    } else {
      const exists = this.interfaceConfigurations.some(c => c.interfaceName === this.currentInterfaceName);
      if (!exists) {
        this.currentInterfaceName = this.interfaceConfigurations[0].interfaceName;
      }
    }

    this.loadInterfaceData();
  }

  private applyDefaultInterfaceState(): void {
    this.sourceAdapterName = 'CSV';
    this.sourceInstanceName = 'Source';
    this.sourceIsEnabled = false;
    this.sourceReceiveFolder = '';
    this.sourceFileMask = '*.txt';
    this.sourceBatchSize = 1000;
    this.sourceFieldSeparator = '║';
    this.sourceAdapterInstanceGuid = '';

    this.destinationInstanceName = 'Destination';
    this.destinationIsEnabled = true;
    this.destinationReceiveFolder = '';
    this.destinationFileMask = '*.txt';
    this.destinationAdapterInstanceGuid = '';
    this.destinationAdapterInstances = [];
    this.destinationCardExpandedStates.clear();

    this.csvData = [];
    this.csvDataText = '';
    this.editableCsvText = '';
    this.formattedCsvHtml = '';
    this.lastSyncedCsvData = '';
    this.isCsvDataUpdateInFlight = false;
    this.tableExists = false;

    this.sqlServerName = '';
    this.sqlDatabaseName = '';
    this.sqlUserName = '';
    this.sqlPassword = '';
    this.sqlIntegratedSecurity = false;
    this.sqlResourceGroup = '';
    this.sqlPollingStatement = '';
    this.sqlPollingInterval = 60;
    this.sqlUseTransaction = false;
    this.sqlBatchSize = 1000;
    this.csvDataInitialization.delete(this.getActiveInterfaceName());
    this.csvPollingInterval = 10;
  }

  showInterfaceJson(): void {
    if (!this.currentInterfaceName) {
      this.snackBar.open('Please select an interface first', 'OK', { duration: 3000 });
      return;
    }

    // First, try to use selectedInterfaceConfig if available and not a placeholder
    let configToUse = this.selectedInterfaceConfig && !this.selectedInterfaceConfig._isPlaceholder 
      ? this.selectedInterfaceConfig 
      : null;

    // If not available, try to find it in interfaceConfigurations (non-placeholder)
    // Make sure we get the REAL interface, not a placeholder
    if (!configToUse) {
      // Find all interfaces with this name, prefer non-placeholder
      const allMatching = this.interfaceConfigurations.filter(c => 
        c.interfaceName === this.currentInterfaceName
      );
      configToUse = allMatching.find(c => !c._isPlaceholder) || null;
    }

    // If we found a real config locally, use it
    if (configToUse && !configToUse._isPlaceholder) {
      this.transportService.getDestinationAdapterInstances(this.currentInterfaceName).subscribe({
        next: (instances) => {
          const fullConfig = {
            ...configToUse,
            destinationAdapterInstances: instances || []
          };
          this.openJsonViewDialog(fullConfig);
        },
        error: (error) => {
          console.error('Error loading destination adapter instances:', error);
          // Show dialog with just the interface config (without instances)
          this.openJsonViewDialog(configToUse);
        }
      });
      return;
    }

    // If not found locally, check for placeholder or try API
    // First check if we have any interface data (including placeholders) in interfaceConfigurations
    const anyMatchingConfig = this.interfaceConfigurations.find(c => 
      c.interfaceName === this.currentInterfaceName
    );
    
    if (anyMatchingConfig) {
      // We have some data locally (real or placeholder), use it
      if (anyMatchingConfig._isPlaceholder) {
        // For placeholders, show the placeholder data as JSON
        this.openJsonViewDialog(anyMatchingConfig);
        return;
      }
      
      // For real interfaces, try to load destination adapter instances
      this.transportService.getDestinationAdapterInstances(this.currentInterfaceName).subscribe({
        next: (instances) => {
          const fullConfig = {
            ...anyMatchingConfig,
            destinationAdapterInstances: instances || []
          };
          this.openJsonViewDialog(fullConfig);
        },
        error: (error) => {
          console.error('Error loading destination adapter instances:', error);
          // Show dialog with just the interface config (without instances)
          this.openJsonViewDialog(anyMatchingConfig);
        }
      });
      return;
    }
    
    // No local data found, try API
    this.transportService.getInterfaceConfiguration(this.currentInterfaceName).subscribe({
      next: (config) => {
        // Successfully loaded from API - update local cache and show JSON
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
        
        // Check if this is ONLY a placeholder (no real interface exists)
        const hasPlaceholder = this.interfaceConfigurations.some(c => 
          c.interfaceName === this.currentInterfaceName && c._isPlaceholder
        );
        
        if (hasPlaceholder) {
          // Show placeholder data as JSON instead of error message
          const placeholderConfig = this.interfaceConfigurations.find(c => 
            c.interfaceName === this.currentInterfaceName && c._isPlaceholder
          );
          if (placeholderConfig) {
            this.openJsonViewDialog(placeholderConfig);
            return;
          }
        }
        
        // Otherwise show the actual error
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Laden der Interface-Konfiguration');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
      }
    });
  }

  private formatHierarchicalJson(obj: any, indent: string = '', parentKey: string = ''): string {
    if (obj === null) return 'null';
    if (typeof obj === 'string') return JSON.stringify(obj);
    if (typeof obj === 'number' || typeof obj === 'boolean') return String(obj);
    
    if (Array.isArray(obj)) {
      if (obj.length === 0) return '[]';
      const items = obj.map(item => `${indent}  ${this.formatHierarchicalJson(item, indent + '  ', parentKey)}`).join(',\n');
      return `[\n${items}\n${indent}]`;
    }
    
    if (typeof obj === 'object') {
      const keys = Object.keys(obj);
      if (keys.length === 0) return '{}';
      
      const items = keys.map((key, index) => {
        const value = obj[key];
        const isLast = index === keys.length - 1;
        const valueStr = this.formatHierarchicalJson(value, indent + '  ', key);
        
        // Add folder-like structure for sources and destinations
        if (key === 'sources' || key === 'destinations') {
          return `${indent}  "${key}": {\n${valueStr}\n${indent}  }${isLast ? '' : ','}`;
        }
        
        // Add folder-like structure for adapter types (CSV, SqlServer) within sources/destinations
        if (parentKey === 'sources' || parentKey === 'destinations') {
          return `${indent}    "${key}": {\n${valueStr}\n${indent}    }${isLast ? '' : ','}`;
        }
        
        // Add folder-like structure for instance names within adapter types
        if ((parentKey === 'CSV' || parentKey === 'SqlServer') && typeof value === 'object' && value !== null && !Array.isArray(value)) {
          return `${indent}      "${key}": {\n${valueStr}\n${indent}      }${isLast ? '' : ','}`;
        }
        
        // Properties within instances
        if (key === 'properties' && typeof value === 'object' && value !== null && !Array.isArray(value)) {
          return `${indent}        "${key}": {\n${valueStr}\n${indent}        }${isLast ? '' : ','}`;
        }
        
        // Regular properties
        return `${indent}  "${key}": ${valueStr}${isLast ? '' : ','}`;
      }).join('\n');
      
      return `{\n${items}\n${indent}}`;
    }
    
    return JSON.stringify(obj);
  }

  private formatJsonWithComments(obj: any, indent: string = '', parentKey: string = ''): string {
    // Helper function to format JSON with comments
    const comments: { [key: string]: string } = {
      // Top-level properties
      'interfaceName': 'The name of the interface configuration',
      'sourceAdapterName': 'Type of source adapter (CSV or SqlServer)',
      'destinationAdapterName': 'Type of destination adapter (CSV or SqlServer)',
      'description': 'Description of the interface configuration',
      'sourceAdapterInstance': 'Source adapter instance configuration. Contains all properties for the source adapter.',
      'destinationAdapterInstances': 'Array of destination adapter instances. Each instance contains all properties for a destination adapter.',
      
      // Adapter instance properties (used in both source and destination)
      'adapterInstanceGuid': 'A unique identifier (GUID) assigned to this adapter instance. Used internally to track which adapter instance created each message in the Service Bus.',
      'instanceName': 'A user-friendly name to identify this adapter instance. Displayed in the UI and helps distinguish between multiple instances.',
      'adapterName': 'Type of adapter (CSV or SqlServer)',
      'isEnabled': 'Controls whether this adapter instance is active. When enabled, the adapter process runs automatically. When disabled, the process stops immediately.',
      'receiveFolder': 'The blob storage folder path where CSV files are monitored. Format: \'container-name/folder-path\' (e.g., \'csv-files/csv-incoming\'). Leave empty to disable folder monitoring.',
      'fileMask': 'Wildcard pattern to filter files in the Receive Folder. Examples: \'*.txt\', \'*.csv\', \'data_*.txt\'. Supports * (any sequence) and ? (single character). Default: \'*.txt\'.',
      'batchSize': 'Number of rows read from the CSV file in one chunk before debatching into single rows. Larger batches improve performance but use more memory. Default: 100 rows per batch.',
      'fieldSeparator': 'Character used to separate fields in CSV files. Default: \'║\' (Box Drawing Double Vertical Line, U+2551) - a seldomly used UTF-8 character that avoids conflicts with common data.',
      'destinationReceiveFolder': 'The blob storage folder path where CSV files will be written (for CSV destination adapters). Format: \'container-name/folder-path\' (e.g., \'csv-files/csv-outgoing\').',
      'destinationFileMask': 'File mask pattern for constructing output filenames (for CSV destination adapters). Supports variables: $datetime (replaced with current date/time: yyyyMMddHHmmss.fff).',
      
      // SQL Server properties
      'sqlServerName': 'SQL Server name or IP address. For Azure SQL: use the full FQDN (e.g., \'sql-server.database.windows.net\'). For on-premises: use server name or IP address.',
      'sqlDatabaseName': 'The name of the SQL Server database to connect to.',
      'sqlUserName': 'SQL login username (required when Integrated Security is disabled).',
      'sqlPassword': 'SQL login password (required when Integrated Security is disabled). Password is masked for security.',
      'sqlIntegratedSecurity': 'Use Windows Authentication (Integrated Security). When enabled, User Name and Password are not required. When disabled, SQL Authentication is used.',
      'sqlResourceGroup': 'Azure Resource Group name (for Azure SQL managed database access). Used for Azure-specific security and access management.',
      'sqlPollingStatement': 'SQL SELECT or EXEC statement to poll for new data (Source adapters only). Executed periodically according to Polling Interval. Example: \'SELECT * FROM Orders WHERE Processed = 0\'.',
      'sqlPollingInterval': 'How often the polling statement is executed (in seconds). The adapter will run the polling statement at this interval to check for new data. Default: 60 seconds.',
      'sqlUseTransaction': 'Wrap execution in an explicit SQL transaction. When enabled, all database operations are wrapped in a transaction that can be committed or rolled back. Ensures atomicity.',
      'sqlBatchSize': 'Number of rows fetched at once when reading data from SQL Server. Larger batch sizes improve performance but use more memory. Default: 1000 rows per batch.',
      'destination': 'Destination table name for SqlServer destination adapters. The table is automatically created with dynamic columns based on the CSV data structure.',
      'tableName': 'Table name for SqlServer adapters. Same as destination property.',
      'configuration': 'Raw configuration object containing adapter-specific settings.'
    };
    
    if (Array.isArray(obj)) {
      if (obj.length === 0) return '[]';
      
      // Add section comment for destinationAdapterInstances array
      let sectionComment = '';
      if (parentKey === 'destinationAdapterInstances') {
        sectionComment = `${indent}  // Destination adapter instances array\n`;
      }
      
      const items = obj.map((item, index) => {
        const itemStr = this.formatJsonWithComments(item, indent + '  ', '');
        // Add comment for each destination instance
        const instanceComment = parentKey === 'destinationAdapterInstances' 
          ? ` // Destination adapter instance ${index + 1}`
          : '';
        return `${indent}  ${itemStr}${instanceComment}`;
      }).join(',\n');
      
      return `${sectionComment}[\n${items}\n${indent}]`;
    } else if (obj !== null && typeof obj === 'object') {
      const keys = Object.keys(obj);
      if (keys.length === 0) return '{}';
      
      // Add section comment for sourceAdapterInstance
      let sectionComment = '';
      if (parentKey === '' && keys.includes('sourceAdapterInstance')) {
        sectionComment = `${indent}// Source adapter instance configuration\n`;
      }
      
      const items = keys.map((key, index) => {
        const value = obj[key];
        const comment = comments[key] || '';
        const commentStr = comment ? ` // ${comment}` : '';
        
        // Add section comment before specific keys
        let keyComment = '';
        if (key === 'sourceAdapterInstance' && parentKey === '') {
          keyComment = `${indent}  // Source adapter instance configuration\n`;
        } else if (key === 'destinationAdapterInstances' && parentKey === '') {
          keyComment = `${indent}  // Destination adapter instances array\n`;
        }
        
        let valueStr: string;
        if (value === null) {
          valueStr = 'null';
        } else if (typeof value === 'string') {
          valueStr = JSON.stringify(value);
        } else if (typeof value === 'number' || typeof value === 'boolean') {
          valueStr = String(value);
        } else {
          valueStr = this.formatJsonWithComments(value, indent + '  ', key);
        }
        
        return `${keyComment}${indent}  "${key}": ${valueStr}${commentStr}`;
      }).join(',\n');
      
      return `${sectionComment}{\n${items}\n${indent}}`;
    } else {
      return JSON.stringify(obj);
    }
  }

  private openJsonViewDialog(config: any): void {
    try {
      // Use the actual structure from the backend (as stored in the JSON file)
      // The backend now returns the structure with Sources and Destinations dictionaries
      // We just need to extract the single interface and format it properly
      
      // Build the structure exactly as it's stored in the backend JSON file
      // Handle both camelCase (from API) and PascalCase (from stored JSON) property names
      const structuredConfig: any = {
        InterfaceName: config.InterfaceName || config.interfaceName || this.currentInterfaceName || 'Unknown Interface',
        Description: config.Description !== undefined ? config.Description : (config.description !== undefined ? config.description : null),
        Sources: {},
        Destinations: {},
        CreatedAt: config.CreatedAt || config.createdAt || null,
        UpdatedAt: config.UpdatedAt !== undefined ? config.UpdatedAt : (config.updatedAt !== undefined ? config.updatedAt : null)
      };

      // Normalize Sources from new structure (config.sources or config.Sources)
      const sourcesData = config.Sources || config.sources || {};
      if (sourcesData && Object.keys(sourcesData).length > 0) {
        // Use the new hierarchical structure - normalize all properties to PascalCase
        Object.keys(sourcesData).forEach(instanceName => {
          const sourceInstance = sourcesData[instanceName];
          const adapterName = (sourceInstance.AdapterName || sourceInstance.adapterName || 'CSV').toString();
          const normalizedSource: any = {
            InstanceName: sourceInstance.InstanceName || sourceInstance.instanceName || instanceName,
            AdapterName: adapterName,
            IsEnabled: sourceInstance.IsEnabled !== undefined ? sourceInstance.IsEnabled : (sourceInstance.isEnabled !== undefined ? sourceInstance.isEnabled : false),
            AdapterInstanceGuid: sourceInstance.AdapterInstanceGuid || sourceInstance.adapterInstanceGuid || '',
            Configuration: this.getFirstDefined(sourceInstance.Configuration, sourceInstance.configuration, '')
          };

          this.assignWhenPresent(normalizedSource, 'SourceReceiveFolder', this.getFirstDefined(sourceInstance.SourceReceiveFolder, sourceInstance.sourceReceiveFolder));
          this.assignWhenPresent(normalizedSource, 'SourceFileMask', this.getFirstDefined(sourceInstance.SourceFileMask, sourceInstance.sourceFileMask));
          this.assignWhenPresent(normalizedSource, 'SourceBatchSize', this.getFirstDefined(sourceInstance.SourceBatchSize, sourceInstance.sourceBatchSize));
          this.assignWhenPresent(normalizedSource, 'SourceFieldSeparator', this.getFirstDefined(sourceInstance.SourceFieldSeparator, sourceInstance.sourceFieldSeparator));
          this.assignWhenPresent(normalizedSource, 'CsvAdapterType', this.getFirstDefined(sourceInstance.CsvAdapterType, sourceInstance.csvAdapterType, sourceInstance.adapterType));
          this.assignWhenPresent(normalizedSource, 'CsvPollingInterval', this.getFirstDefined(sourceInstance.CsvPollingInterval, sourceInstance.csvPollingInterval));
          this.assignWhenPresent(normalizedSource, 'SftpHost', this.getFirstDefined(sourceInstance.SftpHost, sourceInstance.sftpHost));
          this.assignWhenPresent(normalizedSource, 'SftpPort', this.getFirstDefined(sourceInstance.SftpPort, sourceInstance.sftpPort));
          this.assignWhenPresent(normalizedSource, 'SftpUsername', this.getFirstDefined(sourceInstance.SftpUsername, sourceInstance.sftpUsername));
          this.assignWhenPresent(normalizedSource, 'SftpPassword', (sourceInstance.SftpPassword || sourceInstance.sftpPassword) ? '***' : undefined);
          this.assignWhenPresent(normalizedSource, 'SftpSshKey', (sourceInstance.SftpSshKey || sourceInstance.sftpSshKey) ? '***' : undefined);
          this.assignWhenPresent(normalizedSource, 'SftpFolder', this.getFirstDefined(sourceInstance.SftpFolder, sourceInstance.sftpFolder));
          this.assignWhenPresent(normalizedSource, 'SftpFileMask', this.getFirstDefined(sourceInstance.SftpFileMask, sourceInstance.sftpFileMask));
          this.assignWhenPresent(normalizedSource, 'SftpMaxConnectionPoolSize', this.getFirstDefined(sourceInstance.SftpMaxConnectionPoolSize, sourceInstance.sftpMaxConnectionPoolSize));
          this.assignWhenPresent(normalizedSource, 'SftpFileBufferSize', this.getFirstDefined(sourceInstance.SftpFileBufferSize, sourceInstance.sftpFileBufferSize));

          if (this.shouldIncludeSqlProperties(adapterName)) {
            this.assignWhenPresent(normalizedSource, 'SqlServerName', this.getFirstDefined(sourceInstance.SqlServerName, sourceInstance.sqlServerName));
            this.assignWhenPresent(normalizedSource, 'SqlDatabaseName', this.getFirstDefined(sourceInstance.SqlDatabaseName, sourceInstance.sqlDatabaseName));
            this.assignWhenPresent(normalizedSource, 'SqlUserName', this.getFirstDefined(sourceInstance.SqlUserName, sourceInstance.sqlUserName));
            this.assignWhenPresent(normalizedSource, 'SqlPassword', (sourceInstance.SqlPassword || sourceInstance.sqlPassword) ? '***' : undefined);
            this.assignWhenPresent(normalizedSource, 'SqlIntegratedSecurity', this.getFirstDefined(sourceInstance.SqlIntegratedSecurity, sourceInstance.sqlIntegratedSecurity));
            this.assignWhenPresent(normalizedSource, 'SqlResourceGroup', this.getFirstDefined(sourceInstance.SqlResourceGroup, sourceInstance.sqlResourceGroup));
            this.assignWhenPresent(normalizedSource, 'SqlPollingStatement', this.getFirstDefined(sourceInstance.SqlPollingStatement, sourceInstance.sqlPollingStatement));
            this.assignWhenPresent(normalizedSource, 'SqlPollingInterval', this.getFirstDefined(sourceInstance.SqlPollingInterval, sourceInstance.sqlPollingInterval));
            this.assignWhenPresent(normalizedSource, 'SqlTableName', this.getFirstDefined(sourceInstance.SqlTableName, sourceInstance.sqlTableName));
            this.assignWhenPresent(normalizedSource, 'SqlUseTransaction', this.getFirstDefined(sourceInstance.SqlUseTransaction, sourceInstance.sqlUseTransaction));
            this.assignWhenPresent(normalizedSource, 'SqlBatchSize', this.getFirstDefined(sourceInstance.SqlBatchSize, sourceInstance.sqlBatchSize));
            this.assignWhenPresent(normalizedSource, 'SqlCommandTimeout', this.getFirstDefined(sourceInstance.SqlCommandTimeout, sourceInstance.sqlCommandTimeout));
            this.assignWhenPresent(normalizedSource, 'SqlFailOnBadStatement', this.getFirstDefined(sourceInstance.SqlFailOnBadStatement, sourceInstance.sqlFailOnBadStatement));
          }

          this.assignWhenPresent(normalizedSource, 'CreatedAt', this.getFirstDefined(sourceInstance.CreatedAt, sourceInstance.createdAt));
          this.assignWhenPresent(normalizedSource, 'UpdatedAt', this.getFirstDefined(sourceInstance.UpdatedAt, sourceInstance.updatedAt));

          structuredConfig.Sources[instanceName] = normalizedSource;
        });
      } else if (config.sourceAdapterName || config.sourceInstanceName) {
        // Fallback to old structure for backward compatibility
        const sourceInstanceName = config.sourceInstanceName || config.sourceAdapterName || 'Source';
        const adapterName = (config.sourceAdapterName || 'CSV').toString();
        const fallbackSource: any = {
          InstanceName: sourceInstanceName,
          AdapterName: adapterName,
          IsEnabled: this.getEffectiveSourceEnabled(config) ?? false,
          AdapterInstanceGuid: config.sourceAdapterInstanceGuid || '',
          Configuration: config.sourceConfiguration || ''
        };

        this.assignWhenPresent(fallbackSource, 'SourceReceiveFolder', config.sourceReceiveFolder);
        this.assignWhenPresent(fallbackSource, 'SourceFileMask', config.sourceFileMask);
        this.assignWhenPresent(fallbackSource, 'SourceBatchSize', config.sourceBatchSize);
        this.assignWhenPresent(fallbackSource, 'SourceFieldSeparator', config.sourceFieldSeparator);
        this.assignWhenPresent(fallbackSource, 'CsvAdapterType', config.csvAdapterType);
        this.assignWhenPresent(fallbackSource, 'CsvPollingInterval', config.csvPollingInterval);
        this.assignWhenPresent(fallbackSource, 'SftpHost', config.sftpHost);
        this.assignWhenPresent(fallbackSource, 'SftpPort', config.sftpPort);
        this.assignWhenPresent(fallbackSource, 'SftpUsername', config.sftpUsername);
        this.assignWhenPresent(fallbackSource, 'SftpPassword', config.sftpPassword ? '***' : undefined);
        this.assignWhenPresent(fallbackSource, 'SftpSshKey', config.sftpSshKey ? '***' : undefined);
        this.assignWhenPresent(fallbackSource, 'SftpFolder', config.sftpFolder);
        this.assignWhenPresent(fallbackSource, 'SftpFileMask', config.sftpFileMask);
        this.assignWhenPresent(fallbackSource, 'SftpMaxConnectionPoolSize', config.sftpMaxConnectionPoolSize);
        this.assignWhenPresent(fallbackSource, 'SftpFileBufferSize', config.sftpFileBufferSize);

        if (this.shouldIncludeSqlProperties(adapterName)) {
          this.assignWhenPresent(fallbackSource, 'SqlServerName', config.sqlServerName);
          this.assignWhenPresent(fallbackSource, 'SqlDatabaseName', config.sqlDatabaseName);
          this.assignWhenPresent(fallbackSource, 'SqlUserName', config.sqlUserName);
          this.assignWhenPresent(fallbackSource, 'SqlPassword', config.sqlPassword ? '***' : undefined);
          this.assignWhenPresent(fallbackSource, 'SqlIntegratedSecurity', config.sqlIntegratedSecurity);
          this.assignWhenPresent(fallbackSource, 'SqlResourceGroup', config.sqlResourceGroup);
          this.assignWhenPresent(fallbackSource, 'SqlPollingStatement', config.sqlPollingStatement);
          this.assignWhenPresent(fallbackSource, 'SqlPollingInterval', config.sqlPollingInterval);
          this.assignWhenPresent(fallbackSource, 'SqlTableName', config.sqlTableName);
          this.assignWhenPresent(fallbackSource, 'SqlUseTransaction', config.sqlUseTransaction);
          this.assignWhenPresent(fallbackSource, 'SqlBatchSize', config.sqlBatchSize);
          this.assignWhenPresent(fallbackSource, 'SqlCommandTimeout', config.sqlCommandTimeout);
          this.assignWhenPresent(fallbackSource, 'SqlFailOnBadStatement', config.sqlFailOnBadStatement);
        }

        this.assignWhenPresent(fallbackSource, 'CreatedAt', config.createdAt);
        this.assignWhenPresent(fallbackSource, 'UpdatedAt', config.updatedAt);

        structuredConfig.Sources[sourceInstanceName] = fallbackSource;
      }

      // Normalize Destinations from new structure (config.destinations or config.Destinations)
      const destinationsData = config.Destinations || config.destinations || {};
      if (destinationsData && Object.keys(destinationsData).length > 0) {
        // Use the new hierarchical structure - normalize all properties to PascalCase
        Object.keys(destinationsData).forEach(instanceName => {
          const destInstance = destinationsData[instanceName];
          const adapterName = (destInstance.AdapterName || destInstance.adapterName || 'SqlServer').toString();
          const normalizedDestination: any = {
            AdapterInstanceGuid: destInstance.AdapterInstanceGuid || destInstance.adapterInstanceGuid || '',
            InstanceName: destInstance.InstanceName || destInstance.instanceName || instanceName,
            AdapterName: adapterName,
            IsEnabled: destInstance.IsEnabled !== undefined ? destInstance.IsEnabled : (destInstance.isEnabled !== undefined ? destInstance.isEnabled : false),
            Configuration: this.getFirstDefined(destInstance.Configuration, destInstance.configuration, '')
          };

          this.assignWhenPresent(normalizedDestination, 'DestinationReceiveFolder', this.getFirstDefined(destInstance.DestinationReceiveFolder, destInstance.destinationReceiveFolder));
          this.assignWhenPresent(normalizedDestination, 'DestinationFileMask', this.getFirstDefined(destInstance.DestinationFileMask, destInstance.destinationFileMask));

          if (this.shouldIncludeSqlProperties(adapterName)) {
            this.assignWhenPresent(normalizedDestination, 'SqlServerName', this.getFirstDefined(destInstance.SqlServerName, destInstance.sqlServerName));
            this.assignWhenPresent(normalizedDestination, 'SqlDatabaseName', this.getFirstDefined(destInstance.SqlDatabaseName, destInstance.sqlDatabaseName));
            this.assignWhenPresent(normalizedDestination, 'SqlUserName', this.getFirstDefined(destInstance.SqlUserName, destInstance.sqlUserName));
            this.assignWhenPresent(normalizedDestination, 'SqlPassword', (destInstance.SqlPassword || destInstance.sqlPassword) ? '***' : undefined);
            this.assignWhenPresent(normalizedDestination, 'SqlIntegratedSecurity', this.getFirstDefined(destInstance.SqlIntegratedSecurity, destInstance.sqlIntegratedSecurity));
            this.assignWhenPresent(normalizedDestination, 'SqlResourceGroup', this.getFirstDefined(destInstance.SqlResourceGroup, destInstance.sqlResourceGroup));
            this.assignWhenPresent(normalizedDestination, 'SqlTableName', this.getFirstDefined(destInstance.SqlTableName, destInstance.sqlTableName));
            this.assignWhenPresent(normalizedDestination, 'SqlUseTransaction', this.getFirstDefined(destInstance.SqlUseTransaction, destInstance.sqlUseTransaction));
            this.assignWhenPresent(normalizedDestination, 'SqlBatchSize', this.getFirstDefined(destInstance.SqlBatchSize, destInstance.sqlBatchSize));
            this.assignWhenPresent(normalizedDestination, 'SqlCommandTimeout', this.getFirstDefined(destInstance.SqlCommandTimeout, destInstance.sqlCommandTimeout));
            this.assignWhenPresent(normalizedDestination, 'SqlFailOnBadStatement', this.getFirstDefined(destInstance.SqlFailOnBadStatement, destInstance.sqlFailOnBadStatement));
          }

          this.assignWhenPresent(normalizedDestination, 'CreatedAt', this.getFirstDefined(destInstance.CreatedAt, destInstance.createdAt));
          this.assignWhenPresent(normalizedDestination, 'UpdatedAt', this.getFirstDefined(destInstance.UpdatedAt, destInstance.updatedAt));

          structuredConfig.Destinations[instanceName] = normalizedDestination;
        });
      } else if (!structuredConfig.Destinations || Object.keys(structuredConfig.Destinations).length === 0) {
        structuredConfig.Destinations = {};
        
        // Try to use destinationAdapterInstances array first
        const destinationInstances = (this.destinationAdapterInstances && this.destinationAdapterInstances.length > 0) 
          ? this.destinationAdapterInstances 
          : (config.destinationAdapterInstances || []);

        if (destinationInstances.length > 0) {
          destinationInstances.forEach((instance: any) => {
            const instanceName = instance.instanceName || 'Destination';
            const adapterName = (instance.adapterName || 'SqlServer').toString();
            const localDestination: any = {
              AdapterInstanceGuid: instance.adapterInstanceGuid || '',
              InstanceName: instanceName,
              AdapterName: adapterName,
              IsEnabled: instance.isEnabled !== undefined ? instance.isEnabled : false,
              Configuration: instance.configuration || ''
            };

            this.assignWhenPresent(localDestination, 'DestinationReceiveFolder', instance.destinationReceiveFolder);
            this.assignWhenPresent(localDestination, 'DestinationFileMask', instance.destinationFileMask);

            if (this.shouldIncludeSqlProperties(adapterName)) {
              this.assignWhenPresent(localDestination, 'SqlServerName', this.getFirstDefined(instance.sqlServerName, config.sqlServerName));
              this.assignWhenPresent(localDestination, 'SqlDatabaseName', this.getFirstDefined(instance.sqlDatabaseName, config.sqlDatabaseName));
              this.assignWhenPresent(localDestination, 'SqlUserName', this.getFirstDefined(instance.sqlUserName, config.sqlUserName));
              const passwordMask = instance.sqlPassword || config.sqlPassword ? '***' : undefined;
              this.assignWhenPresent(localDestination, 'SqlPassword', passwordMask);
              this.assignWhenPresent(localDestination, 'SqlIntegratedSecurity', this.getFirstDefined(instance.sqlIntegratedSecurity, config.sqlIntegratedSecurity));
              this.assignWhenPresent(localDestination, 'SqlResourceGroup', this.getFirstDefined(instance.sqlResourceGroup, config.sqlResourceGroup));
              this.assignWhenPresent(localDestination, 'SqlTableName', this.getFirstDefined(instance.sqlTableName, config.sqlTableName));
              this.assignWhenPresent(localDestination, 'SqlUseTransaction', this.getFirstDefined(instance.sqlUseTransaction, config.sqlUseTransaction));
              this.assignWhenPresent(localDestination, 'SqlBatchSize', this.getFirstDefined(instance.sqlBatchSize, config.sqlBatchSize));
              this.assignWhenPresent(localDestination, 'SqlCommandTimeout', this.getFirstDefined(instance.sqlCommandTimeout, config.sqlCommandTimeout));
              this.assignWhenPresent(localDestination, 'SqlFailOnBadStatement', this.getFirstDefined(instance.sqlFailOnBadStatement, config.sqlFailOnBadStatement));
            }

            this.assignWhenPresent(localDestination, 'CreatedAt', this.getFirstDefined(instance.createdAt, config.createdAt));
            this.assignWhenPresent(localDestination, 'UpdatedAt', this.getFirstDefined(instance.updatedAt, config.updatedAt));

            structuredConfig.Destinations[instanceName] = localDestination;
          });
        } else if (config.destinationAdapterName || config.destinationInstanceName) {
          // Fallback to old structure
          const destInstanceName = config.destinationInstanceName || config.destinationAdapterName || 'Destination';
          const adapterName = (config.destinationAdapterName || 'SqlServer').toString();
          const fallbackDestination: any = {
            AdapterInstanceGuid: config.destinationAdapterInstanceGuid || '',
            InstanceName: destInstanceName,
            AdapterName: adapterName,
            IsEnabled: config.destinationIsEnabled !== undefined ? config.destinationIsEnabled : false,
            Configuration: config.destinationConfiguration || ''
          };

          this.assignWhenPresent(fallbackDestination, 'DestinationReceiveFolder', config.destinationReceiveFolder);
          this.assignWhenPresent(fallbackDestination, 'DestinationFileMask', config.destinationFileMask);

          if (this.shouldIncludeSqlProperties(adapterName)) {
            this.assignWhenPresent(fallbackDestination, 'SqlServerName', config.sqlServerName);
            this.assignWhenPresent(fallbackDestination, 'SqlDatabaseName', config.sqlDatabaseName);
            this.assignWhenPresent(fallbackDestination, 'SqlUserName', config.sqlUserName);
            this.assignWhenPresent(fallbackDestination, 'SqlPassword', config.sqlPassword ? '***' : undefined);
            this.assignWhenPresent(fallbackDestination, 'SqlIntegratedSecurity', config.sqlIntegratedSecurity);
            this.assignWhenPresent(fallbackDestination, 'SqlResourceGroup', config.sqlResourceGroup);
            this.assignWhenPresent(fallbackDestination, 'SqlTableName', config.sqlTableName);
            this.assignWhenPresent(fallbackDestination, 'SqlUseTransaction', config.sqlUseTransaction);
            this.assignWhenPresent(fallbackDestination, 'SqlBatchSize', config.sqlBatchSize);
            this.assignWhenPresent(fallbackDestination, 'SqlCommandTimeout', config.sqlCommandTimeout);
            this.assignWhenPresent(fallbackDestination, 'SqlFailOnBadStatement', config.sqlFailOnBadStatement);
          }

          this.assignWhenPresent(fallbackDestination, 'CreatedAt', config.createdAt);
          this.assignWhenPresent(fallbackDestination, 'UpdatedAt', config.updatedAt);

          structuredConfig.Destinations[destInstanceName] = fallbackDestination;
        }
      }
      
      // Format JSON with proper indentation (matching backend format)
      const jsonString = JSON.stringify(structuredConfig, null, 2);
      
      const dialogRef = this.dialog.open(InterfaceJsonViewDialogComponent, {
        width: '900px',
        maxWidth: '95vw',
        maxHeight: '90vh',
        data: { 
          interfaceName: this.currentInterfaceName || structuredConfig.InterfaceName || 'Unknown Interface', 
          jsonString: jsonString 
        }
      });

      dialogRef.afterClosed().subscribe(result => {
        console.log('JSON dialog closed');
      });
    } catch (error) {
      console.error('Error opening JSON dialog:', error);
      this.snackBar.open('Error opening JSON dialog: ' + (error as Error).message, 'OK', { 
        duration: 5000,
        panelClass: ['error-snackbar']
      });
    }
  }

  loadDestinationAdapterInstances(): void {
    // Don't load if feature is disabled
    if (!this.isDestinationAdapterUIEnabled) {
      this.destinationAdapterInstances = [];
      return;
    }
    
    const interfaceName = this.getActiveInterfaceName();
    const activeConfig = this.getInterfaceConfig(interfaceName);
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
                interfaceName,
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
        // If no instances exist, create a default SqlServerAdapter instance pointing to TransportData table
        if (this.destinationAdapterInstances.length === 0) {
          const configForDefaults = activeConfig;
          
          // Create default SqlServerAdapter destination instance pointing to TransportData table in app database
          const adapterInstanceGuid = this.generateGuid();
          const defaultSqlServerInstance: DestinationAdapterInstance = {
            adapterInstanceGuid: adapterInstanceGuid,
            instanceName: 'Destination 1',
            adapterName: 'SqlServer',
            isEnabled: false,
            configuration: {
              destination: 'TransportData',
              tableName: 'TransportData',
              // Include SQL connection properties from interface configuration if available
              sqlServerName: configForDefaults?.sqlServerName || '',
              sqlDatabaseName: configForDefaults?.sqlDatabaseName || '',
              sqlUserName: configForDefaults?.sqlUserName || '',
              sqlPassword: configForDefaults?.sqlPassword || '',
              sqlIntegratedSecurity: configForDefaults?.sqlIntegratedSecurity ?? false,
              sqlResourceGroup: configForDefaults?.sqlResourceGroup || ''
            }
          };
          
          // Add locally first so it appears immediately
          this.destinationAdapterInstances = [defaultSqlServerInstance];
          this.destinationCardExpandedStates.set(adapterInstanceGuid, true);
          
          // Then try to add via API
          this.transportService.addDestinationAdapterInstance(
            interfaceName,
            defaultSqlServerInstance.adapterName,
            defaultSqlServerInstance.instanceName,
            JSON.stringify(defaultSqlServerInstance.configuration)
          ).subscribe({
            next: (createdInstance) => {
              // Replace local instance with server instance
              this.destinationAdapterInstances = [createdInstance];
              // Update interface configuration with SQL Server connection properties if available
              if (configForDefaults) {
                if (configForDefaults.sqlServerName && configForDefaults.sqlDatabaseName) {
                  // SQL connection properties are already set in the interface configuration
                  // The SqlServerAdapter will use these from the interface configuration
                }
              }
            },
            error: (error) => {
              console.error('Error creating default SqlServerAdapter instance:', error);
              // Keep the local instance even if API call fails
              // The instance is already added locally above
            }
          });
        }
      },
      error: (error) => {
        console.error('Error loading destination adapter instances:', error);
        // If API fails, create default instance locally
        const adapterInstanceGuid = this.generateGuid();
        const configForDefaults = activeConfig;
        const defaultSqlServerInstance: DestinationAdapterInstance = {
          adapterInstanceGuid: adapterInstanceGuid,
          instanceName: 'Destination 1',
          adapterName: 'SqlServer',
          isEnabled: false,
          configuration: {
            destination: 'TransportData',
            tableName: 'TransportData',
            // SQL connection properties will be loaded from interface configuration when available
            sqlServerName: configForDefaults?.sqlServerName || '',
            sqlDatabaseName: configForDefaults?.sqlDatabaseName || '',
            sqlUserName: configForDefaults?.sqlUserName || '',
            sqlPassword: configForDefaults?.sqlPassword || '',
            sqlIntegratedSecurity: configForDefaults?.sqlIntegratedSecurity ?? false,
            sqlResourceGroup: configForDefaults?.sqlResourceGroup || ''
          }
        };
        this.destinationAdapterInstances = [defaultSqlServerInstance];
        this.destinationCardExpandedStates.set(adapterInstanceGuid, true);
      }
    });
  }

  openSourceAdapterSelectionDialog(): void {
    if (!this.currentInterfaceName) {
      this.snackBar.open('Please select an interface first', 'OK', { duration: 3000 });
      return;
    }

    const dialogRef = this.dialog.open(AdapterSelectDialogComponent, {
      width: '800px',
      maxWidth: '90vw',
      data: {
        adapterType: 'Source' as const,
        currentAdapterName: this.sourceAdapterName
      } as AdapterSelectData
    });

    dialogRef.afterClosed().subscribe((selectedAdapterName: string | null) => {
      if (selectedAdapterName && selectedAdapterName !== this.sourceAdapterName) {
        this.changeSourceAdapterType(selectedAdapterName as any);
      }
    });
  }

  changeSourceAdapterType(newAdapterName: 'CSV' | 'FILE' | 'SFTP' | 'SqlServer' | 'SAP' | 'Dynamics365' | 'CRM'): void {
    if (!this.currentInterfaceName) {
      this.snackBar.open('Please select an interface first', 'OK', { duration: 3000 });
      return;
    }

    // TODO: Implement backend API call to change source adapter type
    // For now, update local state
    const oldAdapterName = this.sourceAdapterName;
    this.sourceAdapterName = newAdapterName;
    
    // Reset adapter-specific properties
    if (newAdapterName !== 'CSV') {
      this.csvDataText = '';
    }
    
    this.snackBar.open(`Source adapter changed from ${oldAdapterName} to ${newAdapterName}`, 'OK', { duration: 3000 });
    
    // Reload interface configuration to get new adapter settings
    this.loadInterfaceConfigurations();
  }

  openDestinationAdapterSelectionDialog(): void {
    if (!this.currentInterfaceName) {
      this.snackBar.open('Please select an interface first', 'OK', { duration: 3000 });
      return;
    }

    const dialogRef = this.dialog.open(AdapterSelectDialogComponent, {
      width: '800px',
      maxWidth: '90vw',
      data: {
        adapterType: 'Destination' as const
      } as AdapterSelectData
    });

    dialogRef.afterClosed().subscribe((selectedAdapterName: string | null) => {
      if (selectedAdapterName) {
        this.addDestinationAdapter(selectedAdapterName as any);
      }
    });
  }

  addDestinationAdapter(adapterName: 'CSV' | 'FILE' | 'SFTP' | 'SqlServer' | 'SAP' | 'Dynamics365' | 'CRM'): void {
    // Check if feature is enabled
    if (!this.ensureDestinationAdapterUIFeatureEnabled()) {
      return;
    }
    
    const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
    
    // Set default configuration based on adapter type
    let defaultConfiguration: any = {};
    if (adapterName === 'SqlServer') {
      defaultConfiguration = {
        destination: 'TransportData',
        tableName: 'TransportData',
        // Include SQL connection properties from interface configuration if available
        sqlServerName: defaultConfig?.sqlServerName || '',
        sqlDatabaseName: defaultConfig?.sqlDatabaseName || '',
        sqlUserName: defaultConfig?.sqlUserName || '',
        sqlPassword: defaultConfig?.sqlPassword || '',
        sqlIntegratedSecurity: defaultConfig?.sqlIntegratedSecurity ?? false,
        sqlResourceGroup: defaultConfig?.sqlResourceGroup || ''
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
    
    // Create local instance first (so it appears immediately)
    const localInstance: DestinationAdapterInstance = {
      adapterInstanceGuid: adapterInstanceGuid,
      instanceName: instanceName,
      adapterName: adapterName,
      isEnabled: false,
      configuration: defaultConfiguration
    };
    
    // Add locally first
    this.destinationAdapterInstances = [...this.destinationAdapterInstances, localInstance];
    this.destinationCardExpandedStates.set(adapterInstanceGuid, true);
    
    // Then try to add via API
    this.transportService.addDestinationAdapterInstance(
      this.currentInterfaceName || this.DEFAULT_INTERFACE_NAME,
      adapterName,
      instanceName,
      JSON.stringify(defaultConfiguration)
    ).subscribe({
      next: (createdInstance) => {
        // Replace local instance with server instance
        const index = this.destinationAdapterInstances.findIndex(i => i.adapterInstanceGuid === adapterInstanceGuid);
        if (index >= 0) {
          this.destinationAdapterInstances[index] = createdInstance;
        }
        
        // Show container app creation progress dialog if container app is being created
        if (createdInstance.containerAppStatus && createdInstance.containerAppStatus !== 'NotCreated') {
          const progressData: ContainerAppProgressData = {
            adapterInstanceGuid: createdInstance.adapterInstanceGuid || adapterInstanceGuid,
            adapterName: adapterName,
            adapterType: 'Destination',
            interfaceName: this.currentInterfaceName || this.DEFAULT_INTERFACE_NAME,
            instanceName: instanceName
          };
          
          const progressDialogRef = this.dialog.open(ContainerAppProgressDialogComponent, {
            width: '600px',
            disableClose: true,
            data: progressData
          });
          
          progressDialogRef.afterClosed().subscribe(result => {
            if (result?.success) {
              this.snackBar.open(`Container App für "${instanceName}" erfolgreich erstellt`, 'OK', { duration: 5000 });
              // Verify container app status and settings
              this.verifyContainerAppSettings(createdInstance.adapterInstanceGuid || adapterInstanceGuid, createdInstance);
            } else if (result?.success === false) {
              this.snackBar.open(`Container App Erstellung für "${instanceName}" fehlgeschlagen`, 'OK', { 
                duration: 10000,
                panelClass: ['error-snackbar']
              });
            }
          });
        } else {
          this.snackBar.open(`Destination adapter "${instanceName}" added successfully`, 'OK', { duration: 3000 });
        }
      },
      error: (error) => {
        console.error('Error adding destination adapter:', error);
        // Keep the local instance even if API fails
        this.snackBar.open(`Destination adapter "${instanceName}" added locally (API call failed)`, 'OK', { 
          duration: 5000,
          panelClass: ['warning-snackbar']
        });
      }
    });
  }

  private verifyContainerAppSettings(adapterInstanceGuid: string, instance: any): void {
    // Check container app status
    this.transportService.getContainerAppStatus(adapterInstanceGuid).subscribe({
      next: (status) => {
        if (status.exists && status.status === 'Running') {
          this.snackBar.open(`Container App Status: ${status.status} - Adapter ist einsatzbereit`, 'OK', { duration: 5000 });
          
          // Verify settings were forwarded to container app
          // This would require checking the adapter-config.json in blob storage
          // For now, we'll just log that verification should be done
          console.log('Container App Settings Verification:', {
            adapterInstanceGuid,
            containerAppName: instance.containerAppName,
            status: status.status,
            message: 'Settings verification should check adapter-config.json in blob storage'
          });
        } else {
          this.snackBar.open(`Container App Status: ${status.status || 'Unknown'}`, 'OK', { 
            duration: 5000,
            panelClass: ['warning-snackbar']
          });
        }
      },
      error: (error) => {
        console.error('Error verifying container app status:', error);
      }
    });
  }

  removeDestinationAdapter(adapterInstanceGuid: string, instanceName: string): void {
    // Check if feature is enabled
    if (!this.ensureDestinationAdapterUIFeatureEnabled()) {
      return;
    }
    
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
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
      }
    });
  }

  onDestinationInstanceEnabledChange(instanceGuid: string, enabled: boolean): void {
    const instance = this.destinationAdapterInstances.find(i => i.adapterInstanceGuid === instanceGuid);
    if (!instance) {
      console.error(`Destination adapter instance ${instanceGuid} not found`);
      return;
    }
    
    // Update enabled property immediately in local array for UI responsiveness
    instance.isEnabled = enabled;
    
    // Update instance properties via API
    let configuration = instance.configuration || {};
    if (typeof configuration === 'string') {
      try {
        configuration = JSON.parse(configuration);
      } catch (e) {
        configuration = {};
      }
    }
    
    this.transportService.updateDestinationAdapterInstance(
      this.currentInterfaceName || this.DEFAULT_INTERFACE_NAME,
      instanceGuid,
      instance.instanceName,
      enabled,
      JSON.stringify(configuration)
    ).subscribe({
      next: () => {
        this.snackBar.open(`Destination adapter "${instance.instanceName}" ${enabled ? 'enabled' : 'disabled'}`, 'OK', { duration: 3000 });
      },
      error: (error) => {
        console.error('Error updating destination adapter instance enabled state:', error);
        // Revert local change on error
        instance.isEnabled = !enabled;
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren des Destination Adapters');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
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
      this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
      this.loadDestinationAdapterInstances(); // Reload to restore previous state
    });
  }

  openDestinationInstanceSettings(instance: DestinationAdapterInstance): void {
    // Check if feature is enabled
    if (!this.ensureDestinationAdapterUIFeatureEnabled()) {
      return;
    }
    
    const interfaceName = this.getActiveInterfaceName();
    const activeConfig = this.getInterfaceConfig(interfaceName);
    
    // Parse configuration for adapter instance
    let instanceConfig: any = {};
    if (instance.configuration) {
      try {
        instanceConfig = typeof instance.configuration === 'string' 
          ? JSON.parse(instance.configuration) 
          : instance.configuration;
      } catch (e) {
        console.warn('Failed to parse destination adapter configuration:', e);
        instanceConfig = {};
      }
    }
    
    const csvAdapterType = (instanceConfig.csvAdapterType || instanceConfig.adapterType || 'FILE').toString().toUpperCase();
    const destinationReceiveFolder = instanceConfig.destinationReceiveFolder || instanceConfig.destination || activeConfig?.destinationReceiveFolder || '';
    const destinationFileMask = instanceConfig.destinationFileMask || activeConfig?.destinationFileMask || '*.txt';
    const fieldSeparator = (instanceConfig.fieldSeparator || activeConfig?.sourceFieldSeparator || '║').toString();
    
    // For SqlServer adapters, load properties from instance config first, fall back to interface config
    const sqlTableName = instanceConfig.tableName || instanceConfig.destination || activeConfig?.SqlTableName || 'TransportData';
    const sqlServerName = instanceConfig.sqlServerName || activeConfig?.sqlServerName || '';
    const sqlDatabaseName = instanceConfig.sqlDatabaseName || activeConfig?.sqlDatabaseName || '';
    const sqlUserName = instanceConfig.sqlUserName || activeConfig?.sqlUserName || '';
    const sqlPassword = instanceConfig.sqlPassword || activeConfig?.sqlPassword || '';
    const sqlIntegratedSecurity = instanceConfig.sqlIntegratedSecurity !== undefined ? instanceConfig.sqlIntegratedSecurity : (activeConfig?.sqlIntegratedSecurity ?? false);
    const sqlResourceGroup = instanceConfig.sqlResourceGroup || activeConfig?.sqlResourceGroup || '';
    
    const dialogData: AdapterPropertiesData = {
      adapterType: 'Destination',
      adapterName: instance.adapterName,
      instanceName: instance.instanceName,
      isEnabled: instance.isEnabled ?? false,
      adapterInstanceGuid: instance.adapterInstanceGuid,
      receiveFolder: instance.adapterName === 'CSV' ? destinationReceiveFolder : undefined,
      fileMask: instance.adapterName === 'CSV' ? destinationFileMask : undefined,
      fieldSeparator: instance.adapterName === 'CSV' ? fieldSeparator : undefined,
      destinationReceiveFolder: instance.adapterName === 'CSV' ? destinationReceiveFolder : undefined,
      destinationFileMask: instance.adapterName === 'CSV' ? destinationFileMask : undefined,
      sqlServerName: sqlServerName,
      sqlDatabaseName: sqlDatabaseName,
      sqlUserName: sqlUserName,
      sqlPassword: sqlPassword,
      sqlIntegratedSecurity: sqlIntegratedSecurity,
      sqlResourceGroup: sqlResourceGroup,
      csvAdapterType: instance.adapterName === 'CSV' ? csvAdapterType : undefined,
      sftpHost: instanceConfig.sftpHost || '',
      sftpPort: instanceConfig.sftpPort ?? 22,
      sftpUsername: instanceConfig.sftpUsername || '',
      sftpPassword: instanceConfig.sftpPassword || '',
      sftpSshKey: instanceConfig.sftpSshKey || '',
      sftpFolder: instanceConfig.sftpFolder || '',
      sftpFileMask: instanceConfig.sftpFileMask || '*.txt',
      sftpMaxConnectionPoolSize: instanceConfig.sftpMaxConnectionPoolSize ?? 5,
      sftpFileBufferSize: instanceConfig.sftpFileBufferSize ?? 8192,
      tableName: instance.adapterName === 'SqlServer' ? sqlTableName : undefined
    };

    const dialogRef = this.dialog.open(AdapterPropertiesDialogComponent, {
      width: '600px',
      data: dialogData
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        // Update the instance
        this.updateDestinationInstance(instance.adapterInstanceGuid, result);
        if (instance.adapterName === 'SqlServer') {
          this.updateSqlConnectionProperties(
            result.sqlServerName,
            result.sqlDatabaseName,
            result.sqlUserName,
            result.sqlPassword,
            result.sqlIntegratedSecurity,
            result.sqlResourceGroup
          );
        }
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
    if (instance.adapterName === 'CSV') {
      const selectedAdapterType = (properties.csvAdapterType || configuration.csvAdapterType || configuration.adapterType || 'FILE').toString().toUpperCase();
      const normalizedFieldSeparator = ((properties.fieldSeparator ?? configuration.fieldSeparator ?? '║').toString().trim()) || '║';
      
      configuration = {
        ...configuration,
        csvAdapterType: selectedAdapterType,
        adapterType: selectedAdapterType,
        fieldSeparator: normalizedFieldSeparator
      };

      if (selectedAdapterType === 'FILE') {
        const receiveFolder = properties.destinationReceiveFolder ?? configuration.destinationReceiveFolder ?? configuration.destination ?? '';
        const fileMask = properties.destinationFileMask ?? configuration.destinationFileMask ?? '*.txt';

        configuration = {
          ...configuration,
          destination: receiveFolder,
          destinationReceiveFolder: receiveFolder,
          destinationFileMask: fileMask
        };
      } else if (selectedAdapterType === 'SFTP') {
        configuration = {
          ...configuration,
          sftpHost: properties.sftpHost ?? configuration.sftpHost ?? '',
          sftpPort: properties.sftpPort ?? configuration.sftpPort ?? 22,
          sftpUsername: properties.sftpUsername ?? configuration.sftpUsername ?? '',
          sftpPassword: properties.sftpPassword ?? configuration.sftpPassword ?? '',
          sftpSshKey: properties.sftpSshKey ?? configuration.sftpSshKey ?? '',
          sftpFolder: properties.sftpFolder ?? configuration.sftpFolder ?? '',
          sftpFileMask: properties.sftpFileMask ?? configuration.sftpFileMask ?? '*.txt',
          sftpMaxConnectionPoolSize: properties.sftpMaxConnectionPoolSize ?? configuration.sftpMaxConnectionPoolSize ?? 5,
          sftpFileBufferSize: properties.sftpFileBufferSize ?? configuration.sftpFileBufferSize ?? 8192,
          destination: properties.sftpFolder ?? configuration.sftpFolder ?? ''
        };
      }
    }
    
    // Update SqlServer adapter configuration with SQL properties and table name
    if (instance.adapterName === 'SqlServer') {
      configuration = {
        ...configuration,
        // Table name from dialog or default to TransportData
        destination: properties.tableName || configuration.destination || 'TransportData',
        tableName: properties.tableName || configuration.tableName || 'TransportData',
        // SQL connection properties from dialog (store in instance config)
        sqlServerName: properties.sqlServerName !== undefined ? properties.sqlServerName : (configuration.sqlServerName || ''),
        sqlDatabaseName: properties.sqlDatabaseName !== undefined ? properties.sqlDatabaseName : (configuration.sqlDatabaseName || ''),
        sqlUserName: properties.sqlUserName !== undefined ? properties.sqlUserName : (configuration.sqlUserName || ''),
        sqlPassword: properties.sqlPassword !== undefined ? properties.sqlPassword : (configuration.sqlPassword || ''),
        sqlIntegratedSecurity: properties.sqlIntegratedSecurity !== undefined ? properties.sqlIntegratedSecurity : (configuration.sqlIntegratedSecurity !== undefined ? configuration.sqlIntegratedSecurity : false),
        sqlResourceGroup: properties.sqlResourceGroup !== undefined ? properties.sqlResourceGroup : (configuration.sqlResourceGroup || '')
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
      this.currentInterfaceName || this.DEFAULT_INTERFACE_NAME,
      instanceGuid,
      properties.instanceName || instance.instanceName,
      properties.isEnabled !== undefined ? properties.isEnabled : instance.isEnabled,
      JSON.stringify(configuration)
    ).subscribe({
      next: (response: any) => {
        // Reload from API to ensure consistency
        this.loadDestinationAdapterInstances();
        
        // Show container app status feedback
        if (response.containerAppStatus) {
          const status = response.containerAppStatus;
          const containerAppName = response.containerAppName || 'Container App';
          
          if (status === 'Updated') {
            this.snackBar.open(
              `✅ Destination adapter instance updated. Container app "${containerAppName}" configuration synced successfully.`,
              'OK',
              { duration: 5000, panelClass: ['success-snackbar'] }
            );
          } else if (status === 'Created') {
            this.snackBar.open(
              `✅ Destination adapter instance updated. Container app "${containerAppName}" created successfully.`,
              'OK',
              { duration: 5000, panelClass: ['success-snackbar'] }
            );
          } else if (status === 'Error' || status === 'CreateError') {
            const errorMsg = response.containerAppError || 'Unknown error';
            this.snackBar.open(
              `⚠️ Adapter updated but container app sync failed: ${errorMsg}`,
              'OK',
              { duration: 10000, panelClass: ['warning-snackbar'] }
            );
            console.error('Container app sync error:', errorMsg);
          } else if (status === 'Skipped' || status === 'NotCreated') {
            const reason = response.containerAppError || 'Container app not found or not created';
            this.snackBar.open(
              `ℹ️ Adapter updated. Container app: ${reason}`,
              'OK',
              { duration: 5000, panelClass: ['info-snackbar'] }
            );
          } else {
            this.snackBar.open('Destination adapter instance updated', 'OK', { duration: 3000 });
          }
        } else {
          this.snackBar.open('Destination adapter instance updated', 'OK', { duration: 3000 });
        }
      },
      error: (error) => {
        console.error('Error updating destination adapter instance:', error);
        // Revert local changes on error
        this.loadDestinationAdapterInstances();
        const detailedMessage = this.extractDetailedErrorMessage(error, 'Fehler beim Aktualisieren der Destination Adapter Instance');
        this.showErrorMessageWithCopy(detailedMessage, { duration: 10000 });
      }
    });
  }

  getAdapterIcon(adapterName: 'CSV' | 'FILE' | 'SFTP' | 'SqlServer' | 'SAP' | 'Dynamics365' | 'CRM'): string {
    switch (adapterName) {
      case 'CSV':
      case 'FILE':
        return 'description';
      case 'SFTP':
        return 'cloud_upload';
      case 'SqlServer':
        return 'storage';
      case 'SAP':
        return 'business';
      case 'Dynamics365':
        return 'cloud';
      case 'CRM':
        return 'contacts';
      default:
        return 'extension';
    }
  }

  getLevelColor(level: string): string {
    switch (level) {
      case 'error': return 'warn';
      case 'warning': return 'accent';
      default: return 'primary';
    }
  }

  // Use a seldom-used UTF-8 character as field separator: ‖ (Double Vertical Line, U+2016)
  private readonly FIELD_SEPARATOR = '‖';

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

      // Detect separator from first line for display
      const firstLine = lines[0];
      let displaySeparator = this.FIELD_SEPARATOR;
      if (firstLine.includes(".'")) {
        displaySeparator = ".'";
      } else if (firstLine.includes(this.sourceFieldSeparator)) {
        displaySeparator = this.sourceFieldSeparator;
      } else if (firstLine.includes(this.FIELD_SEPARATOR)) {
        displaySeparator = this.FIELD_SEPARATOR;
      } else if (firstLine.includes('|')) {
        displaySeparator = '|';
      } else if (firstLine.includes(';')) {
        displaySeparator = ';';
      }
      
      // Build HTML with colored columns
      const htmlLines = lines.map((line) => {
        const values = this.parseCsvLine(line);
        const cells = values.map((value, colIndex) => {
          const color = this.COLUMN_COLORS[colIndex % this.COLUMN_COLORS.length];
          const escapedValue = this.escapeHtml(value.trim().replace(/^"|"$/g, ''));
          return `<span style="color: ${color};">${escapedValue}</span>`;
        });
        
        // Add separator between cells (use detected separator)
        const separator = `<span style="color: #999;">${this.escapeHtml(displaySeparator)}</span>`;
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

  onCsvDataChange(newCsvData: string): void {
    this.applyCsvDataLocally(newCsvData);
    this.updateCsvDataProperty(newCsvData);
  }

  /**
   * Parse a CSV line handling quoted values and custom field separator
   * Detects separator dynamically from the data (supports ║, .', |, ;, etc.)
   */
  private parseCsvLine(line: string): string[] {
    const values: string[] = [];
    let current = '';
    let inQuotes = false;
    
    // Detect separator from the line - check for common separators
    let separator = this.FIELD_SEPARATOR; // Default
    if (line.includes(".'")) {
      separator = ".'"; // Old format separator
    } else if (line.includes(this.sourceFieldSeparator)) {
      separator = this.sourceFieldSeparator; // Use configured separator
    } else if (line.includes(this.FIELD_SEPARATOR)) {
      separator = this.FIELD_SEPARATOR; // Use default
    } else if (line.includes('|')) {
      separator = '|';
    } else if (line.includes(';')) {
      separator = ';';
    } else if (line.includes(',')) {
      separator = ',';
    }
    
    // Handle multi-character separator
    const isMultiCharSeparator = separator.length > 1;
    
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
      } else if (!inQuotes) {
        if (isMultiCharSeparator) {
          // Check for multi-character separator
          const substr = line.substring(i, i + separator.length);
          if (substr === separator) {
            values.push(current.trim());
            current = '';
            i += separator.length - 1; // Skip separator characters
            continue;
          }
        } else if (char === separator) {
          // End of value (using single-character separator)
          values.push(current.trim());
          current = '';
          continue;
        }
      }
      
      current += char;
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

  // Container App Status Tracking
  private containerAppStatuses: Map<string, 'Creating' | 'Running' | 'Stopped' | 'Error' | 'Unknown' | null> = new Map();

  /**
   * Get container app status for an adapter instance
   */
  getContainerAppStatus(adapterInstanceGuid: string): 'Creating' | 'Running' | 'Stopped' | 'Error' | 'Unknown' | null {
    if (!adapterInstanceGuid) {
      return null;
    }
    return this.containerAppStatuses.get(adapterInstanceGuid) || null;
  }

  /**
   * Check container app status for an adapter instance
   */
  checkContainerAppStatus(adapterInstanceGuid: string): void {
    if (!adapterInstanceGuid) {
      return;
    }

    this.transportService.getContainerAppStatus(adapterInstanceGuid).subscribe({
      next: (status) => {
        if (status.exists) {
          if (status.status === 'Succeeded' || status.status === 'Running') {
            this.containerAppStatuses.set(adapterInstanceGuid, 'Running');
          } else if (status.status === 'Stopped' || status.status === 'Stopped') {
            this.containerAppStatuses.set(adapterInstanceGuid, 'Stopped');
          } else if (status.status === 'Creating' || status.status === 'Provisioning') {
            this.containerAppStatuses.set(adapterInstanceGuid, 'Creating');
          } else if (status.status === 'Failed' || status.errorMessage) {
            this.containerAppStatuses.set(adapterInstanceGuid, 'Error');
          } else {
            this.containerAppStatuses.set(adapterInstanceGuid, 'Unknown');
          }
        } else {
          // Container app doesn't exist yet - mark as Creating if we just created the adapter instance
          // Otherwise, mark as Unknown
          this.containerAppStatuses.set(adapterInstanceGuid, 'Unknown');
        }
      },
      error: (err) => {
        console.error('Error checking container app status:', err);
        this.containerAppStatuses.set(adapterInstanceGuid, 'Error');
      }
    });
  }
}


