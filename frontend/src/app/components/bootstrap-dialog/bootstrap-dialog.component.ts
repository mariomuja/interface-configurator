import { Component, Inject, OnDestroy, OnInit } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { CommonModule } from '@angular/common';
import { BootstrapService, BootstrapResult } from '../../services/bootstrap.service';
import { TransportService } from '../../services/transport.service';
import { ProcessLog } from '../../models/data.model';
import { Subscription, timer, of } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';

export interface BootstrapDialogData {
  autoRun?: boolean; // If true, automatically run bootstrap on dialog open
  onComplete?: (result: BootstrapResult) => void; // Callback when bootstrap completes
}

@Component({
  selector: 'app-bootstrap-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatChipsModule
  ],
  template: `
    <h2 mat-dialog-title>
      <mat-icon>system_update</mat-icon>
      System-Bootstrap
    </h2>

    <mat-dialog-content>
      <div *ngIf="isLoading" class="loading-container">
        <mat-spinner diameter="50"></mat-spinner>
        <p>Prüfe Systemkomponenten...</p>
        <div class="log-preview" *ngIf="progressLogs.length">
          <div class="log-preview-title">Live-Details</div>
          <div class="log-line" *ngFor="let log of progressLogs">{{ formatLogLine(log) }}</div>
        </div>
      </div>

      <div *ngIf="!isLoading && bootstrapResult" class="bootstrap-results">
        <div class="overall-status" [class.healthy]="bootstrapResult.overallStatus === 'Healthy'"
             [class.degraded]="bootstrapResult.overallStatus === 'Degraded'"
             [class.unhealthy]="bootstrapResult.overallStatus === 'Unhealthy'">
          <mat-icon>{{ getStatusIcon(bootstrapResult.overallStatus) }}</mat-icon>
          <div class="status-info">
            <h3>Gesamtstatus: {{ getStatusText(bootstrapResult.overallStatus) }}</h3>
            <p>{{ bootstrapResult.healthyChecks }} von {{ bootstrapResult.totalChecks }} Prüfungen erfolgreich</p>
          </div>
        </div>

        <div class="checks-list">
          <div *ngFor="let check of bootstrapResult.checks" class="check-item"
               [class.healthy]="check.status === 'Healthy'"
               [class.degraded]="check.status === 'Degraded'"
               [class.unhealthy]="check.status === 'Unhealthy'">
            <div class="check-header">
              <mat-icon>{{ getStatusIcon(check.status) }}</mat-icon>
              <div class="check-info">
                <strong>{{ check.name }}</strong>
                <span class="check-status">{{ getStatusText(check.status) }}</span>
              </div>
            </div>
            <p class="check-message">{{ check.message }}</p>
            <details *ngIf="check.details" class="check-details">
              <summary>Details anzeigen</summary>
              <pre>{{ check.details }}</pre>
            </details>
          </div>
        </div>

        <div class="log-preview log-preview-results" *ngIf="progressLogs.length">
          <div class="log-preview-title">Bootstrap-Protokoll</div>
          <div class="log-line" *ngFor="let log of progressLogs">{{ formatLogLine(log) }}</div>
        </div>
      </div>

      <div *ngIf="error" class="error-container">
        <mat-icon color="warn">error</mat-icon>
        <p>{{ error }}</p>
      </div>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button (click)="runBootstrap()" [disabled]="isLoading" color="primary">
        <mat-icon>refresh</mat-icon>
        Erneut prüfen
      </button>
      <button mat-button (click)="onClose()" color="primary">Schließen</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .loading-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 40px;
      gap: 20px;
    }

    .bootstrap-results {
      min-width: 600px;
      max-width: 800px;
    }

    .overall-status {
      display: flex;
      align-items: center;
      gap: 16px;
      padding: 20px;
      border-radius: 8px;
      margin-bottom: 24px;
    }

    .overall-status.healthy {
      background-color: #e8f5e9;
      border: 2px solid #4caf50;
    }

    .overall-status.degraded {
      background-color: #fff3e0;
      border: 2px solid #ff9800;
    }

    .overall-status.unhealthy {
      background-color: #ffebee;
      border: 2px solid #f44336;
    }

    .overall-status mat-icon {
      font-size: 48px;
      width: 48px;
      height: 48px;
    }

    .status-info h3 {
      margin: 0 0 8px 0;
      font-size: 18px;
    }

    .status-info p {
      margin: 0;
      color: #666;
    }

    .checks-list {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    .check-item {
      padding: 16px;
      border-radius: 8px;
      border: 1px solid #ddd;
    }

    .check-item.healthy {
      background-color: #f1f8f4;
      border-color: #4caf50;
    }

    .check-item.degraded {
      background-color: #fff8f0;
      border-color: #ff9800;
    }

    .check-item.unhealthy {
      background-color: #fff5f5;
      border-color: #f44336;
    }

    .check-header {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-bottom: 8px;
    }

    .check-header mat-icon {
      font-size: 24px;
      width: 24px;
      height: 24px;
    }

    .check-info {
      flex: 1;
      display: flex;
      justify-content: space-between;
      align-items: center;
    }

    .check-status {
      font-size: 12px;
      padding: 4px 8px;
      border-radius: 4px;
      background-color: #f5f5f5;
    }

    .check-message {
      margin: 8px 0;
      color: #666;
    }

    .check-details {
      margin-top: 8px;
    }

    .check-details summary {
      cursor: pointer;
      color: #1976d2;
      font-weight: 500;
      margin-bottom: 8px;
    }

    .check-details pre {
      background-color: #f5f5f5;
      padding: 12px;
      border-radius: 4px;
      overflow-x: auto;
      font-size: 12px;
      max-height: 200px;
      overflow-y: auto;
    }

    .error-container {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 20px;
      background-color: #ffebee;
      border-radius: 8px;
      color: #c62828;
    }

    .log-preview {
      width: 100%;
      max-height: 220px;
      overflow-y: auto;
      background-color: #101010;
      color: #d0ffd0;
      font-family: 'Courier New', monospace;
      padding: 16px;
      border-radius: 8px;
      border: 1px solid #1b5e20;
      margin-top: 16px;
    }

    .log-preview-title {
      font-weight: 600;
      margin-bottom: 12px;
      color: #9ccc65;
      letter-spacing: 0.08em;
      text-transform: uppercase;
    }

    .log-line {
      margin: 0 0 6px 0;
      white-space: pre-wrap;
      font-size: 12px;
      line-height: 1.4;
    }

    .log-preview-results {
      margin-top: 24px;
    }

    mat-dialog-actions {
      padding: 16px 24px;
    }
  `]
})
export class BootstrapDialogComponent implements OnInit, OnDestroy {
  isLoading = false;
  bootstrapResult: BootstrapResult | null = null;
  error: string | null = null;
  progressLogs: ProcessLog[] = [];

