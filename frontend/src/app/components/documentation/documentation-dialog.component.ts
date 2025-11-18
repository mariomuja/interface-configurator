import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { DOCUMENTATION_CHAPTERS, DocumentationChapter } from '../../models/documentation.model';

@Component({
  selector: 'app-documentation-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatTabsModule
  ],
  template: `
    <div class="documentation-dialog-container">
      <div class="dialog-header">
        <h2>Dokumentation</h2>
        <button mat-icon-button (click)="close()" aria-label="Schließen">
          <mat-icon>close</mat-icon>
        </button>
      </div>
      
      <mat-tab-group class="documentation-tabs">
        <mat-tab *ngFor="let chapter of chapters" [label]="chapter.title">
          <div class="documentation-content" [innerHTML]="chapter.content"></div>
        </mat-tab>
      </mat-tab-group>
    </div>
  `,
  styles: [`
    .documentation-dialog-container {
      display: flex;
      flex-direction: column;
      height: 100%;
    }
    
    .dialog-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 16px 24px;
      border-bottom: 1px solid rgba(0,0,0,0.12);
    }
    
    .dialog-header h2 {
      margin: 0;
      font-size: 24px;
      font-weight: 500;
    }
    
    .documentation-tabs {
      flex: 1;
      overflow: auto;
    }
    
    .documentation-content {
      padding: 24px;
      line-height: 1.6;
      overflow-y: auto;
      max-height: calc(90vh - 120px);
    }
    
    .documentation-content h2 {
      margin-top: 0;
      color: #1976d2;
      border-bottom: 2px solid #1976d2;
      padding-bottom: 8px;
      font-weight: 500; /* Reduced from default bold (700) */
    }
    
    .documentation-content h3 {
      color: #424242;
      margin-top: 24px;
      font-weight: 500; /* Reduced from default bold (700) */
    }
    
    .documentation-content h4 {
      font-weight: 500; /* Reduced from default bold (700) */
    }
    
    .documentation-content ul, .documentation-content ol {
      margin: 16px 0;
      padding-left: 32px;
    }
    
    .documentation-content li {
      margin: 8px 0;
    }
    
    .documentation-content code {
      background-color: #f5f5f5;
      padding: 2px 6px;
      border-radius: 3px;
      font-family: 'Courier New', monospace;
      font-size: 0.9em;
    }
    
    .documentation-content strong {
      color: #1976d2;
      font-weight: 500; /* Reduced from default bold (700) for better readability */
    }
    
    .documentation-content b {
      font-weight: 500; /* Reduced from default bold (700) for better readability */
    }
  `]
})
export class DocumentationDialogComponent {
  chapters = DOCUMENTATION_CHAPTERS;
  
  constructor(private dialogRef: MatDialogRef<DocumentationDialogComponent>) {}
  
  close() {
    this.dialogRef.close();
  }
}


