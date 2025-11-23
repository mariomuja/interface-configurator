import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { AsyncPipe } from '@angular/common';
import { DocumentationComponent } from './components/documentation/documentation.component';
import { DocumentationDialogComponent } from './components/documentation/documentation-dialog.component';
import { TranslationService, Language } from './services/translation.service';
import { VersionService } from './services/version.service';
import { AuthService } from './services/auth.service';
import { FeaturesDialogComponent } from './components/features/features-dialog.component';
import { LoginDialogComponent } from './components/login/login-dialog.component';
import { Observable } from 'rxjs';

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
    AsyncPipe,
    DocumentationComponent
  ],
  template: `
    <mat-toolbar color="primary" class="app-toolbar">
      <div class="toolbar-content">
        <div class="app-title-section">
          <div class="app-title-row">
            <span class="app-title">{{ getTranslation('app.title') }}</span>
            <span class="app-version" *ngIf="versionString$ | async as version">{{ version }}</span>
          </div>
          <p class="app-description">There's much more happening here behind this simple example. 4 weeks to implement a new data transport? Eliminate the need to IMPLEMENT interfaces. Instead CONFIGURE them. Same codebase and quality for all interfaces.</p>
          <div class="concept-banner">
            <div class="concept-frame">
              <div class="concept-text-wrapper">
                <span class="concept-text">This application demonstrates the concept <strong class="concept-name">{{ currentConcept }}</strong>, which means {{ getConceptDescription(currentConcept) }}</span>
              </div>
            </div>
          </div>
        </div>
        <div class="toolbar-right">
          <button mat-button class="features-link" (click)="openFeatures()" *ngIf="authService.isAuthenticated()">
            <mat-icon>featured_play_list</mat-icon>
            Features
          </button>
          <button mat-button class="login-link" (click)="openLogin()" *ngIf="!authService.isAuthenticated()">
            <mat-icon>login</mat-icon>
            Anmelden
          </button>
          <button mat-button class="logout-link" (click)="logout()" *ngIf="authService.isAuthenticated()">
            <mat-icon>logout</mat-icon>
            Abmelden ({{ authService.getCurrentUser()?.username }})
          </button>
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
      padding: 0 12px;
      height: auto;
      min-height: 48px;
    }
    
    .toolbar-content {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      width: 100%;
      padding: 6px 0;
      gap: 12px;
    }
    
    .app-title-section {
      flex: 1 1 auto;
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      min-width: 0;
      margin-right: 12px;
    }
    
    .app-title-row {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-bottom: 2px;
    }
    
    .app-title {
      font-size: 14px;
      font-weight: 500;
      white-space: nowrap;
      margin: 0;
      text-align: left;
    }
    
    .app-version {
      font-size: 11px;
      font-weight: 400;
      color: rgba(255, 255, 255, 0.7);
      background-color: rgba(255, 255, 255, 0.1);
      padding: 2px 8px;
      border-radius: 4px;
      white-space: nowrap;
    }
    
    .app-description {
      font-size: 10px;
      line-height: 1.3;
      color: rgba(255, 255, 255, 0.9);
      margin: 0 0 4px 0;
      text-align: left;
      width: 100%;
      word-wrap: break-word;
      overflow-wrap: break-word;
      white-space: normal;
    }
    
    .concept-banner {
      width: 100%;
      margin-top: 4px;
      overflow: hidden;
    }
    
    .concept-frame {
      background: linear-gradient(135deg, rgba(255, 255, 255, 0.1), rgba(255, 255, 255, 0.05));
      border: 2px solid rgba(255, 255, 255, 0.3);
      border-radius: 6px;
      padding: 6px 12px;
      position: relative;
      overflow: hidden;
      box-shadow: 
        0 2px 8px rgba(0, 0, 0, 0.2),
        inset 0 1px 0 rgba(255, 255, 255, 0.2),
        inset 0 -1px 0 rgba(0, 0, 0, 0.1);
      transform-style: preserve-3d;
      perspective: 1000px;
      animation: frameGlow 3s ease-in-out infinite;
    }
    
    @keyframes frameGlow {
      0%, 100% {
        box-shadow: 
          0 2px 8px rgba(0, 0, 0, 0.2),
          inset 0 1px 0 rgba(255, 255, 255, 0.2),
          inset 0 -1px 0 rgba(0, 0, 0, 0.1),
          0 0 10px rgba(255, 255, 255, 0.1);
      }
      50% {
        box-shadow: 
          0 2px 8px rgba(0, 0, 0, 0.2),
          inset 0 1px 0 rgba(255, 255, 255, 0.3),
          inset 0 -1px 0 rgba(0, 0, 0, 0.1),
          0 0 20px rgba(255, 255, 255, 0.2);
      }
    }
    
    .concept-frame::before {
      content: '';
      position: absolute;
      top: 0;
      left: -100%;
      width: 100%;
      height: 100%;
      background: linear-gradient(90deg, transparent, rgba(255, 255, 255, 0.2), transparent);
      animation: shimmer 3s infinite;
    }
    
    @keyframes shimmer {
      0% {
        left: -100%;
      }
      100% {
        left: 100%;
      }
    }
    
    .concept-text-wrapper {
      position: relative;
      overflow: hidden;
      white-space: nowrap;
    }
    
    .concept-text {
      font-size: 10px;
      line-height: 1.4;
      color: rgba(255, 255, 255, 0.95);
      display: inline-block;
      animation: textScroll 0.5s ease-in-out;
      text-shadow: 0 1px 2px rgba(0, 0, 0, 0.3);
    }
    
    @keyframes textScroll {
      0% {
        opacity: 0;
        transform: translateX(-10px);
      }
      100% {
        opacity: 1;
        transform: translateX(0);
      }
    }
    
    .concept-name {
      color: #FFD700;
      font-weight: 600;
      text-shadow: 
        0 0 5px rgba(255, 215, 0, 0.5),
        0 1px 2px rgba(0, 0, 0, 0.5);
      animation: conceptPulse 2s ease-in-out infinite;
    }
    
    @keyframes conceptPulse {
      0%, 100% {
        text-shadow: 
          0 0 5px rgba(255, 215, 0, 0.5),
          0 1px 2px rgba(0, 0, 0, 0.5);
      }
      50% {
        text-shadow: 
          0 0 10px rgba(255, 215, 0, 0.8),
          0 0 15px rgba(255, 215, 0, 0.4),
          0 1px 2px rgba(0, 0, 0, 0.5);
      }
    }
    
    .toolbar-right {
      display: flex;
      align-items: center;
      gap: 16px;
      margin-left: auto;
    }
    
    .features-link, .documentation-link, .login-link, .logout-link {
      color: rgba(255, 255, 255, 0.9);
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 14px;
      transition: color 0.2s ease;
    }
    
    .features-link:hover, .documentation-link:hover, .login-link:hover, .logout-link:hover {
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
      padding-top: 4px;
      padding-bottom: 4px;
      min-height: 32px;
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
export class AppComponent implements OnInit, OnDestroy {
  title = 'interface-configuration';
  currentLanguage: Language = 'de';
  availableLanguages: Language[] = [];
  versionString$: Observable<string>;
  private dialog = inject(MatDialog);
  
  // Concepts that this application demonstrates
  concepts: string[] = [
    'Configuration over Implementation',
    'Event-Driven Architecture',
    'Dynamic Schema Management',
    'Row-Level Error Handling',
    'Infrastructure as Code',
    'Multi-Platform Architecture',
    'Internationalization',
    'Data Quality & Validation',
    'Adapter Pattern',
    'MessageBox Pattern',
    'Guaranteed Delivery',
    'Debatching',
    'Subscription Pattern',
    'Factory Pattern',
    'Dependency Injection',
    'Clean Architecture',
    'Pluggable Architecture',
    'Universal Adapters',
    'Feature Management',
    'Role-Based Access Control',
    'Feature Toggles',
    'Separation of Concerns',
    'Single Responsibility Principle',
    'Open/Closed Principle',
    'Interface Segregation',
    'Dependency Inversion',
    'SOLID Principles',
    'Repository Pattern',
    'Service Layer Pattern',
    'Unit of Work Pattern',
    'Strategy Pattern',
    'Observer Pattern',
    'Publish-Subscribe Pattern',
    'Staging Area Pattern',
    'Idempotency',
    'Retry Logic',
    'Circuit Breaker Pattern',
    'Dead Letter Queue',
    'Schema Evolution',
    'Type Inference',
    'Dynamic Column Creation',
    'Asynchronous Processing',
    'Non-Blocking Operations',
    'Concurrent Processing',
    'Message Locking',
    'Transaction Management',
    'Error Isolation',
    'Audit Trail',
    'Processing Statistics',
    'Comprehensive Logging'
  ];
  
  currentConceptIndex = 0;
  currentConcept = this.concepts[0];
  private conceptInterval: any;

  constructor(
    private translationService: TranslationService,
    private versionService: VersionService,
    public authService: AuthService
  ) {
    this.versionString$ = this.versionService.getVersionString();
  }

  ngOnInit(): void {
    this.availableLanguages = this.translationService.getAvailableLanguages();
    this.currentLanguage = this.translationService.getCurrentLanguageValue();
    this.startConceptRotation();
  }
  
  ngOnDestroy(): void {
    if (this.conceptInterval) {
      clearInterval(this.conceptInterval);
    }
  }
  
  startConceptRotation(): void {
    // Rotate through concepts every 4 seconds
    this.conceptInterval = setInterval(() => {
      this.currentConceptIndex = (this.currentConceptIndex + 1) % this.concepts.length;
      this.currentConcept = this.concepts[this.currentConceptIndex];
    }, 4000);
  }
  
  getConceptDescription(concept: string): string {
    const descriptions: { [key: string]: string } = {
      'Configuration over Implementation': 'defining interfaces by configuration rather than writing custom code for each integration',
      'Event-Driven Architecture': 'using events to trigger processing and enable loose coupling between components',
      'Dynamic Schema Management': 'automatically adapting database schemas to match incoming data structures',
      'Row-Level Error Handling': 'isolating and preserving failed rows for reprocessing while successfully processing others',
      'Infrastructure as Code': 'defining and managing infrastructure through code for reproducible deployments',
      'Multi-Platform Architecture': 'deploying components across different platforms (Vercel, Azure) for optimal performance',
      'Internationalization': 'supporting multiple languages and regional formats for global accessibility',
      'Data Quality & Validation': 'ensuring data integrity through automatic type detection and validation',
      'Adapter Pattern': 'abstracting data source and destination differences through a unified interface',
      'MessageBox Pattern': 'using a central staging area to ensure guaranteed delivery of messages',
      'Guaranteed Delivery': 'ensuring messages are not lost until all destinations confirm successful processing',
      'Debatching': 'splitting batch data into individual messages for independent processing',
      'Subscription Pattern': 'defining filter criteria for which messages adapters receive from the MessageBox',
      'Factory Pattern': 'centralizing object creation logic to enable dynamic service selection',
      'Dependency Injection': 'injecting dependencies to achieve loose coupling and testability',
      'Clean Architecture': 'organizing code into layers with clear separation of concerns',
      'Pluggable Architecture': 'enabling components to be swapped without changing core logic',
      'Universal Adapters': 'adapters that can function as both source and destination',
      'Feature Management': 'controlling feature visibility through toggles and gradual rollouts',
      'Role-Based Access Control': 'managing user permissions through roles and access levels',
      'Feature Toggles': 'enabling features to be turned on/off without code deployment',
      'Separation of Concerns': 'dividing application logic into distinct sections with single responsibilities',
      'Single Responsibility Principle': 'ensuring each class or module has only one reason to change',
      'Open/Closed Principle': 'designing classes to be open for extension but closed for modification',
      'Interface Segregation': 'creating specific interfaces rather than general-purpose ones',
      'Dependency Inversion': 'depending on abstractions rather than concrete implementations',
      'SOLID Principles': 'following five design principles for maintainable and scalable code',
      'Repository Pattern': 'abstracting data access logic behind a repository interface',
      'Service Layer Pattern': 'organizing business logic into service classes',
      'Unit of Work Pattern': 'managing transactions and tracking changes to data',
      'Strategy Pattern': 'defining a family of algorithms and making them interchangeable',
      'Observer Pattern': 'notifying multiple objects about state changes',
      'Publish-Subscribe Pattern': 'decoupling message producers from consumers through a message broker',
      'Staging Area Pattern': 'using an intermediate storage area for reliable message processing',
      'Idempotency': 'ensuring operations can be safely repeated without side effects',
      'Retry Logic': 'automatically retrying failed operations with exponential backoff',
      'Circuit Breaker Pattern': 'preventing cascading failures by stopping requests when a service is down',
      'Dead Letter Queue': 'storing messages that cannot be processed after maximum retries',
      'Schema Evolution': 'handling changes to data structures without manual migrations',
      'Type Inference': 'automatically detecting data types from values',
      'Dynamic Column Creation': 'creating database columns automatically based on incoming data',
      'Asynchronous Processing': 'processing operations without blocking the main thread',
      'Non-Blocking Operations': 'allowing other operations to continue while waiting for results',
      'Concurrent Processing': 'handling multiple operations simultaneously',
      'Message Locking': 'preventing concurrent processing of the same message',
      'Transaction Management': 'ensuring data consistency through atomic operations',
      'Error Isolation': 'preventing errors in one component from affecting others',
      'Audit Trail': 'maintaining a complete history of all operations and changes',
      'Processing Statistics': 'tracking performance metrics and processing times',
      'Comprehensive Logging': 'recording detailed information for debugging and monitoring'
    };
    
    return descriptions[concept] || 'a key architectural or coding concept demonstrated in this application';
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

  openFeatures(): void {
    if (!this.authService.isAuthenticated()) {
      this.openLogin();
      return;
    }
    
    this.dialog.open(FeaturesDialogComponent, {
      width: '90%',
      maxWidth: '1200px',
      maxHeight: '90vh',
      panelClass: 'features-dialog'
    });
  }

  openLogin(): void {
    const dialogRef = this.dialog.open(LoginDialogComponent, {
      width: '400px',
      disableClose: false
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        // Login successful, features might be available now
      }
    });
  }

  logout(): void {
    this.authService.logout();
  }
}


