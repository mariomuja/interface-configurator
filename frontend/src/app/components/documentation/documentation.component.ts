import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DOCUMENTATION_CHAPTERS, DocumentationChapter } from '../../models/documentation.model';
import { DocumentationDialogComponent } from './documentation-dialog.component';

@Component({
  selector: 'app-documentation',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatDialogModule, MatTooltipModule],
  template: `
    <button mat-icon-button 
            class="documentation-button"
            (click)="openDocumentation()"
            matTooltip="Dokumentation anzeigen"
            aria-label="Dokumentation">
      <mat-icon class="help-icon">help</mat-icon>
    </button>
  `,
  styles: [`
    .documentation-button {
      position: fixed;
      bottom: 20px;
      right: 20px;
      z-index: 1000;
      background-color: #ffc107;
      color: #000;
      box-shadow: 0 2px 8px rgba(0,0,0,0.3);
    }
    
    .documentation-button:hover {
      background-color: #ffb300;
      transform: scale(1.1);
      transition: all 0.2s;
    }
    
    .help-icon {
      font-size: 28px;
      width: 28px;
      height: 28px;
    }
  `]
})
export class DocumentationComponent {
  private dialog = inject(MatDialog);
  chapters = DOCUMENTATION_CHAPTERS;

  openDocumentation() {
    this.dialog.open(DocumentationDialogComponent, {
      width: '90%',
      maxWidth: '800px',
      maxHeight: '90vh',
      panelClass: 'documentation-dialog'
    });
  }
}

