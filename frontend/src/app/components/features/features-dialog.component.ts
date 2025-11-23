import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { FormsModule } from '@angular/forms';
import { FeatureService, Feature } from '../../services/feature.service';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-features-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatSlideToggleModule,
    MatCardModule,
    MatChipsModule,
    MatExpansionModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatFormFieldModule,
    MatInputModule,
    FormsModule
  ],
  template: `
    <div class="features-dialog">
      <div class="dialog-header">
        <h2 mat-dialog-title>Feature-Verwaltung</h2>
        <button mat-icon-button (click)="close()" class="close-button">
          <mat-icon>close</mat-icon>
        </button>
      </div>
      
      <mat-dialog-content class="features-content">
        <div *ngIf="loading" class="loading-container">
          <mat-spinner diameter="50"></mat-spinner>
          <p>Features werden geladen...</p>
        </div>
        
        <div *ngIf="!loading && features.length === 0" class="no-features">
          <mat-icon>info</mat-icon>
          <p>Noch keine Features vorhanden</p>
        </div>
        
        <div *ngFor="let feature of features" class="feature-card">
          <mat-card>
            <mat-card-header>
              <div class="feature-header">
                <div class="feature-title-section">
                  <mat-card-title>
                    Feature #{{ feature.featureNumber }}: {{ feature.title }}
                  </mat-card-title>
                  <div class="feature-meta">
                    <mat-chip-set>
                      <mat-chip [class]="'priority-' + feature.priority.toLowerCase()">
                        {{ feature.priority }}
                      </mat-chip>
                      <mat-chip>{{ feature.category }}</mat-chip>
                      <mat-chip *ngIf="feature.isEnabled" class="enabled-chip">
                        <mat-icon>check_circle</mat-icon>
                        Aktiviert
                      </mat-chip>
                      <mat-chip *ngIf="!feature.isEnabled" class="disabled-chip">
                        <mat-icon>cancel</mat-icon>
                        Deaktiviert
                      </mat-chip>
                    </mat-chip-set>
                    <span class="feature-date">
                      Implementiert: {{ feature.implementedDate | date:'dd.MM.yyyy HH:mm' }}
                    </span>
                    <span *ngIf="feature.enabledDate" class="feature-date enabled-date">
                      Aktiviert: {{ feature.enabledDate | date:'dd.MM.yyyy HH:mm' }} 
                      <span *ngIf="feature.enabledBy">von {{ feature.enabledBy }}</span>
                    </span>
                  </div>
                </div>
                <div class="feature-toggle-section" *ngIf="feature.canToggle">
                  <mat-slide-toggle
                    [checked]="feature.isEnabled"
                    (change)="toggleFeature(feature)"
                    [disabled]="togglingFeatureId === feature.id">
                    <span class="toggle-label">
                      {{ feature.isEnabled ? 'Aktiviert' : 'Deaktiviert' }}
                    </span>
                  </mat-slide-toggle>
                </div>
              </div>
            </mat-card-header>
            
            <mat-card-content>
              <div class="feature-description">
                <p class="short-description">{{ feature.description }}</p>
              </div>
              
              <mat-accordion class="feature-details">
                <mat-expansion-panel>
                  <mat-expansion-panel-header>
                    <mat-panel-title>
                      <mat-icon>description</mat-icon>
                      Detaillierte Beschreibung
                    </mat-panel-title>
                  </mat-expansion-panel-header>
                  <div class="detail-content" [innerHTML]="formatText(feature.detailedDescription)"></div>
                </mat-expansion-panel>
                
                <mat-expansion-panel *ngIf="feature.testInstructions">
                  <mat-expansion-panel-header>
                    <mat-panel-title>
                      <mat-icon>bug_report</mat-icon>
                      Testanweisungen
                    </mat-panel-title>
                  </mat-expansion-panel-header>
                  <div class="detail-content" [innerHTML]="formatText(feature.testInstructions)"></div>
                </mat-expansion-panel>
                
                <mat-expansion-panel *ngIf="feature.technicalDetails">
                  <mat-expansion-panel-header>
                    <mat-panel-title>
                      <mat-icon>code</mat-icon>
                      Technische Details
                    </mat-panel-title>
                  </mat-expansion-panel-header>
                  <div class="detail-content" [innerHTML]="formatText(feature.technicalDetails)"></div>
                </mat-expansion-panel>
                
                <mat-expansion-panel *ngIf="feature.dependencies">
                  <mat-expansion-panel-header>
                    <mat-panel-title>
                      <mat-icon>link</mat-icon>
                      Abhängigkeiten
                    </mat-panel-title>
                  </mat-expansion-panel-header>
                  <div class="detail-content" [innerHTML]="formatText(feature.dependencies)"></div>
                </mat-expansion-panel>
                
                <mat-expansion-panel *ngIf="feature.knownIssues">
                  <mat-expansion-panel-header>
                    <mat-panel-title>
                      <mat-icon>warning</mat-icon>
                      Bekannte Probleme
                    </mat-panel-title>
                  </mat-expansion-panel-header>
                  <div class="detail-content warning-content" [innerHTML]="formatText(feature.knownIssues)"></div>
                </mat-expansion-panel>
                
                <mat-expansion-panel *ngIf="feature.breakingChanges">
                  <mat-expansion-panel-header>
                    <mat-panel-title>
                      <mat-icon>error</mat-icon>
                      Breaking Changes
                    </mat-panel-title>
                  </mat-expansion-panel-header>
                  <div class="detail-content error-content" [innerHTML]="formatText(feature.breakingChanges)"></div>
                </mat-expansion-panel>
                
                <mat-expansion-panel [expanded]="false">
                  <mat-expansion-panel-header>
                    <mat-panel-title>
                      <mat-icon>comment</mat-icon>
                      Testergebnis-Kommentar
                      <span *ngIf="feature.testComment" class="comment-badge">Vorhanden</span>
                    </mat-panel-title>
                  </mat-expansion-panel-header>
                  <div class="test-comment-section">
                    <div *ngIf="feature.testComment" class="existing-comment">
                      <div class="comment-header">
                        <span class="comment-author">
                          <mat-icon>person</mat-icon>
                          {{ feature.testCommentBy || 'Unbekannt' }}
                        </span>
                        <span class="comment-date" *ngIf="feature.testCommentDate">
                          {{ feature.testCommentDate | date:'dd.MM.yyyy HH:mm' }}
                        </span>
                      </div>
                      <div class="comment-content" [innerHTML]="formatText(feature.testComment)"></div>
                    </div>
                    <div *ngIf="!feature.testComment" class="no-comment">
                      <p>Noch kein Testergebnis-Kommentar vorhanden.</p>
                    </div>
                    <div class="comment-input-section">
                      <mat-form-field appearance="outline" class="full-width">
                        <mat-label>Testergebnis-Kommentar hinzufügen/aktualisieren</mat-label>
                        <textarea 
                          matInput 
                          [(ngModel)]="editingComments[feature.id]"
                          [placeholder]="feature.testComment || 'Beschreiben Sie hier das Testergebnis...'"
                          rows="5"
                          maxlength="5000">
                        </textarea>
                        <mat-hint>Dieser Kommentar ist für alle Benutzer sichtbar und erklärt, warum das Feature noch nicht freigegeben wurde.</mat-hint>
                      </mat-form-field>
                      <div class="comment-actions">
                        <button 
                          mat-raised-button 
                          color="primary" 
                          (click)="saveTestComment(feature)"
                          [disabled]="savingCommentId === feature.id || !editingComments[feature.id]?.trim()">
                          <mat-icon>save</mat-icon>
                          Kommentar speichern
                        </button>
                        <button 
                          mat-button 
                          (click)="editingComments[feature.id] = feature.testComment || ''"
                          *ngIf="editingComments[feature.id]">
                          Abbrechen
                        </button>
                      </div>
                    </div>
                  </div>
                </mat-expansion-panel>
              </mat-accordion>
            </mat-card-content>
          </mat-card>
        </div>
      </mat-dialog-content>
    </div>
  `,
  styles: [`
    .features-dialog {
      width: 90vw;
      max-width: 1200px;
      max-height: 90vh;
    }
    
    .dialog-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 16px 24px;
      border-bottom: 1px solid rgba(0,0,0,0.12);
    }
    
    .close-button {
      margin-left: auto;
    }
    
    .features-content {
      max-height: calc(90vh - 100px);
      overflow-y: auto;
      padding: 16px;
    }
    
    .loading-container, .no-features {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 40px;
      gap: 16px;
    }
    
    .feature-card {
      margin-bottom: 16px;
    }
    
    .feature-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      width: 100%;
      gap: 16px;
    }
    
    .feature-title-section {
      flex: 1;
    }
    
    .feature-meta {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      align-items: center;
      margin-top: 8px;
    }
    
    .feature-date {
      font-size: 12px;
      color: rgba(0,0,0,0.6);
      margin-left: 8px;
    }
    
    .enabled-date {
      color: #4caf50;
    }
    
    .feature-toggle-section {
      display: flex;
      align-items: center;
    }
    
    .toggle-label {
      margin-left: 8px;
      font-size: 14px;
    }
    
    .short-description {
      font-size: 14px;
      color: rgba(0,0,0,0.87);
      margin: 8px 0;
    }
    
    .feature-details {
      margin-top: 16px;
    }
    
    .detail-content {
      padding: 16px;
      white-space: pre-wrap;
      line-height: 1.6;
    }
    
    .warning-content {
      background-color: #fff3cd;
      border-left: 4px solid #ffc107;
    }
    
    .error-content {
      background-color: #f8d7da;
      border-left: 4px solid #dc3545;
    }
    
    mat-chip {
      font-size: 12px;
    }
    
    .priority-high {
      background-color: #ff9800 !important;
      color: white !important;
    }
    
    .priority-critical {
      background-color: #f44336 !important;
      color: white !important;
    }
    
    .priority-medium {
      background-color: #2196f3 !important;
      color: white !important;
    }
    
    .priority-low {
      background-color: #9e9e9e !important;
      color: white !important;
    }
    
    .enabled-chip {
      background-color: #4caf50 !important;
      color: white !important;
    }
    
    .disabled-chip {
      background-color: #9e9e9e !important;
      color: white !important;
    }
    
    mat-expansion-panel-header mat-icon {
      margin-right: 8px;
    }
    
    .test-comment-section {
      padding: 16px;
    }
    
    .existing-comment {
      margin-bottom: 16px;
      padding: 12px;
      background-color: #f5f5f5;
      border-radius: 4px;
      border-left: 4px solid #2196f3;
    }
    
    .comment-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 8px;
      font-size: 12px;
      color: rgba(0,0,0,0.6);
    }
    
    .comment-author {
      display: flex;
      align-items: center;
      gap: 4px;
    }
    
    .comment-author mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
    }
    
    .comment-content {
      white-space: pre-wrap;
      line-height: 1.6;
      color: rgba(0,0,0,0.87);
    }
    
    .no-comment {
      padding: 12px;
      color: rgba(0,0,0,0.6);
      font-style: italic;
      margin-bottom: 16px;
    }
    
    .comment-input-section {
      margin-top: 16px;
    }
    
    .full-width {
      width: 100%;
    }
    
    .comment-actions {
      display: flex;
      gap: 8px;
      margin-top: 8px;
    }
    
    .comment-badge {
      margin-left: 8px;
      padding: 2px 8px;
      background-color: #4caf50;
      color: white;
      border-radius: 12px;
      font-size: 11px;
    }
  `]
})
export class FeaturesDialogComponent implements OnInit {
  features: Feature[] = [];
  loading = true;
  togglingFeatureId: number | null = null;
  savingCommentId: number | null = null;
  editingComments: { [featureId: number]: string } = {};
  
