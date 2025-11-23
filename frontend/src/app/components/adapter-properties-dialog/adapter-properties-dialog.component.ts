import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

export interface AdapterPropertiesData {
  adapterType: 'Source' | 'Destination';
  adapterName: 'CSV' | 'SqlServer';
  instanceName: string;
  isEnabled: boolean;
  receiveFolder?: string; // Only for CSV adapters
  fileMask?: string; // Only for CSV adapters
  batchSize?: number; // Only for CSV adapters
  fieldSeparator?: string; // Only for CSV adapters
  destinationReceiveFolder?: string; // Only for CSV adapters when used as destination
  destinationFileMask?: string; // Only for CSV adapters when used as destination
  // SFTP properties (only for CSV Source adapters)
  csvAdapterType?: string; // "RAW", "FILE", or "SFTP"
  csvData?: string; // CSV data content for RAW adapter type
  sftpHost?: string;
  sftpPort?: number;
  sftpUsername?: string;
  sftpPassword?: string;
  sftpSshKey?: string;
  sftpFolder?: string;
  sftpFileMask?: string;
  sftpMaxConnectionPoolSize?: number;
  sftpFileBufferSize?: number;
  csvPollingInterval?: number; // Polling interval for CSV adapters
  // SQL Server properties
  sqlServerName?: string;
  sqlDatabaseName?: string;
  sqlUserName?: string;
  sqlPassword?: string;
  sqlIntegratedSecurity?: boolean;
  sqlResourceGroup?: string;
  sqlPollingStatement?: string; // Only for Source adapters
  sqlPollingInterval?: number; // Only for Source adapters
  sqlUseTransaction?: boolean; // SQL Server adapter property
  sqlBatchSize?: number; // SQL Server adapter property
  tableName?: string; // Table name for SqlServer adapters (used for both source and destination)
  adapterInstanceGuid: string;
  // JQ Transformation properties (only for Destination adapters)
  jqScriptFile?: string; // URI to jq script file for JSON transformation
  sourceAdapterSubscription?: string; // GUID of source adapter to subscribe to
  // SQL Server custom statements (only for Destination adapters)
  insertStatement?: string; // Custom INSERT statement using OPENJSON
  updateStatement?: string; // Custom UPDATE statement using OPENJSON
  deleteStatement?: string; // Custom DELETE statement using OPENJSON
  // Available source adapters for subscription selection
  availableSourceAdapters?: Array<{ guid: string; name: string; adapterName: string }>;
}

@Component({
  selector: 'app-adapter-properties-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule
  ],
  templateUrl: './adapter-properties-dialog.component.html',
  styleUrl: './adapter-properties-dialog.component.css'
})
export class AdapterPropertiesDialogComponent implements OnInit {
  instanceName: string = '';
  isEnabled: boolean = true;
  receiveFolder: string = '';
  fileMask: string = '*.txt';
  batchSize: number = 1000; // Increased default batch size for better performance
  fieldSeparator: string = '║';
  destinationReceiveFolder: string = '';
  destinationFileMask: string = '*.txt';
  // SFTP properties
  csvAdapterType: string = 'RAW'; // Default to RAW for our example
  csvData: string = ''; // CSV data content for RAW adapter type
  sftpHost: string = '';
  sftpPort: number = 22;
  sftpUsername: string = '';
  sftpPassword: string = '';
  sftpSshKey: string = '';
  sftpFolder: string = '';
  sftpFileMask: string = '*.txt';
  sftpMaxConnectionPoolSize: number = 5;
  sftpFileBufferSize: number = 8192;
  csvPollingInterval: number = 10;
  // SQL Server properties
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
  tableName: string = 'TransportData'; // Table name for SqlServer destination adapters
  connectionString: string = '';
  adapterInstanceGuid: string = '';
  // JQ Transformation properties (only for Destination adapters)
  jqScriptFile: string = '';
  sourceAdapterSubscription: string = '';
  // SQL Server custom statements (only for Destination adapters)
  insertStatement: string = '';
  updateStatement: string = '';
  deleteStatement: string = '';
  // Available source adapters for subscription
  availableSourceAdapters: Array<{ guid: string; name: string; adapterName: string }> = [];

