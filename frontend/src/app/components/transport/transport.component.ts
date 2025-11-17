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
import { FormsModule } from '@angular/forms';
import { TransportService } from '../../services/transport.service';
import { TranslationService } from '../../services/translation.service';
import { CsvRecord, SqlRecord, ProcessLog } from '../../models/data.model';
import { interval, Subscription } from 'rxjs';
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
    MatInputModule
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
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.loadSampleCsvData();
    this.loadSqlData();
    this.loadProcessLogs();
    this.loadInterfaceConfigurations();
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
      },
      error: (error) => {
        console.warn('Could not load interface configurations:', error);
        // Don't show error - this is not critical for basic functionality
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
        this.snackBar.open('Fehler beim Laden der CSV-Daten', 'Schließen', { duration: 3000 });
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
        
        // Extract detailed error message
        let errorMessage = 'Fehler beim Laden der SQL-Daten';
        let errorDetails = '';
        let errorCode = '';
        
        if (error.error) {
          if (error.error.details) {
            errorDetails = error.error.details;
          } else if (error.error.message) {
            errorDetails = error.error.message;
          } else if (error.error.error) {
            errorDetails = error.error.error;
          }
          
          if (error.error.message) {
            errorMessage = error.error.message;
          } else if (error.error.error) {
            errorMessage = error.error.error;
          }
          
          if (error.error.code) {
            errorCode = error.error.code;
          }
        } else if (error.message) {
          errorDetails = error.message;
        }
        
        // Create unique error identifier
        const errorKey = `${errorCode}:${errorDetails}`;
        
        // Only show error popup if:
        // 1. It's a different error than last time, OR
        // 2. It's the first time showing this error, OR
        // 3. It's been shown less than 3 times (to avoid spam)
        if (errorKey !== this.lastErrorShown || this.errorShownCount < 3) {
          const fullMessage = errorDetails 
            ? `${errorMessage}: ${errorDetails}`
            : errorMessage;
          
          this.snackBar.open(fullMessage, 'Schließen', { 
            duration: 8000,
            panelClass: ['error-snackbar']
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
        
        // Extract detailed error message
        let errorMessage = 'Fehler beim Laden der Prozess-Logs';
        let errorDetails = '';
        let errorCode = '';
        
        if (error.error) {
          if (error.error.details) {
            errorDetails = error.error.details;
          } else if (error.error.message) {
            errorDetails = error.error.message;
          } else if (error.error.error) {
            errorDetails = error.error.error;
          }
          
          if (error.error.message) {
            errorMessage = error.error.message;
          } else if (error.error.error) {
            errorMessage = error.error.error;
          }
          
          if (error.error.code) {
            errorCode = error.error.code;
          }
        } else if (error.message) {
          errorDetails = error.message;
        }
        
        // Create unique error identifier
        const errorKey = `process-logs:${errorCode}:${errorDetails}`;
        
        // Only show error popup if it's a different error or hasn't been shown too many times
        if (errorKey !== this.lastErrorShown || this.errorShownCount < 3) {
          const fullMessage = errorDetails 
            ? `${errorMessage}: ${errorDetails}`
            : errorMessage;
          
          this.snackBar.open(fullMessage, 'Schließen', { 
            duration: 8000,
            panelClass: ['error-snackbar']
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
        sourceConfiguration: JSON.stringify({ source: 'csv-files/csv-incoming', enabled: true }),
        destinationAdapterName: 'SqlServer',
        destinationConfiguration: JSON.stringify({ destination: 'TransportData', enabled: true }),
        description: 'Default CSV to SQL Server interface'
      }).subscribe({
        next: () => {
          this.loadInterfaceConfigurations();
          this.uploadAndStartTransport();
        },
        error: (error) => {
          console.warn('Could not create interface configuration, continuing anyway:', error);
          this.uploadAndStartTransport();
        }
      });
    } else {
      // Ensure it's enabled
      if (!defaultConfig.isEnabled) {
        this.transportService.toggleInterfaceConfiguration(this.DEFAULT_INTERFACE_NAME, true).subscribe({
          next: () => {
            this.loadInterfaceConfigurations();
            this.uploadAndStartTransport();
          },
          error: (error) => {
            console.warn('Could not enable interface configuration, continuing anyway:', error);
            this.uploadAndStartTransport();
          }
        });
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
        this.isTransporting = false;
        
        // Extract detailed error message
        let errorMessage = 'Fehler beim Starten des Transports';
        let errorDetails = '';
        
        if (error.error) {
          if (error.error.details) {
            errorDetails = error.error.details;
          } else if (error.error.message) {
            errorDetails = error.error.message;
          } else if (error.error.error) {
            errorDetails = error.error.error;
          }
        } else if (error.message) {
          errorDetails = error.message;
        }
        
        const fullMessage = errorDetails 
          ? `${errorMessage}: ${errorDetails}`
          : errorMessage;
        
        this.snackBar.open(fullMessage, 'Schließen', { 
          duration: 8000,
          panelClass: ['error-snackbar']
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
          const errorMessage = error.error?.message || error.error?.error || 'Fehler beim Löschen der Tabelle';
          this.snackBar.open(errorMessage, 'Schließen', { duration: 5000 });
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
          const errorMessage = error.error?.message || error.error?.error || 'Fehler beim Leeren der Protokolltabelle';
          this.snackBar.open(errorMessage, 'Schließen', { duration: 3000 });
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
        this.isDiagnosing = false;
        this.snackBar.open('Fehler bei der Diagnose', 'Schließen', { duration: 3000 });
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
  
  getDefaultInterfaceStatus(): { exists: boolean; enabled: boolean } {
    const defaultConfig = this.interfaceConfigurations.find(c => c.interfaceName === this.DEFAULT_INTERFACE_NAME);
    return {
      exists: !!defaultConfig,
      enabled: defaultConfig?.isEnabled ?? false
    };
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