  constructor(
    public dialogRef: MatDialogRef<FeaturesDialogComponent>,
    private featureService: FeatureService,
    private authService: AuthService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.loadFeatures();
  }

  loadFeatures(): void {
    this.loading = true;
    this.featureService.getFeatures().subscribe({
      next: (features) => {
        this.features = features;
        // Initialize editing comments with existing comments
        features.forEach(f => {
          this.editingComments[f.id] = f.testComment || '';
        });
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading features:', error);
        this.snackBar.open('Fehler beim Laden der Features', 'Schließen', { duration: 3000 });
        this.loading = false;
      }
    });
  }

  toggleFeature(feature: Feature): void {
    if (!feature.canToggle) {
      return;
    }

    this.togglingFeatureId = feature.id;
    this.featureService.toggleFeature(feature.id).subscribe({
      next: (result) => {
        if (result.success) {
          feature.isEnabled = !feature.isEnabled;
          feature.enabledDate = feature.isEnabled ? new Date().toISOString() : undefined;
          feature.enabledBy = feature.isEnabled ? this.authService.getCurrentUser()?.username : undefined;
          this.snackBar.open(
            `Feature ${feature.isEnabled ? 'aktiviert' : 'deaktiviert'}`,
            'Schließen',
            { duration: 2000 }
          );
        } else {
          this.snackBar.open('Fehler beim Umschalten des Features', 'Schließen', { duration: 3000 });
        }
        this.togglingFeatureId = null;
      },
      error: (error) => {
        console.error('Error toggling feature:', error);
        this.snackBar.open('Fehler beim Umschalten des Features', 'Schließen', { duration: 3000 });
        this.togglingFeatureId = null;
      }
    });
  }