  constructor(
    public dialogRef: MatDialogRef<AdapterPropertiesDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: AdapterPropertiesData
  ) {}

  ngOnInit(): void {
    this.instanceName = this.data.instanceName || '';
    // Use explicit check for undefined - false is a valid value!
    this.isEnabled = this.data.isEnabled !== undefined ? this.data.isEnabled : true;
    this.receiveFolder = this.data.receiveFolder || '';
    this.fileMask = this.data.fileMask || '*.txt';
    this.batchSize = this.data.batchSize ?? 100;
    this.fieldSeparator = this.data.fieldSeparator || '║';
    this.destinationReceiveFolder = this.data.destinationReceiveFolder || '';
    this.destinationFileMask = this.data.destinationFileMask || '*.txt';
    // SFTP / adapter type
    if (this.data.adapterName === 'CSV') {
      const fallbackType = this.data.adapterType === 'Destination' ? 'FILE' : 'RAW';
      let selectedType = (this.data.csvAdapterType || fallbackType).toUpperCase();
      if (this.data.adapterType === 'Destination' && selectedType === 'RAW') {
        selectedType = 'FILE';
      }
      this.csvAdapterType = selectedType;
    }
    this.csvData = this.data.csvData || '';
    this.sftpHost = this.data.sftpHost || '';
    this.sftpPort = this.data.sftpPort ?? 22;
    this.sftpUsername = this.data.sftpUsername || '';
    this.sftpPassword = this.data.sftpPassword || '';
    this.sftpSshKey = this.data.sftpSshKey || '';
    this.sftpFolder = this.data.sftpFolder || '';
    this.sftpFileMask = this.data.sftpFileMask || '*.txt';
    this.sftpMaxConnectionPoolSize = this.data.sftpMaxConnectionPoolSize ?? 5;
    this.sftpFileBufferSize = this.data.sftpFileBufferSize ?? 8192;
    this.csvPollingInterval = this.data.csvPollingInterval ?? 10;
    // SQL Server properties
    this.sqlServerName = this.data.sqlServerName || '';
    this.sqlDatabaseName = this.data.sqlDatabaseName || '';
    this.sqlUserName = this.data.sqlUserName || '';
    this.sqlPassword = this.data.sqlPassword || '';
    this.sqlIntegratedSecurity = this.data.sqlIntegratedSecurity ?? false;
    this.sqlResourceGroup = this.data.sqlResourceGroup || '';
    this.sqlPollingStatement = this.data.sqlPollingStatement || '';
    this.sqlPollingInterval = this.data.sqlPollingInterval ?? 60;
    this.tableName = this.data.tableName || 'TransportData';
    this.adapterInstanceGuid = this.data.adapterInstanceGuid || '';
    // JQ Transformation properties (only for Destination adapters)
    this.jqScriptFile = this.data.jqScriptFile || '';
    this.sourceAdapterSubscription = this.data.sourceAdapterSubscription || '';
    // SQL Server custom statements (only for Destination adapters)
    this.insertStatement = this.data.insertStatement || '';
    this.updateStatement = this.data.updateStatement || '';
    this.deleteStatement = this.data.deleteStatement || '';
    // Available source adapters
    this.availableSourceAdapters = this.data.availableSourceAdapters || [];
    this.updateConnectionString();
  }
  
  onSelectJQScriptFile(): void {
    // Create a file input element
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.jq,.json';
    input.onchange = (event: any) => {
      const file = event.target.files[0];
      if (file) {
        // Convert to file:// URI
        this.jqScriptFile = `file:///${file.path.replace(/\\/g, '/')}`;
      }
    };
    input.click();
  }

