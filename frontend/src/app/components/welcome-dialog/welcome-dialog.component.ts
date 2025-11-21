import { Component } from '@angular/core';
import { MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-welcome-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule],
  templateUrl: './welcome-dialog.component.html',
  styleUrl: './welcome-dialog.component.css'
})
export class WelcomeDialogComponent {
  constructor(public dialogRef: MatDialogRef<WelcomeDialogComponent>) {}

  onClose(): void {
    this.dialogRef.close();
  }

  onDontShowAgain(): void {
    localStorage.setItem('welcomeDialogShown', 'true');
    this.dialogRef.close();
  }
}
