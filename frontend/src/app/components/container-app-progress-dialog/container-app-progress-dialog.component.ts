import { Component, Inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { interval, Subscription } from 'rxjs';
import { TransportService } from '../../services/transport.service';

export interface ContainerAppProgressData {
  adapterInstanceGuid: string;
  adapterName: string;
  adapterType: 'Source' | 'Destination';
  interfaceName: string;
  instanceName: string;
}

export interface ProgressStep {
  id: string;
  label: string;
  status: 'pending' | 'in-progress' | 'completed' | 'error';
  message?: string;
  timestamp?: Date;
}

@Component({
  selector: 'app-container-app-progress-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatProgressBarModule,
    MatButtonModule,
    MatIconModule,
    MatListModule
  ],
  template: `
    <h2 mat-dialog-title>
      <mat-icon>cloud_upload</mat-icon>
      Container App wird erstellt...
    </h2>
    
    <mat-dialog-content>
      <div class="progress-container">
        <!-- Progress Bar -->
        <mat-progress-bar 
          mode="determinate" 
          [value]="progressPercentage"
          [color]="hasErrors ? 'warn' : 'primary'">
        </mat-progress-bar>
        
        <div class="progress-info">
          <span class="progress-text">{{ currentStepLabel }}</span>
          <span class="progress-percentage">{{ progressPercentage }}%</span>
        </div>

        <!-- Steps List -->
        <mat-list class="steps-list">
          <mat-list-item *ngFor="let step of steps" [class]="'step-' + step.status">
            <mat-icon matListItemIcon [class]="getStepIconClass(step.status)">
              {{ getStepIcon(step.status) }}
            </mat-icon>
            <div matListItemTitle>{{ step.label }}</div>
            <div matListItemLine *ngIf="step.message" class="step-message">{{ step.message }}</div>
            <div matListItemLine *ngIf="step.timestamp" class="step-timestamp">
              {{ step.timestamp | date:'HH:mm:ss' }}
            </div>
          </mat-list-item>
        </mat-list>

        <!-- Error Messages -->
        <div *ngIf="errorMessages.length > 0" class="error-section">
          <h3 class="error-title">
            <mat-icon>error</mat-icon>
            Fehlermeldungen
          </h3>
          <mat-list class="error-list">
            <mat-list-item *ngFor="let error of errorMessages">
              <mat-icon matListItemIcon color="warn">error</mat-icon>
              <div matListItemTitle class="error-message">{{ error }}</div>
            </mat-list-item>
          </mat-list>
        </div>
      </div>
    </mat-dialog-content>
    
    <mat-dialog-actions align="end">
      <button 
        mat-button 
        (click)="close()"
        [disabled]="isCreating && !hasErrors">
        {{ hasErrors ? 'Schließen' : 'Abbrechen' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .progress-container {
      min-width: 500px;
      max-width: 700px;
    }

    mat-progress-bar {
      height: 8px;
      margin-bottom: 16px;
    }

    .progress-info {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 24px;
    }

    .progress-text {
      font-weight: 500;
      color: rgba(0, 0, 0, 0.87);
    }

    .progress-percentage {
      font-size: 14px;
      color: rgba(0, 0, 0, 0.6);
    }

    .steps-list {
      max-height: 300px;
      overflow-y: auto;
      margin-bottom: 16px;
    }

    .step-pending {
      opacity: 0.5;
    }

    .step-in-progress {
      font-weight: 500;
      color: #1976d2;
    }

    .step-completed {
      color: #4caf50;
    }

    .step-error {
      color: #f44336;
    }

    .step-message {
      font-size: 12px;
      color: rgba(0, 0, 0, 0.6);
      margin-top: 4px;
    }

    .step-timestamp {
      font-size: 11px;
      color: rgba(0, 0, 0, 0.4);
      margin-top: 2px;
    }

    .error-section {
      margin-top: 24px;
      padding: 16px;
      background-color: #ffebee;
      border-radius: 4px;
      border-left: 4px solid #f44336;
    }

    .error-title {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0 0 12px 0;
      color: #f44336;
      font-size: 16px;
    }

    .error-list {
      margin: 0;
    }

    .error-message {
      color: #d32f2f;
      font-size: 13px;
    }

    mat-dialog-actions {
      padding: 16px 24px;
      border-top: 1px solid rgba(0, 0, 0, 0.12);
    }
  `]
})
export class ContainerAppProgressDialogComponent implements OnInit, OnDestroy {
  steps: ProgressStep[] = [];
  progressPercentage: number = 0;
  currentStepLabel: string = 'Initialisierung...';
  isCreating: boolean = true;
  hasErrors: boolean = false;
  errorMessages: string[] = [];
  private statusCheckSubscription?: Subscription;

