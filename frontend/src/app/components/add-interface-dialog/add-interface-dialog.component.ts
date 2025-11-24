import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface AddInterfaceDialogData {
  existingNames: string[];
}

@Component({
  selector: 'app-add-interface-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule
  ],
  template: `
    <h2 mat-dialog-title>
      <mat-icon>add</mat-icon>
      Neue Schnittstelle erstellen
    </h2>
    <mat-dialog-content>
      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Schnittstellen-Name</mat-label>
        <input matInput [(ngModel)]="interfaceName" (keyup.enter)="onCreate()" placeholder="z.B. FromCsvToSqlServerExample" />
        <mat-icon matSuffix>label</mat-icon>
      </mat-form-field>
      <p class="hint">Der Name muss mindestens 5 Zeichen lang sein und eindeutig sein.</p>
      <p *ngIf="errorMessage" class="error-message">{{ errorMessage }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="onCancel()">Abbrechen</button>
      <button mat-raised-button color="primary" (click)="onCreate()" [disabled]="!isValid()">Erstellen</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .full-width {
      width: 100%;
      margin-bottom: 10px;
    }
    .hint {
      font-size: 12px;
      color: #666;
      margin-top: 5px;
    }
    .error-message {
      color: #f44336;
      font-size: 14px;
      margin-top: 10px;
    }
    mat-dialog-content {
      min-width: 400px;
    }
  `]
})
export class AddInterfaceDialogComponent {
  interfaceName: string = '';
  errorMessage: string = '';

  constructor(
    public dialogRef: MatDialogRef<AddInterfaceDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: AddInterfaceDialogData
  ) {}

  isValid(): boolean {
    const trimmed = this.interfaceName.trim();
    if (!trimmed || trimmed.length < 5) {
      return false;
    }
    if (this.data.existingNames.some(name => name.toLowerCase() === trimmed.toLowerCase())) {
      return false;
    }
    return true;
  }

  onCreate(): void {
    const trimmed = this.interfaceName.trim();
    
    if (!trimmed) {
      this.errorMessage = 'Schnittstellen-Name darf nicht leer sein.';
      return;
    }
    
    if (trimmed.length < 5) {
      this.errorMessage = 'Schnittstellen-Name muss mindestens 5 Zeichen lang sein.';
      return;
    }
    
    if (this.data.existingNames.some(name => name.toLowerCase() === trimmed.toLowerCase())) {
      this.errorMessage = 'Eine Schnittstelle mit diesem Namen existiert bereits.';
      return;
    }
    
    this.dialogRef.close(trimmed);
  }

  onCancel(): void {
    this.dialogRef.close();
  }
}

