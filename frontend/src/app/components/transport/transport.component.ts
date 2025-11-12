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
  sqlData: SqlRecord[] = [];
  processLogs: ProcessLog[] = [];
  logDataSource = new MatTableDataSource<ProcessLog & { component?: string }>([]);
  isLoading = false;
  isTransporting = false;
  private refreshSubscription?: Subscription;
  
  selectedComponent: string = 'all';
  availableComponents: string[] = ['all', 'Azure Function', 'Blob Storage', 'SQL Server', 'Vercel API'];

  csvDisplayedColumns: string[] = ['id', 'name', 'email', 'age', 'city', 'salary'];
  sqlDisplayedColumns: string[] = ['id', 'name', 'email', 'age', 'city', 'salary', 'createdAt'];
  logDisplayedColumns: string[] = ['timestamp', 'level', 'component', 'message', 'details'];

  constructor(
    private transportService: TransportService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.loadSampleCsvData();
    this.loadSqlData();
    this.loadProcessLogs();
    this.startAutoRefresh();
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
      },
      error: (error) => {
        console.error('Error loading SQL data:', error);
        this.snackBar.open('Fehler beim Laden der SQL-Daten', 'Schließen', { duration: 3000 });
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
      },
      error: (error) => {
        console.error('Error loading process logs:', error);
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
    this.transportService.startTransport().subscribe({
      next: (response) => {
        this.snackBar.open('Transport gestartet: ' + response.message, 'Schließen', { duration: 5000 });
        this.isTransporting = false;
        setTimeout(() => {
          this.loadSqlData();
          this.loadProcessLogs();
        }, 2000);
      },
      error: (error) => {
        console.error('Error starting transport:', error);
        this.snackBar.open('Fehler beim Starten des Transports', 'Schließen', { duration: 3000 });
        this.isTransporting = false;
      }
    });
  }

  clearTable(): void {
    if (confirm('Möchten Sie wirklich alle Daten aus der Tabelle löschen?')) {
      this.transportService.clearTable().subscribe({
        next: (response) => {
          this.snackBar.open(response.message, 'Schließen', { duration: 3000 });
          this.loadSqlData();
          this.loadProcessLogs();
        },
        error: (error) => {
          console.error('Error clearing table:', error);
          this.snackBar.open('Fehler beim Löschen der Tabelle', 'Schließen', { duration: 3000 });
        }
      });
    }
  }

  private startAutoRefresh(): void {
    this.refreshSubscription = interval(5000).subscribe(() => {
      this.loadSqlData();
      this.loadProcessLogs();
    });
  }

  getLevelColor(level: string): string {
    switch (level) {
      case 'error': return 'warn';
      case 'warning': return 'accent';
      default: return 'primary';
    }
  }
}