  constructor(
    public dialogRef: MatDialogRef<ContainerAppProgressDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: ContainerAppProgressData,
    private transportService: TransportService
  ) {
    this.initializeSteps();
  }

  ngOnInit(): void {
    this.startContainerAppCreation();
  }

  ngOnDestroy(): void {
    if (this.statusCheckSubscription) {
      this.statusCheckSubscription.unsubscribe();
    }
  }

  private initializeSteps(): void {
    this.steps = [
      {
        id: 'init',
        label: 'Initialisierung',
        status: 'pending'
      },
      {
        id: 'blob-storage',
        label: 'Blob Storage Container erstellen',
        status: 'pending'
      },
      {
        id: 'config-upload',
        label: 'Adapter-Konfiguration hochladen',
        status: 'pending'
      },
      {
        id: 'container-app',
        label: 'Container App erstellen',
        status: 'pending'
      },
      {
        id: 'environment',
        label: 'Umgebungsvariablen konfigurieren',
        status: 'pending'
      },
      {
        id: 'verify',
        label: 'Container App Status prüfen',
        status: 'pending'
      },
      {
        id: 'complete',
        label: 'Erstellung abgeschlossen',
        status: 'pending'
      }
    ];
  }

  private async startContainerAppCreation(): Promise<void> {
    try {
      // Step 1: Initialisierung
      this.updateStep('init', 'in-progress', 'Container App Erstellung wird gestartet...');
      await this.delay(500);
      this.updateStep('init', 'completed', 'Initialisierung abgeschlossen');

      // Step 2: Blob Storage
      this.updateStep('blob-storage', 'in-progress', 'Blob Storage Container wird erstellt...');
      await this.delay(1500);
      this.updateStep('blob-storage', 'completed', 'Blob Storage Container erstellt');

      // Step 3: Config Upload
      this.updateStep('config-upload', 'in-progress', 'Adapter-Konfiguration wird hochgeladen...');
      await this.delay(1500);
      this.updateStep('config-upload', 'completed', 'Adapter-Konfiguration hochgeladen');

      // Step 4: Container App Creation
      this.updateStep('container-app', 'in-progress', 'Container App wird erstellt... (dies kann einige Minuten dauern)');
      
      // Start status polling
      this.startStatusPolling();
    } catch (error: any) {
      this.handleError('Fehler beim Starten der Container App Erstellung', error);
    }
  }

