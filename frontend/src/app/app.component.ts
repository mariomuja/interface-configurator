import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { DocumentationComponent } from './components/documentation/documentation.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    RouterOutlet,
    MatToolbarModule,
    DocumentationComponent
  ],
  template: `
    <mat-toolbar color="primary" class="app-toolbar">
      <div class="toolbar-content">
        <span class="app-title">CSV to SQL Server Transport</span>
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
      justify-content: center;
      width: 100%;
      padding: 8px 0;
    }
    
    .app-title {
      font-size: 18px;
      font-weight: 500;
      white-space: nowrap;
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
      }
    }
  `]
})
export class AppComponent {
  title = 'infrastructure-as-code';
}


