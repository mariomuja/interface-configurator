import { Component, Inject, OnInit, AfterViewInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MatDialog, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CsvAdapterSettingsComponent } from '../adapter-settings/csv-adapter-settings.component';
import { SqlServerAdapterSettingsComponent } from '../adapter-settings/sql-server-adapter-settings.component';
import { SapAdapterSettingsComponent } from '../adapter-settings/sap-adapter-settings.component';
import { Dynamics365AdapterSettingsComponent } from '../adapter-settings/dynamics365-adapter-settings.component';
import { CrmAdapterSettingsComponent } from '../adapter-settings/crm-adapter-settings.component';
import { BaseAdapterSettingsComponent } from '../adapter-settings/base-adapter-settings.component';
import { AdapterWizardComponent } from '../adapter-wizard/adapter-wizard.component';
import { AdapterWizardData } from '../../models/adapter-wizard.model';

export interface AdapterPropertiesData {
  adapterType: 'Source' | 'Destination';
  adapterName: 'CSV' | 'FILE' | 'SFTP' | 'SqlServer' | 'SAP' | 'Dynamics365' | 'CRM';
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
  csvSkipHeaderLines?: number; // Number of lines to skip at the beginning of CSV files
  csvSkipFooterLines?: number; // Number of lines to skip at the end of CSV files
  csvQuoteCharacter?: string; // Quote character used to enclose CSV values
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
    MatTooltipModule,
    CsvAdapterSettingsComponent,
    SqlServerAdapterSettingsComponent,
    SapAdapterSettingsComponent,
    Dynamics365AdapterSettingsComponent,
    CrmAdapterSettingsComponent
  ],
  templateUrl: './adapter-properties-dialog.component.html',
  styleUrl: './adapter-properties-dialog.component.css'
})
export class AdapterPropertiesDialogComponent implements OnInit, AfterViewInit {
  instanceName: string = '';
  isEnabled: boolean = true;
  adapterInstanceGuid: string = '';
  
  // References to adapter-specific settings components
  @ViewChild(CsvAdapterSettingsComponent) csvAdapterSettings?: CsvAdapterSettingsComponent;
  @ViewChild(SqlServerAdapterSettingsComponent) sqlServerAdapterSettings?: SqlServerAdapterSettingsComponent;
  @ViewChild(SapAdapterSettingsComponent) sapAdapterSettings?: SapAdapterSettingsComponent;
  @ViewChild(Dynamics365AdapterSettingsComponent) dynamics365AdapterSettings?: Dynamics365AdapterSettingsComponent;
  @ViewChild(CrmAdapterSettingsComponent) crmAdapterSettings?: CrmAdapterSettingsComponent;
  
  // Adapter-specific settings (stored from event handlers for SAP, Dynamics365, CRM)
  private adapterSettings: any = {};
  
