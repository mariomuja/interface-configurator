import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';

export interface DiagnoseDialogData {
  result?: any;
}

@Component({
  selector: 'app-diagnose-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatCardModule,
    MatChipsModule
  ],
  template: `
    <h2 mat-dialog-title>
      <mat-icon>bug_report</mat-icon>
      System-Diagnose
    </h2>
    
    <mat-dialog-content>
      <div *ngIf="!data.result && !isLoading" class="diagnose-prompt">
        <p>Klicken Sie auf "Diagnose starten", um eine vollständige Systemprüfung durchzuführen.</p>
      </div>
      
      <div *ngIf="isLoading" class="loading-container">
        <mat-spinner diameter="50"></mat-spinner>
        <p>Diagnose wird durchgeführt...</p>
      </div>
      
      <div *ngIf="data.result && !isLoading" class="diagnostics-result">
        <div class="diagnostics-summary">
          <mat-chip-set>
            <mat-chip [class]="'status-' + (data.result.summary?.overall?.toLowerCase() || 'ok')">
              <mat-icon>{{ getStatusIcon(data.result.summary?.overall) }}</mat-icon>
              Status: {{ data.result.summary?.overall || 'OK' }}
            </mat-chip>
            <mat-chip>
              Erfolgreich: {{ data.result.summary?.passed || 0 }}/{{ data.result.summary?.totalChecks || 0 }}
            </mat-chip>
          </mat-chip-set>
        </div>
        <div class="diagnostics-checks">
          <div *ngFor="let check of data.result.checks" class="diagnostic-check" 
               [class.ok]="check.status === 'OK'" 
               [class.failed]="check.status === 'FAILED'" 
               [class.error]="check.status === 'ERROR'"
               [class.warning]="check.status === 'WARNING'">
            <mat-icon>{{ getCheckIcon(check.status) }}</mat-icon>
            <div class="check-details">
              <strong>{{ check.name }}</strong>
              <span class="check-status">{{ check.status }}</span>
              <div class="check-details-text">{{ check.details }}</div>
            </div>
          </div>
        </div>
      </div>
    </mat-dialog-content>
    
    <mat-dialog-actions align="end">
      <button mat-button (click)="onClose()" *ngIf="data.result">Schließen</button>
      <button mat-raised-button color="primary" (click)="onStartDiagnose()" [disabled]="isLoading" *ngIf="!data.result">
        <mat-icon>play_arrow</mat-icon>
        Diagnose starten
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    h2[mat-dialog-title] {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    
    mat-dialog-content {
      min-width: 600px;
      max-width: 800px;
      max-height: 70vh;
      overflow-y: auto;
    }
    
    .diagnose-prompt {
      padding: 20px;
      text-align: center;
    }
    
    .loading-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 16px;
      padding: 40px;
    }
    
    .diagnostics-summary {
      margin-bottom: 20px;
      padding: 16px;
      background-color: #f5f5f5;
      border-radius: 4px;
    }
    
    .diagnostics-checks {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    
    .diagnostic-check {
      display: flex;
      align-items: flex-start;
      gap: 12px;
      padding: 12px;
      border-radius: 4px;
      border-left: 4px solid;
    }
    
    .diagnostic-check.ok {
      background-color: #e8f5e9;
      border-left-color: #4caf50;
    }
    
    .diagnostic-check.failed {
      background-color: #ffebee;
      border-left-color: #f44336;
    }
    
    .diagnostic-check.error {
      background-color: #fff3e0;
      border-left-color: #ff9800;
    }
    
    .diagnostic-check.warning {
      background-color: #fff9c4;
      border-left-color: #ffc107;
    }
    
    .diagnostic-check mat-icon {
      flex-shrink: 0;
    }
    
    .check-details {
      flex: 1;
    }
    
    .check-details strong {
      display: block;
      margin-bottom: 4px;
    }
    
    .check-status {
      display: inline-block;
      margin-left: 8px;
      padding: 2px 8px;
      border-radius: 12px;
      font-size: 11px;
      font-weight: 500;
    }
    
    .check-details-text {
      margin-top: 4px;
      font-size: 12px;
      color: rgba(0, 0, 0, 0.7);
      white-space: pre-wrap;
    }
    
    mat-chip {
      margin-right: 8px;
    }
    
    .status-ok {
      background-color: #4caf50 !important;
      color: white !important;
    }
    
    .status-failed {
      background-color: #f44336 !important;
      color: white !important;
    }
    
    .status-error {
      background-color: #ff9800 !important;
      color: white !important;
    }
  `]
})
export class DiagnoseDialogComponent implements OnInit {
  isLoading = true;
  
  constructor(
    public dialogRef: MatDialogRef<DiagnoseDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: DiagnoseDialogData
  ) {
    // If result is already provided, don't show loading
    if (this.data.result) {
      this.isLoading = false;
    }
  }
  
  ngOnInit(): void {
    // If result is already provided, show it
    // Otherwise, loading is already set to true
  }
  
  onStartDiagnose(): void {
    // This will be handled by parent component
    this.isLoading = true;
  }
  
  onClose(): void {
    this.dialogRef.close();
  }
  
  getStatusIcon(status: string): string {
    switch (status?.toUpperCase()) {
      case 'OK': return 'check_circle';
      case 'FAILED': return 'error';
      case 'ERROR': return 'warning';
      default: return 'info';
    }
  }
  
  getCheckIcon(status: string): string {
    switch (status?.toUpperCase()) {
      case 'OK': return 'check_circle';
      case 'FAILED': return 'error';
      case 'ERROR': return 'warning';
      case 'WARNING': return 'warning';
      default: return 'info';
    }
  }
}

