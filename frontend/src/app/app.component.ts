import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { DocumentationComponent } from './components/documentation/documentation.component';
import { DocumentationDialogComponent } from './components/documentation/documentation-dialog.component';
import { TranslationService, Language } from './services/translation.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    RouterOutlet,
    MatToolbarModule,
    MatSelectModule,
    MatFormFieldModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    FormsModule,
    CommonModule,
    DocumentationComponent
  ],
  template: `
    <mat-toolbar color="primary" class="app-toolbar">
      <div class="toolbar-content">
        <div class="app-title-section">
          <span class="app-title">{{ getTranslation('app.title') }}</span>
          <p class="app-description">There's much more happening here behind this simple example. 4 weeks to implement a new data transport? Eliminate the need to IMPLEMENT interfaces. Instead CONFIGURE them. Same codebase and quality for all interfaces.</p>
        </div>
        <div class="toolbar-right">
          <button mat-button class="documentation-link" (click)="openDocumentation()">
            <mat-icon>menu_book</mat-icon>
            Read the documentation
          </button>
          <div class="profile-card">
            <div class="profile-text">
              <strong>Mario Muja</strong><span>Hamburg, Germany</span><br>
              <span class="contact-links">
                <a href="tel:+4915204641473" target="_blank" rel="noopener noreferrer">Call me: +49 1520 464 14 73</a> / 
                <a href="tel:+393453450098" target="_blank" rel="noopener noreferrer">+39 345 345 00 98</a><br>
                <a href="mailto:mario.muja@gmail.com" target="_blank" rel="noopener noreferrer">mario.muja&#64;gmail.com</a> | 
                <a href="https://github.com/mariomuja" target="_blank" rel="noopener noreferrer">GitHub</a> | 
                <a href="https://www.linkedin.com/in/mario-muja-016782347" target="_blank" rel="noopener noreferrer">LinkedIn</a>
              </span>
            </div>
          </div>
          <div class="language-selector">
            <mat-form-field appearance="outline" class="language-field">
              <mat-select [(ngModel)]="currentLanguage" (selectionChange)="onLanguageChange($event.value)">
                <mat-option *ngFor="let lang of availableLanguages" [value]="lang">
                  {{ getLanguageName(lang) }}
                </mat-option>
              </mat-select>
            </mat-form-field>
          </div>
        </div>
      </div>
    </mat-toolbar>
    <main style="padding: 20px;">
      <router-outlet></router-outlet>
    </main>
    <app-documentation></app-documentation>
  `,
  styles: [`
    :host {
      display: flex;
      flex-direction: column;
      height: 100vh;
    }
    
    .app-toolbar {
      padding: 0 16px;
      height: auto;
      min-height: 64px;
    }
    
    .toolbar-content {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      width: 100%;
      padding: 8px 0;
      gap: 16px;
    }
    
    .app-title-section {
      flex: 1 1 auto;
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      min-width: 0;
      margin-right: 16px;
    }
    
    .app-title {
      font-size: 18px;
      font-weight: 500;
      white-space: nowrap;
      margin: 0 0 4px 0;
      text-align: left;
    }
    
    .app-description {
      font-size: 11px;
      line-height: 1.4;
      color: rgba(255, 255, 255, 0.9);
      margin: 0;
      text-align: left;
      width: 100%;
      word-wrap: break-word;
      overflow-wrap: break-word;
      white-space: normal;
    }
    
    .toolbar-right {
      display: flex;
      align-items: center;
      gap: 16px;
      margin-left: auto;
    }
    
    .documentation-link {
      color: rgba(255, 255, 255, 0.9);
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 14px;
      transition: color 0.2s ease;
    }
    
    .documentation-link:hover {
      color: rgba(255, 255, 255, 1);
      background-color: rgba(255, 255, 255, 0.1);
    }
    
    .documentation-link mat-icon {
      font-size: 20px;
      width: 20px;
      height: 20px;
      line-height: 20px;
    }
    
    .profile-card {
      background: linear-gradient(145deg, #d3d3d3, #c0c0c0);
      padding: 10px 14px;
      border-radius: 8px;
      font-size: 12px;
      line-height: 1.4;
      box-shadow: 
        0 4px 8px rgba(0, 0, 0, 0.2),
        inset 0 1px 0 rgba(255, 255, 255, 0.3),
        inset 0 -1px 0 rgba(0, 0, 0, 0.1);
      border: 1px solid rgba(0, 0, 0, 0.1);
      transition: all 0.3s ease;
      position: relative;
      overflow: hidden;
    }
    
    .profile-card::before {
      content: '';
      position: absolute;
      top: 0;
      left: 0;
      right: 0;
      height: 1px;
      background: linear-gradient(90deg, transparent, rgba(255, 255, 255, 0.3), transparent);
    }
    
    .profile-card:hover {
      transform: translateY(-2px);
      box-shadow: 
        0 6px 12px rgba(0, 0, 0, 0.4),
        inset 0 1px 0 rgba(255, 255, 255, 0.3),
        inset 0 -1px 0 rgba(0, 0, 0, 0.3);
    }
    
           .profile-text {
             color: #333;
             position: relative;
             z-index: 1;
             line-height: 1.3;
           }
           
           .profile-text strong {
             display: inline;
             margin-right: 8px;
             text-shadow: none;
           }
           
           .profile-text span {
             display: inline;
             margin: 0;
           }
    
    .contact-links {
      font-size: 11px;
    }
    
    .contact-links a {
      color: #0066cc;
      text-decoration: none;
      margin: 0 2px;
      transition: color 0.2s ease;
    }
    
    .contact-links a:hover {
      color: #004499;
      text-decoration: underline;
    }
    
    .language-selector {
      flex: 0 0 auto;
    }
    
    .language-field {
      width: 140px;
      margin: 0;
      background: linear-gradient(145deg, #d3d3d3, #c0c0c0);
      border-radius: 8px;
      box-shadow: 
        0 4px 8px rgba(0, 0, 0, 0.2),
        inset 0 1px 0 rgba(255, 255, 255, 0.3),
        inset 0 -1px 0 rgba(0, 0, 0, 0.1);
      border: 1px solid rgba(0, 0, 0, 0.1);
      transition: all 0.3s ease;
      overflow: hidden;
    }
    
    .language-field:hover {
      transform: translateY(-2px);
      box-shadow: 
        0 6px 12px rgba(0, 0, 0, 0.4),
        inset 0 1px 0 rgba(255, 255, 255, 0.3),
        inset 0 -1px 0 rgba(0, 0, 0, 0.3);
    }
    
    .language-field ::ng-deep .mat-mdc-form-field-wrapper {
      background: transparent;
    }
    
    .language-field ::ng-deep .mat-mdc-text-field-wrapper {
      background: transparent;
    }
    
    .language-field ::ng-deep .mat-mdc-form-field-flex {
      background: transparent;
    }
    
    .language-field ::ng-deep .mat-mdc-select-value,
    .language-field ::ng-deep .mat-mdc-select-trigger {
      color: #333 !important;
    }
    
    .language-field ::ng-deep .mat-mdc-form-field-label {
      color: rgba(0, 0, 0, 0.6) !important;
    }
    
    .language-field ::ng-deep .mat-mdc-form-field-focus-overlay {
      background-color: rgba(0, 0, 0, 0.05);
    }
    
    .language-field ::ng-deep .mdc-line-ripple::before {
      border-bottom-color: rgba(0, 0, 0, 0.2) !important;
    }
    
    .language-field ::ng-deep .mdc-line-ripple::after {
      border-bottom-color: rgba(0, 0, 0, 0.4) !important;
    }
    
    .language-field ::ng-deep .mat-mdc-form-field-subscript-wrapper {
      display: none;
    }
    
    .language-field ::ng-deep .mat-mdc-text-field-wrapper {
      padding-bottom: 0;
    }
    
    .language-field ::ng-deep .mat-mdc-form-field-infix {
      padding-top: 8px;
      padding-bottom: 8px;
      min-height: 40px;
    }
    
    main {
      flex: 1;
      overflow: auto;
    }
    
    @media (max-width: 768px) {
      .app-toolbar {
        padding: 0 8px;
      }
      
      .app-title {
        font-size: 16px;
        white-space: normal;
        flex: 1;
        text-align: left;
      }
      
      .language-field {
        width: 120px;
      }
    }
  `]
})
export class AppComponent implements OnInit {
  title = 'interface-configuration';
  currentLanguage: Language = 'de';
  availableLanguages: Language[] = [];
  private dialog = inject(MatDialog);

  constructor(private translationService: TranslationService) {}

  ngOnInit(): void {
    this.availableLanguages = this.translationService.getAvailableLanguages();
    this.currentLanguage = this.translationService.getCurrentLanguageValue();
  }

  getTranslation(key: string): string {
    return this.translationService.translate(key);
  }

  getLanguageName(lang: Language): string {
    return this.translationService.getLanguageName(lang);
  }

  onLanguageChange(language: Language): void {
    this.translationService.setLanguage(language);
  }

  openDocumentation(): void {
    this.dialog.open(DocumentationDialogComponent, {
      width: '90%',
      maxWidth: '1000px',
      maxHeight: '90vh',
      panelClass: 'documentation-dialog'
    });
  }
}