  saveTestComment(feature: Feature): void {
    const comment = this.editingComments[feature.id]?.trim();
    if (!comment) {
      return;
    }

    this.savingCommentId = feature.id;
    this.featureService.updateTestComment(feature.id, comment).subscribe({
      next: (result) => {
        if (result.success) {
          feature.testComment = comment;
          feature.testCommentBy = this.authService.getCurrentUser()?.username;
          feature.testCommentDate = new Date().toISOString();
          this.snackBar.open('Testergebnis-Kommentar gespeichert', 'Schließen', { duration: 2000 });
        } else {
          this.snackBar.open('Fehler beim Speichern des Kommentars', 'Schließen', { duration: 3000 });
        }
        this.savingCommentId = null;
      },
      error: (error) => {
        console.error('Error saving test comment:', error);
        this.snackBar.open('Fehler beim Speichern des Kommentars', 'Schließen', { duration: 3000 });
        this.savingCommentId = null;
      }
    });
  }

  formatText(text: string | undefined): string {
    if (!text) return '';
    // Convert markdown-style formatting to HTML
    return text
      .replace(/\n/g, '<br>')
      .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
      .replace(/\*(.*?)\*/g, '<em>$1</em>')
      .replace(/`(.*?)`/g, '<code>$1</code>');
  }

  close(): void {
    this.dialogRef.close();
  }
}

