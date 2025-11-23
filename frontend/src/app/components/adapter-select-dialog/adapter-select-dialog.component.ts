import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatTooltipModule } from '@angular/material/tooltip';

export interface AdapterSelectData {
  adapterType: 'Source' | 'Destination';
}

export interface AdapterInfo {
  name: string;
  alias: string;
  icon: string;
  description: string;
  supportsSource: boolean;
  supportsDestination: boolean;
}

@Component({
  selector: 'app-adapter-select-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatTooltipModule
  ],
  template: `
    <div class="adapter-select-dialog">
      <h2 mat-dialog-title>
        <mat-icon>{{ data.adapterType === 'Source' ? 'input' : 'output' }}</mat-icon>
        {{ data.adapterType === 'Source' ? 'Quell-Adapter auswählen' : 'Ziel-Adapter auswählen' }}
      </h2>
      
      <mat-dialog-content>
        <div class="adapter-grid">
          <mat-card 
            *ngFor="let adapter of availableAdapters" 
            class="adapter-card"
            [class.selected]="selectedAdapter === adapter.name"
            (click)="selectAdapter(adapter)">
            <mat-card-content>
              <div class="adapter-icon">
                <mat-icon [fontIcon]="adapter.icon" class="large-icon"></mat-icon>
              </div>
              <h3>{{ adapter.alias }}</h3>
              <p class="adapter-description">{{ adapter.description }}</p>
            </mat-card-content>
          </mat-card>
        </div>
      </mat-dialog-content>
      
      <mat-dialog-actions align="end">
        <button mat-button (click)="close()">Abbrechen</button>
        <button 
          mat-raised-button 
          color="primary" 
          [disabled]="!selectedAdapter"
          (click)="confirm()">
          Auswählen
        </button>
      </mat-dialog-actions>
    </div>
  `,
  styles: [`
    .adapter-select-dialog {
      width: 800px;
      max-width: 90vw;
    }
    
    h2 mat-dialog-title {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    
    .adapter-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
      gap: 16px;
      padding: 16px 0;
    }
    
    .adapter-card {
      cursor: pointer;
      transition: all 0.2s;
      border: 2px solid transparent;
    }
    
    .adapter-card:hover {
      transform: translateY(-4px);
      box-shadow: 0 4px 12px rgba(0,0,0,0.15);
    }
    
    .adapter-card.selected {
      border-color: #1976d2;
      background-color: #e3f2fd;
    }
    
    .adapter-icon {
      text-align: center;
      margin-bottom: 12px;
    }
    
    .large-icon {
      font-size: 48px;
      width: 48px;
      height: 48px;
      color: #1976d2;
    }
    
    .adapter-card h3 {
      margin: 0 0 8px 0;
      text-align: center;
      font-size: 16px;
    }
    
    .adapter-description {
      margin: 0;
      font-size: 12px;
      color: rgba(0,0,0,0.6);
      text-align: center;
    }
  `]
})
export class AdapterSelectDialogComponent {
  selectedAdapter: string | null = null;
  
  allAdapters: AdapterInfo[] = [
    {
      name: 'CSV',
      alias: 'CSV',
      icon: 'description',
      description: 'CSV-Dateien aus Blob Storage lesen/schreiben',
      supportsSource: true,
      supportsDestination: true
    },
    {
      name: 'SqlServer',
      alias: 'SQL Server',
      icon: 'storage',
      description: 'Daten aus/in SQL Server Datenbanken',
      supportsSource: true,
      supportsDestination: true
    },
    {
      name: 'SAP',
      alias: 'SAP',
      icon: 'business',
      description: 'IDOCs aus SAP abrufen oder an SAP senden',
      supportsSource: true,
      supportsDestination: true
    },
    {
      name: 'Dynamics365',
      alias: 'Dynamics 365',
      icon: 'cloud',
      description: 'Daten aus/in Microsoft Dynamics 365',
      supportsSource: true,
      supportsDestination: true
    },
    {
      name: 'CRM',
      alias: 'Microsoft CRM',
      icon: 'contacts',
      description: 'Daten aus/in Microsoft CRM',
      supportsSource: true,
      supportsDestination: true
    }
  ];

  get availableAdapters(): AdapterInfo[] {
    return this.allAdapters.filter(adapter => 
      this.data.adapterType === 'Source' ? adapter.supportsSource : adapter.supportsDestination
    );
  }

  constructor(
    public dialogRef: MatDialogRef<AdapterSelectDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: AdapterSelectData
  ) {}

  selectAdapter(adapter: AdapterInfo): void {
    this.selectedAdapter = adapter.name;
  }

  confirm(): void {
    if (this.selectedAdapter) {
      this.dialogRef.close(this.selectedAdapter);
    }
  }

  close(): void {
    this.dialogRef.close(null);
  }
}