  private logPollingSub?: Subscription;
  private readonly maxLogEntries = 50;

  constructor(
    public dialogRef: MatDialogRef<BootstrapDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: BootstrapDialogData,
    private bootstrapService: BootstrapService,
    private transportService: TransportService
  ) {}

  ngOnInit(): void {
    if (this.data?.autoRun !== false) {
      // Auto-run bootstrap by default
      this.runBootstrap();
    }
  }

  ngOnDestroy(): void {
    this.stopLogPolling();
  }

  runBootstrap(): void {
    this.isLoading = true;
    this.error = null;
    this.bootstrapResult = null;
    this.progressLogs = [];
    this.startLogPolling();

    this.bootstrapService.runBootstrapAndRefreshLogs().subscribe({
      next: (result) => {
        this.bootstrapResult = result;
        this.isLoading = false;
        this.stopLogPolling();
        this.fetchLatestLogsOnce();

        // Refresh ProcessLogs after a short delay to show bootstrap logs
        setTimeout(() => {
          // Trigger ProcessLogs refresh - this will be handled by the component that opened the dialog
          if (this.data?.onComplete) {
            this.data.onComplete(result);
          }
          // Also trigger a custom event that TransportComponent can listen to
          window.dispatchEvent(new CustomEvent('bootstrap-complete'));
        }, 1500);
      },
      error: (err) => {
        this.error = `Bootstrap-Fehler: ${err.message || 'Unbekannter Fehler'}`;
        this.isLoading = false;
        console.error('Bootstrap error:', err);
        this.stopLogPolling();
      }
    });
  }

  getStatusIcon(status: string): string {
    switch (status) {
      case 'Healthy':
        return 'check_circle';
      case 'Degraded':
        return 'warning';
      case 'Unhealthy':
        return 'error';
      default:
        return 'help';
    }
  }

  getStatusText(status: string): string {
    switch (status) {
      case 'Healthy':
        return 'Gesund';
      case 'Degraded':
        return 'Beeinträchtigt';
      case 'Unhealthy':
        return 'Fehlerhaft';
      default:
        return status;
    }
  }

  formatLogLine(log: ProcessLog): string {
    const timestamp = this.formatTimestamp(log);
    const level = (log.level || '').toUpperCase().padEnd(7);
    const message = log.message || '';
    const details = log.details ? `\n    ${log.details}` : '';
    return `[${timestamp}] ${level} ${message}${details}`;
  }

  onClose(): void {
    this.dialogRef.close(this.bootstrapResult);
  }

  private startLogPolling(): void {
    this.stopLogPolling();
    this.logPollingSub = timer(0, 2000)
      .pipe(
        switchMap(() => this.transportService.getProcessLogs()),
        catchError(err => {
          console.error('Fehler beim Laden der Bootstrap-Logs:', err);
          return of([]);
        })
      )
      .subscribe(logs => this.updateProgressLogs(logs));
  }

  private stopLogPolling(): void {
    this.logPollingSub?.unsubscribe();
    this.logPollingSub = undefined;
  }

  private fetchLatestLogsOnce(): void {
    this.transportService.getProcessLogs()
      .pipe(catchError(() => of([])))
      .subscribe(logs => this.updateProgressLogs(logs));
  }

  private updateProgressLogs(logs: ProcessLog[]): void {
    const bootstrapLogs = logs
      .filter(log => (log.component || '').toLowerCase() === 'bootstrap')
      .sort((a, b) => this.toDate(a) - this.toDate(b));
    this.progressLogs = bootstrapLogs.slice(-this.maxLogEntries);
  }

  private formatTimestamp(log: ProcessLog): string {
    const raw = log.timestamp || log.datetime_created;
    if (!raw) {
      return '--:--:--';
    }
    const date = new Date(raw);
    if (isNaN(date.getTime())) {
      return raw;
    }
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
  }

  private toDate(log: ProcessLog): number {
    const raw = log.timestamp || log.datetime_created;
    if (!raw) {
      return 0;
    }
    const value = new Date(raw).getTime();
    return isNaN(value) ? 0 : value;
  }
}