  updateConnectionString(): void {
    if (!this.showSqlServerProperties) {
      this.connectionString = '';
      return;
    }

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
  }

  onSqlPropertyChange(): void {
    this.updateConnectionString();
  }

  copyConnectionString(): void {
    if (this.connectionString) {
      navigator.clipboard.writeText(this.connectionString).then(() => {
        // Show success feedback (you might want to use MatSnackBar here)
        alert('Connection string copied to clipboard!');
      }).catch(err => {
        console.error('Failed to copy connection string:', err);
        alert('Failed to copy connection string');
      });
    }
  }

  onCancel(): void {
    this.dialogRef.close();
  }

  onSave(): void {
    this.dialogRef.close({
      instanceName: this.instanceName.trim() || (this.data.adapterType === 'Source' ? 'Source' : 'Destination'),
      isEnabled: this.isEnabled ?? true,
      receiveFolder: this.data.adapterName === 'CSV' ? (this.receiveFolder.trim() || '') : undefined,
      fileMask: this.data.adapterName === 'CSV' ? (this.fileMask.trim() || '*.txt') : undefined,
      batchSize: this.data.adapterName === 'CSV' ? (this.batchSize > 0 ? this.batchSize : 100) : undefined,
      fieldSeparator: this.data.adapterName === 'CSV' ? (this.fieldSeparator.trim() || '║') : undefined,
      destinationReceiveFolder: this.data.adapterName === 'CSV' && this.data.adapterType === 'Destination' ? (this.destinationReceiveFolder.trim() || '') : undefined,
      destinationFileMask: this.data.adapterName === 'CSV' && this.data.adapterType === 'Destination' ? (this.destinationFileMask.trim() || '*.txt') : undefined,
      // SFTP properties (only for CSV Source adapters)
      csvAdapterType: this.data.adapterName === 'CSV' ? (this.csvAdapterType || (this.data.adapterType === 'Destination' ? 'FILE' : 'RAW')) : undefined,
      csvData: this.showRawProperties ? (this.csvData.trim() || '') : undefined,
      sftpHost: this.showSftpProperties && this.csvAdapterType === 'SFTP' ? (this.sftpHost.trim() || '') : undefined,
      sftpPort: this.showSftpProperties && this.csvAdapterType === 'SFTP' ? (this.sftpPort > 0 ? this.sftpPort : 22) : undefined,
      sftpUsername: this.showSftpProperties && this.csvAdapterType === 'SFTP' ? (this.sftpUsername.trim() || '') : undefined,
      sftpPassword: this.showSftpProperties && this.csvAdapterType === 'SFTP' ? (this.sftpPassword.trim() || '') : undefined,
      sftpSshKey: this.showSftpProperties && this.csvAdapterType === 'SFTP' ? (this.sftpSshKey.trim() || '') : undefined,
      sftpFolder: this.showSftpProperties && this.csvAdapterType === 'SFTP' ? (this.sftpFolder.trim() || '') : undefined,
      sftpFileMask: this.showSftpProperties && this.csvAdapterType === 'SFTP' ? (this.sftpFileMask.trim() || '*.txt') : undefined,
      sftpMaxConnectionPoolSize: this.showSftpProperties && this.csvAdapterType === 'SFTP' ? (this.sftpMaxConnectionPoolSize > 0 ? this.sftpMaxConnectionPoolSize : 5) : undefined,
      sftpFileBufferSize: this.showSftpProperties && this.csvAdapterType === 'SFTP' ? (this.sftpFileBufferSize > 0 ? this.sftpFileBufferSize : 8192) : undefined,
          csvPollingInterval: this.data.adapterName === 'CSV' && this.data.adapterType === 'Source' ? (this.csvPollingInterval > 0 ? this.csvPollingInterval : 10) : undefined,
      // SQL Server properties
      sqlServerName: this.data.adapterName === 'SqlServer' ? (this.sqlServerName.trim() || '') : undefined,
      sqlDatabaseName: this.data.adapterName === 'SqlServer' ? (this.sqlDatabaseName.trim() || '') : undefined,
      sqlUserName: this.data.adapterName === 'SqlServer' ? (this.sqlUserName.trim() || '') : undefined,
      sqlPassword: this.data.adapterName === 'SqlServer' ? (this.sqlPassword.trim() || '') : undefined,
      sqlIntegratedSecurity: this.data.adapterName === 'SqlServer' ? this.sqlIntegratedSecurity : undefined,
      sqlResourceGroup: this.data.adapterName === 'SqlServer' ? (this.sqlResourceGroup.trim() || '') : undefined,
      sqlPollingStatement: this.showSqlPollingProperties ? (this.sqlPollingStatement.trim() || '') : undefined,
      sqlPollingInterval: this.showSqlPollingProperties ? (this.sqlPollingInterval > 0 ? this.sqlPollingInterval : 60) : undefined,
      tableName: this.data.adapterName === 'SqlServer' ? (this.tableName.trim() || 'TransportData') : undefined,
      // JQ Transformation properties (only for Destination adapters)
      jqScriptFile: this.data.adapterType === 'Destination' ? (this.jqScriptFile.trim() || '') : undefined,
      sourceAdapterSubscription: this.data.adapterType === 'Destination' ? (this.sourceAdapterSubscription.trim() || '') : undefined,
      // SQL Server custom statements (only for Destination adapters)
      insertStatement: this.data.adapterType === 'Destination' && this.data.adapterName === 'SqlServer' ? (this.insertStatement.trim() || '') : undefined,
      updateStatement: this.data.adapterType === 'Destination' && this.data.adapterName === 'SqlServer' ? (this.updateStatement.trim() || '') : undefined,
      deleteStatement: this.data.adapterType === 'Destination' && this.data.adapterName === 'SqlServer' ? (this.deleteStatement.trim() || '') : undefined
    });
  }
  
