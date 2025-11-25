import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatExpansionModule } from '@angular/material/expansion';

export interface ServiceBusMessageDialogData {
  message: any;
}

@Component({
  selector: 'app-service-bus-message-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatExpansionModule
  ],
  template: `
    <h2 mat-dialog-title>
      <mat-icon>message</mat-icon>
      Service Bus Message Details
    </h2>
    <mat-dialog-content>
      <div class="message-details">
        <mat-card class="message-card">
          <mat-card-header>
            <mat-card-title>Message Information</mat-card-title>
          </mat-card-header>
          <mat-card-content>
            <div class="detail-row">
              <strong>Message ID:</strong>
              <span class="message-id">{{ data.message.messageId }}</span>
            </div>
            <div class="detail-row">
              <strong>Interface Name:</strong>
              <span>{{ data.message.interfaceName }}</span>
            </div>
            <div class="detail-row">
              <strong>Adapter Name:</strong>
              <span>{{ data.message.adapterName }}</span>
            </div>
            <div class="detail-row">
              <strong>Adapter Type:</strong>
              <span>{{ data.message.adapterType }}</span>
            </div>
            <div class="detail-row">
              <strong>Adapter Instance GUID:</strong>
              <span class="guid">{{ data.message.adapterInstanceGuid }}</span>
            </div>
            <div class="detail-row">
              <strong>Enqueued Time:</strong>
              <span>{{ data.message.enqueuedTime | date:'medium' }}</span>
            </div>
            <div class="detail-row">
              <strong>Delivery Count:</strong>
              <span>{{ data.message.deliveryCount }}</span>
            </div>
          </mat-card-content>
        </mat-card>

        <mat-expansion-panel expanded="true" class="headers-panel">
          <mat-expansion-panel-header>
            <mat-panel-title>
              <mat-icon>list</mat-icon>
              Headers ({{ data.message.headers?.length || 0 }})
            </mat-panel-title>
          </mat-expansion-panel-header>
          <div class="headers-content">
            <div *ngIf="data.message.headers && data.message.headers.length > 0; else noHeaders">
              <div *ngFor="let header of data.message.headers" class="header-item">
                {{ header }}
              </div>
            </div>
            <ng-template #noHeaders>
              <p class="no-data">No headers</p>
            </ng-template>
          </div>
        </mat-expansion-panel>

        <mat-expansion-panel expanded="true" class="record-panel">
          <mat-expansion-panel-header>
            <mat-panel-title>
              <mat-icon>data_object</mat-icon>
              Record Data
            </mat-panel-title>
          </mat-expansion-panel-header>
          <div class="record-content">
            <pre *ngIf="data.message.record && Object.keys(data.message.record).length > 0; else noRecord">{{ formatRecord(data.message.record) }}</pre>
            <ng-template #noRecord>
              <p class="no-data">No record data</p>
            </ng-template>
          </div>
        </mat-expansion-panel>

        <mat-expansion-panel *ngIf="data.message.properties && Object.keys(data.message.properties).length > 0" class="properties-panel">
          <mat-expansion-panel-header>
            <mat-panel-title>
              <mat-icon>settings</mat-icon>
              Properties ({{ Object.keys(data.message.properties).length }})
            </mat-panel-title>
          </mat-expansion-panel-header>
          <div class="properties-content">
            <pre>{{ formatProperties(data.message.properties) }}</pre>
          </div>
        </mat-expansion-panel>
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="onClose()">
        <mat-icon>close</mat-icon>
        Close
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .message-details {
      min-width: 600px;
      max-width: 900px;
    }

    .message-card {
      margin-bottom: 16px;
    }

    .detail-row {
      display: flex;
      padding: 8px 0;
      border-bottom: 1px solid rgba(0, 0, 0, 0.1);
    }

    .detail-row:last-child {
      border-bottom: none;
    }

    .detail-row strong {
      min-width: 180px;
      color: rgba(0, 0, 0, 0.7);
    }

    .message-id, .guid {
      font-family: 'Courier New', monospace;
      font-size: 0.9em;
      word-break: break-all;
    }

    .headers-content, .record-content, .properties-content {
      padding: 16px;
      max-height: 400px;
      overflow-y: auto;
    }

    .header-item {
      padding: 4px 0;
      font-family: 'Courier New', monospace;
    }

    pre {
      margin: 0;
      padding: 12px;
      background-color: #f5f5f5;
      border-radius: 4px;
      overflow-x: auto;
      font-family: 'Courier New', monospace;
      font-size: 0.9em;
      white-space: pre-wrap;
      word-wrap: break-word;
    }

    .no-data {
      color: rgba(0, 0, 0, 0.5);
      font-style: italic;
      padding: 16px;
    }

    mat-expansion-panel {
      margin-bottom: 8px;
    }

    mat-panel-title {
      display: flex;
      align-items: center;
      gap: 8px;
    }
  `]
})
export class ServiceBusMessageDialogComponent {
  Object = Object; // Make Object available in template

  constructor(
    public dialogRef: MatDialogRef<ServiceBusMessageDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: ServiceBusMessageDialogData
  ) {}

  formatRecord(record: any): string {
    return JSON.stringify(record, null, 2);
  }

  formatProperties(properties: any): string {
    return JSON.stringify(properties, null, 2);
  }

  onClose(): void {
    this.dialogRef.close();
  }
}