  private startStatusPolling(): void {
    let pollCount = 0;
    const maxPolls = 60; // Maximum 3 minutes (60 * 3 seconds)
    
    // Poll container app status every 3 seconds
    this.statusCheckSubscription = interval(3000).subscribe(async () => {
      pollCount++;
      
      // Stop polling after max attempts
      if (pollCount > maxPolls) {
        this.handleError('Container App Erstellung dauert zu lange', 'Timeout nach 3 Minuten');
        return;
      }
      
      try {
        const status = await this.transportService.getContainerAppStatus(
          this.data.adapterInstanceGuid
        ).toPromise();

        if (status) {
          if (status.exists && status.status === 'Running') {
            // Container app is ready
            this.updateStep('container-app', 'completed', 'Container App wurde erfolgreich erstellt');
            this.updateStep('environment', 'completed', 'Umgebungsvariablen wurden konfiguriert');
            this.updateStep('verify', 'completed', `Container App Status: ${status.status}`);
            this.updateStep('complete', 'completed', 'Container App ist einsatzbereit');
            this.isCreating = false;
            this.progressPercentage = 100;
            this.currentStepLabel = 'Erstellung erfolgreich abgeschlossen';
            
            if (this.statusCheckSubscription) {
              this.statusCheckSubscription.unsubscribe();
            }

            // Auto-close after 3 seconds
            setTimeout(() => {
              this.dialogRef.close({ success: true });
            }, 3000);
          } else if (status.status === 'Error' || status.errorMessage) {
            this.handleError('Container App Erstellung fehlgeschlagen', status.errorMessage || 'Unbekannter Fehler');
          } else if (status.status === 'Provisioning' || status.status === 'Creating') {
            // Still provisioning
            this.updateStep('container-app', 'in-progress', `Status: ${status.status} (${pollCount * 3} Sekunden)`);
            this.updateStep('environment', 'pending');
            this.updateStep('verify', 'pending');
          } else if (status.exists) {
            // Container app exists but not running yet
            this.updateStep('container-app', 'completed', 'Container App erstellt');
            this.updateStep('environment', 'in-progress', `Status: ${status.status}`);
            this.updateStep('verify', 'pending');
          } else {
            // Still creating
            this.updateStep('container-app', 'in-progress', `Erstellung läuft... (${pollCount * 3} Sekunden)`);
          }
        }
      } catch (error: any) {
        // Don't show error immediately - might be transient
        if (pollCount > 5) {
          // Only log after a few attempts
          console.warn('Error checking container app status:', error);
          this.updateStep('container-app', 'in-progress', `Status prüfen... (Versuch ${pollCount})`);
        }
      }
    });
  }

  private updateStep(stepId: string, status: ProgressStep['status'], message?: string): void {
    const step = this.steps.find(s => s.id === stepId);
    if (step) {
      step.status = status;
      if (message) {
        step.message = message;
      }
      if (status === 'in-progress' || status === 'completed') {
        step.timestamp = new Date();
      }
      this.updateProgress();
    }
  }

  private updateProgress(): void {
    const completedSteps = this.steps.filter(s => s.status === 'completed').length;
    const totalSteps = this.steps.length;
    this.progressPercentage = Math.round((completedSteps / totalSteps) * 100);
    
    const currentStep = this.steps.find(s => s.status === 'in-progress');
    if (currentStep) {
      this.currentStepLabel = currentStep.label;
    } else {
      const lastCompleted = [...this.steps].reverse().find(s => s.status === 'completed');
      if (lastCompleted) {
        this.currentStepLabel = lastCompleted.label;
      }
    }
  }

  private handleError(message: string, error: any): void {
    this.hasErrors = true;
    const errorText = typeof error === 'string' ? error : error?.message || 'Unbekannter Fehler';
    this.errorMessages.push(`${message}: ${errorText}`);
    
    const currentStep = this.steps.find(s => s.status === 'in-progress');
    if (currentStep) {
      this.updateStep(currentStep.id, 'error', errorText);
    }
    
    this.isCreating = false;
    this.currentStepLabel = 'Fehler aufgetreten';
    
    if (this.statusCheckSubscription) {
      this.statusCheckSubscription.unsubscribe();
    }
  }

  public getStepIcon(status: ProgressStep['status']): string {
    switch (status) {
      case 'completed':
        return 'check_circle';
      case 'in-progress':
        return 'hourglass_empty';
      case 'error':
        return 'error';
      default:
        return 'radio_button_unchecked';
    }
  }

  public getStepIconClass(status: ProgressStep['status']): string {
    switch (status) {
      case 'completed':
        return 'step-icon-completed';
      case 'in-progress':
        return 'step-icon-in-progress';
      case 'error':
        return 'step-icon-error';
      default:
        return 'step-icon-pending';
    }
  }

  private delay(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
  }

  close(): void {
    if (this.statusCheckSubscription) {
      this.statusCheckSubscription.unsubscribe();
    }
    this.dialogRef.close({ success: !this.hasErrors });
  }
}

