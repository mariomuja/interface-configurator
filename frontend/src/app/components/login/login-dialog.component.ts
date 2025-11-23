import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatInputModule,
    MatFormFieldModule,
    MatIconModule,
    MatSnackBarModule
  ],
  template: `
    <div class="login-dialog">
      <h2 mat-dialog-title>Anmelden</h2>
      
      <mat-dialog-content>
        <!-- Demo-User Quick Login -->
        <div class="demo-login-section">
          <h3>Demo-Benutzer (ohne Passwort)</h3>
          <button 
            mat-raised-button 
            color="accent" 
            class="demo-login-button"
            (click)="loginAsDemo()"
            [disabled]="loggingIn">
            <mat-icon>person</mat-icon>
            Als Demo-Benutzer anmelden
          </button>
          <p class="demo-description">
            Als Demo-Benutzer können Sie die Anwendung sofort testen. 
            Sie können Features ansehen, aber keine Features für andere Benutzer freischalten.
          </p>
        </div>

        <div class="divider">
          <span>oder</span>
        </div>

        <!-- Admin Login -->
        <div class="admin-login-section">
          <h3>Admin-Anmeldung</h3>
          <p class="admin-info">
            <mat-icon>admin_panel_settings</mat-icon>
            Als Admin-Benutzer können Sie Features für alle anderen Benutzer freischalten.
            Verwenden Sie Ihre Admin-Anmeldedaten.
          </p>
          
          <form (ngSubmit)="login()" #loginForm="ngForm">
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Benutzername</mat-label>
              <input matInput [(ngModel)]="username" name="username" [required]="!isDemoLogin">
              <mat-icon matPrefix>person</mat-icon>
            </mat-form-field>
            
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Passwort</mat-label>
              <input matInput type="password" [(ngModel)]="password" name="password" [required]="!isDemoLogin">
              <mat-icon matPrefix>lock</mat-icon>
            </mat-form-field>
            
            <div class="login-info">
              <p><strong>Admin-Zugangsdaten:</strong></p>
              <ul>
                <li><strong>Admin:</strong> admin / admin123</li>
              </ul>
            </div>
          </form>
        </div>
      </mat-dialog-content>
      
      <mat-dialog-actions align="end">
        <button mat-button (click)="close()">Abbrechen</button>
        <button 
          mat-raised-button 
          color="primary" 
          (click)="login()" 
          [disabled]="loggingIn || !username || !password"
          *ngIf="!isDemoLogin">
          <mat-icon *ngIf="!loggingIn">login</mat-icon>
          <span *ngIf="loggingIn">Wird angemeldet...</span>
          <span *ngIf="!loggingIn">Als Admin anmelden</span>
        </button>
      </mat-dialog-actions>
    </div>
  `,
  styles: [`
    .login-dialog {
      width: 400px;
      padding: 16px;
    }
    
    .full-width {
      width: 100%;
      margin-bottom: 16px;
    }
    
    .login-info {
      margin-top: 16px;
      padding: 12px;
      background-color: #f5f5f5;
      border-radius: 4px;
      font-size: 12px;
    }
    
    .login-info ul {
      margin: 8px 0 0 0;
      padding-left: 20px;
    }
    
    .login-info li {
      margin: 4px 0;
    }
    
    .demo-login-section {
      margin-bottom: 24px;
      padding: 16px;
      background-color: #e3f2fd;
      border-radius: 8px;
      border: 2px solid #2196f3;
    }
    
    .demo-login-section h3 {
      margin: 0 0 12px 0;
      color: #1976d2;
      font-size: 16px;
    }
    
    .demo-login-button {
      width: 100%;
      margin-bottom: 12px;
      height: 48px;
      font-size: 16px;
    }
    
    .demo-description {
      margin: 0;
      font-size: 13px;
      color: rgba(0,0,0,0.7);
      line-height: 1.5;
    }
    
    .divider {
      text-align: center;
      margin: 24px 0;
      position: relative;
    }
    
    .divider::before {
      content: '';
      position: absolute;
      top: 50%;
      left: 0;
      right: 0;
      height: 1px;
      background-color: rgba(0,0,0,0.12);
    }
    
    .divider span {
      background-color: white;
      padding: 0 16px;
      position: relative;
      color: rgba(0,0,0,0.6);
    }
    
    .admin-login-section {
      margin-top: 24px;
    }
    
    .admin-login-section h3 {
      margin: 0 0 12px 0;
      font-size: 16px;
    }
    
    .admin-info {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 12px;
      background-color: #fff3cd;
      border-left: 4px solid #ffc107;
      border-radius: 4px;
      margin-bottom: 16px;
      font-size: 13px;
      color: rgba(0,0,0,0.87);
    }
    
    .admin-info mat-icon {
      color: #ff9800;
    }
  `]
})
export class LoginDialogComponent {
  username = '';
  password = '';
  loggingIn = false;
  isDemoLogin = false;

  constructor(
    public dialogRef: MatDialogRef<LoginDialogComponent>,
    private authService: AuthService,
    private snackBar: MatSnackBar
  ) {}

  loginAsDemo(): void {
    this.isDemoLogin = true;
    this.loggingIn = true;
    // Demo-User "test" can login without password
    this.authService.login('test', '').subscribe({
      next: (response) => {
        this.loggingIn = false;
        if (response.success) {
          this.snackBar.open(`Willkommen, ${response.user?.username}! (Demo-Benutzer)`, 'Schließen', { duration: 2000 });
          this.dialogRef.close(true);
        } else {
          this.snackBar.open(response.errorMessage || 'Anmeldung fehlgeschlagen', 'Schließen', { duration: 3000 });
        }
        this.isDemoLogin = false;
      },
      error: (error) => {
        this.loggingIn = false;
        this.isDemoLogin = false;
        console.error('Demo login error:', error);
        this.snackBar.open('Fehler bei der Anmeldung', 'Schließen', { duration: 3000 });
      }
    });
  }

  login(): void {
    if (!this.username || !this.password) {
      this.snackBar.open('Bitte geben Sie Benutzername und Passwort ein', 'Schließen', { duration: 3000 });
      return;
    }

    this.isDemoLogin = false;
    this.loggingIn = true;
    this.authService.login(this.username, this.password).subscribe({
      next: (response) => {
        this.loggingIn = false;
        if (response.success) {
          const roleText = response.user?.role === 'admin' ? ' (Admin)' : '';
          this.snackBar.open(`Willkommen, ${response.user?.username}!${roleText}`, 'Schließen', { duration: 2000 });
          this.dialogRef.close(true);
        } else {
          this.snackBar.open(response.errorMessage || 'Anmeldung fehlgeschlagen', 'Schließen', { duration: 3000 });
        }
      },
      error: (error) => {
        this.loggingIn = false;
        console.error('Login error:', error);
        this.snackBar.open('Fehler bei der Anmeldung', 'Schließen', { duration: 3000 });
      }
    });
  }

  close(): void {
    this.dialogRef.close(false);
  }
}

