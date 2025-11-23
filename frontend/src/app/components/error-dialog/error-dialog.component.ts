import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatSnackBarModule, MatSnackBar } from '@angular/material/snack-bar';
import { ErrorTrackingService, ErrorReport } from '../../services/error-tracking.service';

export interface ErrorDialogData {
  error: Error | any;
  functionName?: string;
  component?: string;
  context?: any;
}

@Component({
  selector: 'app-error-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatExpansionModule,
    MatSnackBarModule
  ],
  template: `
    <h2 mat-dialog-title>
      <mat-icon color="warn" style="vertical-align: middle; margin-right: 8px;">error</mat-icon>
      Ein Fehler ist aufgetreten
    </h2>
    
    <mat-dialog-content>
      <mat-card>
        <mat-card-content>
          <p><strong>Fehler:</strong> {{ errorMessage }}</p>
          <p *ngIf="errorName"><strong>Typ:</strong> {{ errorName }}</p>
          <p *ngIf="functionName"><strong>Funktion:</strong> {{ functionName }}</p>
          <p *ngIf="component"><strong>Komponente:</strong> {{ component }}</p>
        </mat-card-content>
      </mat-card>

      <mat-expansion-panel *ngIf="errorStack" style="margin-top: 16px;">
        <mat-expansion-panel-header>
          <mat-panel-title>
            <mat-icon>code</mat-icon>
            Stack Trace anzeigen
          </mat-panel-title>
        </mat-expansion-panel-header>
        <pre style="white-space: pre-wrap; font-size: 11px; overflow-x: auto;">{{ errorStack }}</pre>
      </mat-expansion-panel>

      <mat-expansion-panel *ngIf="errorReport" style="margin-top: 16px;">
        <mat-expansion-panel-header>
          <mat-panel-title>
            <mat-icon>history</mat-icon>
            Funktionsaufruf-Historie ({{ errorReport.functionCallHistory.length }} Einträge)
          </mat-panel-title>
        </mat-expansion-panel-header>
        <div style="max-height: 300px; overflow-y: auto;">
          <div *ngFor="let call of errorReport.functionCallHistory" 
               style="padding: 8px; border-bottom: 1px solid #e0e0e0; font-size: 11px;">
            <div style="display: flex; justify-content: space-between;">
              <span><strong>{{ call.functionName }}</strong></span>
              <span [style.color]="call.success ? 'green' : 'red'">
                {{ call.success ? '✓' : '✗' }}
              </span>
            </div>
            <div *ngIf="call.component" style="color: #666; font-size: 10px;">
              {{ call.component }}
            </div>
            <div style="color: #999; font-size: 9px;">
              {{ formatTimestamp(call.timestamp) }}
            </div>
            <div *ngIf="call.error" style="color: red; margin-top: 4px;">
              {{ call.error.message }}
            </div>
          </div>
        </div>
      </mat-expansion-panel>

      <mat-expansion-panel *ngIf="errorReport" style="margin-top: 16px;">
        <mat-expansion-panel-header>
          <mat-panel-title>
            <mat-icon>info</mat-icon>
            Anwendungszustand
          </mat-panel-title>
        </mat-expansion-panel-header>
        <pre style="white-space: pre-wrap; font-size: 11px; overflow-x: auto;">{{ formatApplicationState(errorReport.applicationState) }}</pre>
      </mat-expansion-panel>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button (click)="copyErrorDetails()">
        <mat-icon>content_copy</mat-icon>
        Fehlerdetails kopieren
      </button>
      <button mat-button (click)="downloadErrorReport()">
        <mat-icon>download</mat-icon>
        Fehlerbericht herunterladen
      </button>
      <button mat-button color="primary" (click)="submitToAI()" [disabled]="isSubmitting">
        <mat-icon *ngIf="!isSubmitting">smart_toy</mat-icon>
        <mat-spinner *ngIf="isSubmitting" diameter="20" style="display: inline-block;"></mat-spinner>
        Fehler an AI zur Korrektur übergeben
      </button>
      <button mat-button (click)="close()">Schließen</button>
    </mat-dialog-actions>
  `,
  styles: [`
    mat-dialog-content {
      max-height: 70vh;
      overflow-y: auto;
    }
    pre {
      background-color: #f5f5f5;
      padding: 12px;
      border-radius: 4px;
      border: 1px solid #e0e0e0;
    }
  `]
})
export class ErrorDialogComponent {
  errorMessage: string = '';
  errorName: string = '';
  errorStack: string = '';
  functionName: string = '';
  component: string = '';
  errorReport: ErrorReport | null = null;
  isSubmitting: boolean = false;

  constructor(
    public dialogRef: MatDialogRef<ErrorDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: ErrorDialogData,
    private errorTrackingService: ErrorTrackingService,
    private snackBar: MatSnackBar
  ) {
    this.initializeErrorData();
  }

  private initializeErrorData(): void {
    const error = this.data.error;
    
    if (error instanceof Error) {
      this.errorMessage = error.message;
      this.errorName = error.name;
      this.errorStack = error.stack || '';
    } else {
      this.errorMessage = String(error);
      this.errorName = 'Unknown';
    }

    this.functionName = this.data.functionName || 'Unknown';
    this.component = this.data.component || 'Unknown';

    // Get error report from tracking service
    this.errorReport = this.errorTrackingService.getCurrentErrorReport();
    
    // If no report exists, create one
    if (!this.errorReport) {
      this.errorReport = this.errorTrackingService.trackError(
        this.functionName,
        error,
        this.component,
        this.data.context
      );
    }
  }

  formatTimestamp(timestamp: number): string {
    return new Date(timestamp).toLocaleString('de-DE');
  }

  formatApplicationState(state: any): string {
    return JSON.stringify(state, null, 2);
  }

  copyErrorDetails(): void {
    const details = {
      error: {
        message: this.errorMessage,
        name: this.errorName,
        stack: this.errorStack
      },
      function: this.functionName,
      component: this.component,
      report: this.errorReport
    };

    navigator.clipboard.writeText(JSON.stringify(details, null, 2)).then(() => {
      this.snackBar.open('Fehlerdetails in Zwischenablage kopiert', 'OK', {
        duration: 2000
      });
    });
  }

  downloadErrorReport(): void {
    if (!this.errorReport) return;

    const blob = new Blob([JSON.stringify(this.errorReport, null, 2)], {
      type: 'application/json'
    });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `error-report-${this.errorReport.errorId}.json`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    window.URL.revokeObjectURL(url);

    this.snackBar.open('Fehlerbericht heruntergeladen', 'OK', {
      duration: 2000
    });
  }

  submitToAI(): void {
    if (!this.errorReport) {
      this.snackBar.open('Kein Fehlerbericht verfügbar', 'OK', {
        duration: 3000,
        panelClass: ['error-snackbar']
      });
      return;
    }

    this.isSubmitting = true;

    this.errorTrackingService.submitErrorToAI(this.errorReport).subscribe({
      next: (response) => {
        this.isSubmitting = false;
        this.snackBar.open(
          'Fehler wurde an AI übergeben. Die Korrektur wird automatisch durchgeführt.',
          'OK',
          {
            duration: 5000,
            panelClass: ['success-snackbar']
          }
        );
        this.close();
      },
      error: (error) => {
        this.isSubmitting = false;
        console.error('Failed to submit error to AI:', error);
        this.snackBar.open(
          'Fehler beim Übergeben an AI. Bitte versuchen Sie es später erneut.',
          'OK',
          {
            duration: 5000,
            panelClass: ['error-snackbar']
          }
        );
      }
    });
  }

  close(): void {
    this.dialogRef.close();
  }
}



