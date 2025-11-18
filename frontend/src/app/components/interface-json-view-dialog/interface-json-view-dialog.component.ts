import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

export interface InterfaceJsonViewData {
  interfaceName: string;
  jsonString: string;
}

@Component({
  selector: 'app-interface-json-view-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule
  ],
  templateUrl: './interface-json-view-dialog.component.html',
  styleUrl: './interface-json-view-dialog.component.css'
})
export class InterfaceJsonViewDialogComponent {
  constructor(
    public dialogRef: MatDialogRef<InterfaceJsonViewDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data?: InterfaceJsonViewData
  ) {}

  onClose(): void {
    this.dialogRef.close();
  }

  copyToClipboard(): void {
    if (!this.data?.jsonString) {
      console.error('No data available to copy');
      return;
    }
    navigator.clipboard.writeText(this.data.jsonString).then(() => {
      // Could show a snackbar here if needed
    }).catch(err => {
      console.error('Failed to copy to clipboard:', err);
    });
  }
}