  get adapterSettingsComponent(): BaseAdapterSettingsComponent | undefined {
    return this.csvAdapterSettings || this.sqlServerAdapterSettings || 
           this.sapAdapterSettings || this.dynamics365AdapterSettings || this.crmAdapterSettings;
  }
  
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
    @Inject(MAT_DIALOG_DATA) public data: AdapterPropertiesData,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    this.instanceName = this.data.instanceName || '';
    // Use explicit check for undefined - false is a valid value!
    this.isEnabled = this.data.isEnabled !== undefined ? this.data.isEnabled : true;
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
  }

  ngAfterViewInit(): void {
    // Initialize adapter-specific settings component after view is initialized
    if (this.adapterSettingsComponent) {
      this.adapterSettingsComponent.initializeSettings(this.data);
      this.adapterSettingsComponent.instanceName = this.instanceName;
      this.adapterSettingsComponent.isEnabled = this.isEnabled;
      this.adapterSettingsComponent.adapterInstanceGuid = this.adapterInstanceGuid;
      this.adapterSettingsComponent.adapterType = this.data.adapterType;
    }
    
    // Also initialize SAP, Dynamics365, CRM components if they exist
    if (this.sapAdapterSettings) {
      this.sapAdapterSettings.initializeSettings(this.data);
    }
    if (this.dynamics365AdapterSettings) {
      this.dynamics365AdapterSettings.initializeSettings(this.data);
    }
    if (this.crmAdapterSettings) {
      this.crmAdapterSettings.initializeSettings(this.data);
    }
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

  onSapSettingsChange(settings: any): void {
    this.adapterSettings = { ...this.adapterSettings, ...settings };
  }

  onDynamics365SettingsChange(settings: any): void {
    this.adapterSettings = { ...this.adapterSettings, ...settings };
  }

  onCrmSettingsChange(settings: any): void {
    this.adapterSettings = { ...this.adapterSettings, ...settings };
  }

  onCancel(): void {
    this.dialogRef.close();
  }

  onSave(): void {
    // Get settings from adapter-specific component
    let adapterSettings = this.adapterSettingsComponent?.getSettings() || {};
    
    // Also merge adapter-specific settings from event handlers (for SAP, Dynamics365, CRM)
    if (this.data.adapterName === 'SAP' || this.data.adapterName === 'Dynamics365' || this.data.adapterName === 'CRM') {
      adapterSettings = { ...adapterSettings, ...this.adapterSettings };
    }
    
    // Merge with common settings
    const result = {
      instanceName: this.instanceName.trim() || (this.data.adapterType === 'Source' ? 'Source' : 'Destination'),
      isEnabled: this.isEnabled ?? true,
      ...adapterSettings,
      // JQ Transformation properties (only for Destination adapters)
      jqScriptFile: this.data.adapterType === 'Destination' ? (this.jqScriptFile.trim() || '') : undefined,
      sourceAdapterSubscription: this.data.adapterType === 'Destination' ? (this.sourceAdapterSubscription.trim() || '') : undefined,
      // SQL Server custom statements (only for Destination adapters)
      insertStatement: this.data.adapterType === 'Destination' && this.data.adapterName === 'SqlServer' ? (this.insertStatement.trim() || '') : undefined,
      updateStatement: this.data.adapterType === 'Destination' && this.data.adapterName === 'SqlServer' ? (this.updateStatement.trim() || '') : undefined,
      deleteStatement: this.data.adapterType === 'Destination' && this.data.adapterName === 'SqlServer' ? (this.deleteStatement.trim() || '') : undefined
    };
    
    this.dialogRef.close(result);
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

  get showAdapterSettings(): boolean {
    return this.data.adapterName === 'CSV' || this.data.adapterName === 'FILE' || this.data.adapterName === 'SFTP' || this.data.adapterName === 'SqlServer';
  }

  get dialogTitle(): string {
    return `${this.data.adapterType} Adapter Properties (${this.data.adapterName})`;
  }

  openWizard(): void {
    // Collect current settings from adapter-specific component
    const currentSettings = this.adapterSettingsComponent?.getSettings() || {};
    
    // Merge with common settings
    const allCurrentSettings = {
      instanceName: this.instanceName,
      isEnabled: this.isEnabled,
      ...currentSettings,
      ...this.adapterSettings
    };

    const wizardData: AdapterWizardData = {
      adapterName: this.data.adapterName,
      adapterType: this.data.adapterType,
      currentSettings: allCurrentSettings,
      onSettingsUpdate: (settings: Record<string, any>) => {
        // Update settings in the dialog
        // First, update common settings
        if (settings.instanceName !== undefined) {
          this.instanceName = settings.instanceName;
        }
        if (settings.isEnabled !== undefined) {
          this.isEnabled = settings.isEnabled;
        }

        // Then, update adapter-specific settings
        if (this.adapterSettingsComponent) {
          // Remove common settings before passing to adapter component
          const adapterSettings = { ...settings };
          delete adapterSettings.instanceName;
          delete adapterSettings.isEnabled;
          
          // Initialize adapter settings component with new values
          this.adapterSettingsComponent.initializeSettings({
            ...this.data,
            ...adapterSettings
          });
        }

        // Also update SAP, Dynamics365, CRM settings if applicable
        if (this.data.adapterName === 'SAP' && this.sapAdapterSettings) {
          this.sapAdapterSettings.initializeSettings({
            ...this.data,
            ...settings
          });
        }
        if (this.data.adapterName === 'Dynamics365' && this.dynamics365AdapterSettings) {
          this.dynamics365AdapterSettings.initializeSettings({
            ...this.data,
            ...settings
          });
        }
        if (this.data.adapterName === 'CRM' && this.crmAdapterSettings) {
          this.crmAdapterSettings.initializeSettings({
            ...this.data,
            ...settings
          });
        }
      }
    };

    const wizardDialogRef = this.dialog.open(AdapterWizardComponent, {
      width: '800px',
      maxWidth: '90vw',
      maxHeight: '90vh',
      data: wizardData,
      disableClose: false
    });

    wizardDialogRef.afterClosed().subscribe(result => {
      // Wizard was closed, settings are already updated via onSettingsUpdate callback
    });
  }
}

