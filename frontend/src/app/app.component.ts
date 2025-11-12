import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { CommonModule } from '@angular/common';
import { DocumentationComponent } from './components/documentation/documentation.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    RouterOutlet,
    MatToolbarModule,
    MatCardModule,
    MatIconModule,
    CommonModule,
    DocumentationComponent
  ],
  template: `
    <mat-toolbar color="primary" class="app-toolbar">
      <div class="toolbar-content">
        <span class="app-title">Infrastructure as Code - CSV to SQL Server Transport</span>
        <mat-card class="profile-card">
          <div class="profile-content">
            <p class="profile-text">
              Hi, I am <strong>Mario Muja</strong>. I live in Hamburg. This is an application that I have created in my spare free time. 
              If you need support with Angular/TypeScript development or with this app, then you can contact me on my German phone number 
              <a href="tel:+4915204641473">+49 1520 464 1473</a> or at 
              <a href="mailto:mario.muja&#64;gmail.com">mario.muja&#64;gmail.com</a>. 
              I am looking forward to hearing from you! Have a nice day.
            </p>
            <a href="https://github.com/mariomuja" target="_blank" rel="noopener noreferrer" class="github-link">
              <mat-icon>code</mat-icon>
              <span>View on GitHub</span>
            </a>
          </div>
        </mat-card>
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
      align-items: center;
      justify-content: space-between;
      width: 100%;
      gap: 16px;
      padding: 8px 0;
    }
    
    .app-title {
      font-size: 18px;
      font-weight: 500;
      flex: 1;
      white-space: nowrap;
    }
    
    .profile-card {
      flex: 0 0 auto;
      max-width: 420px;
      background: rgba(255, 255, 255, 0.15);
      backdrop-filter: blur(10px);
      border-radius: 12px;
      padding: 20px;
      border: 1px solid rgba(255, 255, 255, 0.2);
      transition: all 0.3s ease;
      box-shadow: none;
      margin: 0;
    }
    
    .profile-card:hover {
      background: rgba(255, 255, 255, 0.2);
      border-color: rgba(255, 255, 255, 0.3);
      transform: translateY(-2px);
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
    }
    
    .profile-content {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    
    .profile-text {
      margin: 0 0 12px 0;
      font-size: 13px;
      line-height: 1.6;
      color: white;
    }
    
    .profile-text strong {
      font-weight: 600;
    }
    
    .profile-text a {
      color: #ffd700;
      text-decoration: none;
      font-weight: 500;
      transition: all 0.2s ease;
    }
    
    .profile-text a:hover {
      color: #ffed4e;
      text-decoration: underline;
    }
    
    .github-link {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      padding: 8px 16px;
      background: rgba(255, 255, 255, 0.2);
      color: white;
      text-decoration: none;
      border-radius: 6px;
      font-size: 13px;
      font-weight: 500;
      transition: all 0.2s ease;
      align-self: flex-start;
      border: 1px solid rgba(255, 255, 255, 0.3);
    }
    
    .github-link mat-icon {
      font-size: 20px;
      width: 20px;
      height: 20px;
    }
    
    .github-link:hover {
      background: rgba(255, 255, 255, 0.3);
      transform: translateY(-2px);
      border-color: rgba(255, 255, 255, 0.5);
    }
    
    main {
      flex: 1;
      overflow: auto;
    }
    
    @media (max-width: 1200px) {
      .toolbar-content {
        flex-direction: column;
        align-items: flex-start;
        gap: 12px;
      }
      
      .profile-card {
        max-width: 100%;
        width: 100%;
      }
      
      .app-title {
        white-space: normal;
      }
    }
    
    @media (max-width: 768px) {
      .app-toolbar {
        padding: 0 8px;
      }
      
      .app-title {
        font-size: 16px;
      }
      
      .profile-text {
        font-size: 12px;
      }
    }
  `]
})
export class AppComponent {
  title = 'infrastructure-as-code';
}


