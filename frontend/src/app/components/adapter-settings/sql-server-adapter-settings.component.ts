import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { BaseAdapterSettingsComponent } from './base-adapter-settings.component';

@Component({
  selector: 'app-sql-server-adapter-settings',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSlideToggleModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule
  ],
  templateUrl: './sql-server-adapter-settings.component.html',
  styleUrl: './sql-server-adapter-settings.component.css'
})
export class SqlServerAdapterSettingsComponent extends BaseAdapterSettingsComponent implements OnInit {
  // SQL Server Properties
  sqlServerName: string = '';
  sqlDatabaseName: string = '';
  sqlUserName: string = '';
  sqlPassword: string = '';
  sqlIntegratedSecurity: boolean = false;
  sqlResourceGroup: string = '';
  sqlPollingStatement: string = '';
  sqlPollingInterval: number = 60;
  sqlUseTransaction: boolean = false;
  sqlBatchSize: number = 1000;
  tableName: string = 'TransportData';
  connectionString: string = '';

  ngOnInit(): void {
    // Initialize will be called by parent component
  }

  override initializeSettings(data: any): void {
    this.sqlServerName = data.sqlServerName || '';
    this.sqlDatabaseName = data.sqlDatabaseName || '';
    this.sqlUserName = data.sqlUserName || '';
    this.sqlPassword = data.sqlPassword || '';
    this.sqlIntegratedSecurity = data.sqlIntegratedSecurity ?? false;
    this.sqlResourceGroup = data.sqlResourceGroup || '';
    this.sqlPollingStatement = data.sqlPollingStatement || '';
    this.sqlPollingInterval = data.sqlPollingInterval ?? 60;
    this.sqlUseTransaction = data.sqlUseTransaction ?? false;
    this.sqlBatchSize = data.sqlBatchSize ?? 1000;
    this.tableName = data.tableName || 'TransportData';
    this.updateConnectionString();
  }

  override getSettings(): any {
    return {
      sqlServerName: this.sqlServerName.trim() || '',
      sqlDatabaseName: this.sqlDatabaseName.trim() || '',
      sqlUserName: this.sqlUserName.trim() || '',
      sqlPassword: this.sqlPassword.trim() || '',
      sqlIntegratedSecurity: this.sqlIntegratedSecurity,
      sqlResourceGroup: this.sqlResourceGroup.trim() || '',
      sqlPollingStatement: this.adapterType === 'Source' ? (this.sqlPollingStatement.trim() || '') : undefined,
      sqlPollingInterval: this.adapterType === 'Source' ? (this.sqlPollingInterval > 0 ? this.sqlPollingInterval : 60) : undefined,
      sqlUseTransaction: this.sqlUseTransaction,
      sqlBatchSize: this.sqlBatchSize > 0 ? this.sqlBatchSize : 1000,
      tableName: this.tableName.trim() || 'TransportData'
    };
  }

  updateConnectionString(): void {
    const parts: string[] = [];
    
    if (this.sqlServerName) {
      if (this.sqlServerName.includes('.database.windows.net')) {
        parts.push(`Server=tcp:${this.sqlServerName},1433`);
      } else {
        parts.push(`Server=${this.sqlServerName},1433`);
      }
    }
    
    if (this.sqlDatabaseName) {
      parts.push(`Initial Catalog=${this.sqlDatabaseName}`);
    }
    
    if (this.sqlIntegratedSecurity) {
      parts.push('Integrated Security=True');
    } else {
      if (this.sqlUserName) {
        parts.push(`User ID=${this.sqlUserName}`);
      }
      if (this.sqlPassword) {
        parts.push(`Password=${this.sqlPassword}`);
      }
    }
    
    if (this.sqlServerName?.includes('.database.windows.net')) {
      parts.push('Persist Security Info=False');
      parts.push('MultipleActiveResultSets=False');
      parts.push('Encrypt=True');
      parts.push('TrustServerCertificate=False');
      parts.push('Connection Timeout=30');
    } else {
      parts.push('Persist Security Info=False');
      parts.push('MultipleActiveResultSets=True');
      parts.push('Encrypt=False');
      parts.push('Connection Timeout=30');
    }
    
    this.connectionString = parts.length > 0 ? parts.join(';') + ';' : '';
    this.emitSettingsChange();
  }

  onSqlPropertyChange(): void {
    this.updateConnectionString();
  }

  copyConnectionString(): void {
    if (this.connectionString) {
      navigator.clipboard.writeText(this.connectionString).then(() => {
        alert('Connection string copied to clipboard!');
      }).catch(err => {
        console.error('Failed to copy connection string:', err);
        alert('Failed to copy connection string');
      });
    }
  }
}

