import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { BaseAdapterSettingsComponent } from './base-adapter-settings.component';

@Component({
  selector: 'app-csv-adapter-settings',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatTooltipModule
  ],
  templateUrl: './csv-adapter-settings.component.html',
  styleUrl: './csv-adapter-settings.component.css'
})
export class CsvAdapterSettingsComponent extends BaseAdapterSettingsComponent implements OnInit {
  // CSV Properties
  receiveFolder: string = '';
  fileMask: string = '*.txt';
  batchSize: number = 1000;
  fieldSeparator: string = '║';
  destinationReceiveFolder: string = '';
  destinationFileMask: string = '*.txt';
  csvAdapterType: string = 'RAW';
  csvData: string = '';
  csvPollingInterval: number = 10;
  
  // SFTP Properties
  sftpHost: string = '';
  sftpPort: number = 22;
  sftpUsername: string = '';
  sftpPassword: string = '';
  sftpSshKey: string = '';
  sftpFolder: string = '';
  sftpFileMask: string = '*.txt';
  sftpMaxConnectionPoolSize: number = 5;
  sftpFileBufferSize: number = 8192;

  ngOnInit(): void {
    // Initialize will be called by parent component
  }

  override initializeSettings(data: any): void {
    this.receiveFolder = data.receiveFolder || '';
    this.fileMask = data.fileMask || '*.txt';
    this.batchSize = data.batchSize ?? 1000;
    this.fieldSeparator = data.fieldSeparator || '║';
    this.destinationReceiveFolder = data.destinationReceiveFolder || '';
    this.destinationFileMask = data.destinationFileMask || '*.txt';
    
    const fallbackType = this.adapterType === 'Destination' ? 'FILE' : 'RAW';
    let selectedType = (data.csvAdapterType || fallbackType).toUpperCase();
    if (this.adapterType === 'Destination' && selectedType === 'RAW') {
      selectedType = 'FILE';
    }
    this.csvAdapterType = selectedType;
    
    this.csvData = data.csvData || '';
    this.csvPollingInterval = data.csvPollingInterval ?? 10;
    
    // SFTP properties
    this.sftpHost = data.sftpHost || '';
    this.sftpPort = data.sftpPort ?? 22;
    this.sftpUsername = data.sftpUsername || '';
    this.sftpPassword = data.sftpPassword || '';
    this.sftpSshKey = data.sftpSshKey || '';
    this.sftpFolder = data.sftpFolder || '';
    this.sftpFileMask = data.sftpFileMask || '*.txt';
    this.sftpMaxConnectionPoolSize = data.sftpMaxConnectionPoolSize ?? 5;
    this.sftpFileBufferSize = data.sftpFileBufferSize ?? 8192;
  }

  override getSettings(): any {
    return {
      receiveFolder: this.receiveFolder.trim() || '',
      fileMask: this.fileMask.trim() || '*.txt',
      batchSize: this.batchSize > 0 ? this.batchSize : 1000,
      fieldSeparator: this.fieldSeparator.trim() || '║',
      destinationReceiveFolder: this.adapterType === 'Destination' ? (this.destinationReceiveFolder.trim() || '') : undefined,
      destinationFileMask: this.adapterType === 'Destination' ? (this.destinationFileMask.trim() || '*.txt') : undefined,
      csvAdapterType: this.csvAdapterType || (this.adapterType === 'Destination' ? 'FILE' : 'RAW'),
      csvData: this.showRawProperties ? (this.csvData.trim() || '') : undefined,
      csvPollingInterval: this.adapterType === 'Source' ? (this.csvPollingInterval > 0 ? this.csvPollingInterval : 10) : undefined,
      // SFTP properties
      sftpHost: this.showSftpProperties ? (this.sftpHost.trim() || '') : undefined,
      sftpPort: this.showSftpProperties ? (this.sftpPort > 0 ? this.sftpPort : 22) : undefined,
      sftpUsername: this.showSftpProperties ? (this.sftpUsername.trim() || '') : undefined,
      sftpPassword: this.showSftpProperties ? (this.sftpPassword.trim() || '') : undefined,
      sftpSshKey: this.showSftpProperties ? (this.sftpSshKey.trim() || '') : undefined,
      sftpFolder: this.showSftpProperties ? (this.sftpFolder.trim() || '') : undefined,
      sftpFileMask: this.showSftpProperties ? (this.sftpFileMask.trim() || '*.txt') : undefined,
      sftpMaxConnectionPoolSize: this.showSftpProperties ? (this.sftpMaxConnectionPoolSize > 0 ? this.sftpMaxConnectionPoolSize : 5) : undefined,
      sftpFileBufferSize: this.showSftpProperties ? (this.sftpFileBufferSize > 0 ? this.sftpFileBufferSize : 8192) : undefined
    };
  }

  get showReceiveFolder(): boolean {
    return this.adapterType === 'Source' && this.csvAdapterType === 'FILE';
  }

  get showFileMask(): boolean {
    return this.adapterType === 'Source' && this.csvAdapterType === 'FILE';
  }

  get showDestinationProperties(): boolean {
    return this.adapterType === 'Destination' && this.csvAdapterType === 'FILE';
  }

  get showSftpProperties(): boolean {
    return this.csvAdapterType === 'SFTP';
  }

  get showRawProperties(): boolean {
    return this.adapterType === 'Source' && this.csvAdapterType === 'RAW';
  }

  get showFileProperties(): boolean {
    return this.adapterType === 'Source' && this.csvAdapterType === 'FILE';
  }
}