  get showJQProperties(): boolean {
    return this.data.adapterType === 'Destination';
  }
  
  get showSourceAdapterSubscription(): boolean {
    return this.data.adapterType === 'Destination' && this.availableSourceAdapters.length > 0;
  }
  
  get showCustomSqlStatements(): boolean {
    return this.data.adapterType === 'Destination' && this.data.adapterName === 'SqlServer';
  }

  get showReceiveFolder(): boolean {
    return this.data.adapterName === 'CSV' && this.showFileProperties;
  }

  get showFileMask(): boolean {
    return this.data.adapterName === 'CSV' && this.showFileProperties;
  }

  get showBatchSize(): boolean {
    return this.data.adapterName === 'CSV';
  }

  get showFieldSeparator(): boolean {
    return this.data.adapterName === 'CSV' && this.data.adapterType === 'Destination';
  }

  get showDestinationProperties(): boolean {
    return this.data.adapterName === 'CSV' && this.data.adapterType === 'Destination' && this.csvAdapterType === 'FILE';
  }

  get showSqlServerProperties(): boolean {
    return this.data.adapterName === 'SqlServer';
  }

  get showSqlPollingProperties(): boolean {
    return this.data.adapterName === 'SqlServer' && this.data.adapterType === 'Source';
  }

  get showSftpProperties(): boolean {
    return this.data.adapterName === 'CSV' && this.csvAdapterType === 'SFTP';
  }

  get showFileProperties(): boolean {
    return this.data.adapterName === 'CSV' && this.data.adapterType === 'Source' && this.csvAdapterType === 'FILE';
  }

  get showRawProperties(): boolean {
    return this.data.adapterName === 'CSV' && this.data.adapterType === 'Source' && this.csvAdapterType === 'RAW';
  }

  get dialogTitle(): string {
    return `${this.data.adapterType} Adapter Properties (${this.data.adapterName})`;
  }
}

