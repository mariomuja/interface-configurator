import { Component, Inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatRadioModule } from '@angular/material/radio';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatCardModule } from '@angular/material/card';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { AdapterWizardData, WizardStep, WizardOption, AdapterWizardConfig, AdapterWizardValues } from '../../models/adapter-wizard.model';
import { TransportService } from '../../services/transport.service';

@Component({
  selector: 'app-adapter-wizard',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatProgressBarModule,
    MatRadioModule,
    MatCheckboxModule,
    MatCardModule,
    MatSlideToggleModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './adapter-wizard.component.html',
  styleUrl: './adapter-wizard.component.css'
})
export class AdapterWizardComponent implements OnInit, OnDestroy {
  currentStepIndex: number = 0;
  wizardConfig: AdapterWizardConfig | null = null;
  stepValues: AdapterWizardValues = {};
  availableServers: string[] = [];
  availableRfcs: string[] = [];
  isLoadingServers: boolean = false;
  isLoadingRfcs: boolean = false;
  validationErrors: Record<string, string> = {};

  constructor(
    public dialogRef: MatDialogRef<AdapterWizardComponent>,
    @Inject(MAT_DIALOG_DATA) public data: AdapterWizardData,
    private transportService: TransportService
  ) {
    // Initialize step values from current settings
    this.stepValues = { ...data.currentSettings };
  }

  ngOnInit(): void {
    this.loadWizardConfig();
    this.loadInitialData();
  }

  ngOnDestroy(): void {
    // Cleanup if needed
  }

  get currentStep(): WizardStep | null {
    if (!this.wizardConfig || this.currentStepIndex >= this.wizardConfig.steps.length) {
      return null;
    }
    return this.wizardConfig.steps[this.currentStepIndex];
  }

  get progress(): number {
    if (!this.wizardConfig || this.wizardConfig.steps.length === 0) {
      return 0;
    }
    return ((this.currentStepIndex + 1) / this.wizardConfig.steps.length) * 100;
  }

  get canGoBack(): boolean {
    return this.currentStepIndex > 0;
  }

  get canGoNext(): boolean {
    if (!this.currentStep) {
      return false;
    }
    return this.isStepValid(this.currentStep);
  }

  get isLastStep(): boolean {
    if (!this.wizardConfig) {
      return false;
    }
    return this.currentStepIndex === this.wizardConfig.steps.length - 1;
  }

  loadWizardConfig(): void {
    // Load adapter-specific wizard configuration
    this.wizardConfig = this.getWizardConfigForAdapter(this.data.adapterName, this.data.adapterType);
  }

  loadInitialData(): void {
    // Load initial data like available servers, RFCs, etc.
    if (this.data.adapterName === 'SqlServer') {
      this.loadAvailableServers();
    } else if (this.data.adapterName === 'SAP') {
      this.loadAvailableRfcs();
    }
  }

  async loadAvailableServers(): Promise<void> {
    this.isLoadingServers = true;
    try {
      // Call backend API to discover SQL servers
      // For now, provide common examples
      this.availableServers = [
        'localhost',
        'localhost\\SQLEXPRESS',
        '.\\SQLEXPRESS',
        'sql-server.database.windows.net'
      ];
      
      // Try to discover servers via API if available
      // const servers = await this.transportService.discoverSqlServers().toPromise();
      // if (servers) {
      //   this.availableServers = servers;
      // }
    } catch (error) {
      console.error('Error loading servers:', error);
    } finally {
      this.isLoadingServers = false;
    }
  }

  async loadAvailableRfcs(): Promise<void> {
    this.isLoadingRfcs = true;
    try {
      // Call backend API to discover SAP RFCs
      // For now, provide common examples
      this.availableRfcs = [
        'IDOC_INBOUND_ASYNCHRONOUS',
        'IDOC_OUTBOUND_ASYNCHRONOUS',
        'RFC_READ_TABLE',
        'BAPI_MATERIAL_GET_DETAIL',
        'BAPI_CUSTOMER_GETDETAIL'
      ];
      
      // Try to discover RFCs via API if available
      // const rfcs = await this.transportService.discoverSapRfcs().toPromise();
      // if (rfcs) {
      //   this.availableRfcs = rfcs;
      // }
    } catch (error) {
      console.error('Error loading RFCs:', error);
    } finally {
      this.isLoadingRfcs = false;
    }
  }

  getWizardConfigForAdapter(adapterName: string, adapterType: 'Source' | 'Destination'): AdapterWizardConfig {
    // Return adapter-specific wizard configuration
    const configs: Record<string, AdapterWizardConfig> = {
      'CSV': this.getCsvWizardConfig(adapterType),
      'SqlServer': this.getSqlServerWizardConfig(adapterType),
      'SAP': this.getSapWizardConfig(adapterType),
      'Dynamics365': this.getDynamics365WizardConfig(adapterType),
      'CRM': this.getCrmWizardConfig(adapterType),
      'SFTP': this.getSftpWizardConfig(adapterType)
    };

    return configs[adapterName] || this.getDefaultWizardConfig(adapterName, adapterType);
  }

  getCsvWizardConfig(adapterType: 'Source' | 'Destination'): AdapterWizardConfig {
    const steps: WizardStep[] = [
      {
        id: 'adapter-type',
        title: 'Adapter-Typ auswählen',
        description: 'Wählen Sie, wie der CSV-Adapter Daten verarbeiten soll.',
        inputType: 'multichoice',
        fieldName: 'csvAdapterType',
        required: true,
        options: [
          {
            value: 'RAW',
            label: 'RAW - Daten direkt eingeben',
            description: 'CSV-Daten werden direkt in der Benutzeroberfläche eingegeben',
            icon: 'edit'
          },
          {
            value: 'FILE',
            label: 'FILE - Dateien aus Blob Storage',
            description: 'CSV-Dateien werden aus Azure Blob Storage gelesen/geschrieben',
            icon: 'folder'
          },
          {
            value: 'SFTP',
            label: 'SFTP - Dateien von SFTP-Server',
            description: 'CSV-Dateien werden von einem SFTP-Server gelesen/geschrieben',
            icon: 'cloud_upload'
          }
        ],
        defaultValue: adapterType === 'Destination' ? 'FILE' : 'RAW'
      }
    ];

    // Add SFTP-specific steps if SFTP is selected
    steps.push({
      id: 'sftp-connection',
      title: 'SFTP-Verbindung konfigurieren',
      description: 'Geben Sie die Verbindungsdaten für den SFTP-Server ein.',
      inputType: 'text',
      fieldName: 'sftpHost',
      placeholder: 'z.B. sftp.example.com',
      required: true,
      conditional: {
        field: 'csvAdapterType',
        value: 'SFTP',
        operator: 'equals'
      },
      helpText: 'Der Hostname oder die IP-Adresse des SFTP-Servers'
    });

    steps.push({
      id: 'sftp-port',
      title: 'SFTP-Port',
      description: 'Der Port für die SFTP-Verbindung (Standard: 22).',
      inputType: 'number',
      fieldName: 'sftpPort',
      placeholder: '22',
      required: true,
      defaultValue: 22,
      min: 1,
      max: 65535,
      conditional: {
        field: 'csvAdapterType',
        value: 'SFTP',
        operator: 'equals'
      }
    });

    steps.push({
      id: 'sftp-auth',
      title: 'Authentifizierung',
      description: 'Wie möchten Sie sich beim SFTP-Server authentifizieren?',
      inputType: 'multichoice',
      fieldName: 'sftpAuthMethod',
      required: true,
      options: [
        {
          value: 'password',
          label: 'Passwort',
          description: 'Authentifizierung mit Benutzername und Passwort',
          icon: 'lock'
        },
        {
          value: 'sshkey',
          label: 'SSH-Schlüssel',
          description: 'Authentifizierung mit einem SSH-Private-Key',
          icon: 'vpn_key'
        }
      ],
      conditional: {
        field: 'csvAdapterType',
        value: 'SFTP',
        operator: 'equals'
      }
    });

    steps.push({
      id: 'sftp-username',
      title: 'SFTP-Benutzername',
      description: 'Geben Sie den Benutzernamen für die SFTP-Verbindung ein.',
      inputType: 'text',
      fieldName: 'sftpUsername',
      placeholder: 'benutzername',
      required: true,
      conditional: {
        field: 'csvAdapterType',
        value: 'SFTP',
        operator: 'equals'
      }
    });

    steps.push({
      id: 'sftp-password',
      title: 'SFTP-Passwort',
      description: 'Geben Sie das Passwort für die SFTP-Verbindung ein.',
      inputType: 'password',
      fieldName: 'sftpPassword',
      placeholder: '••••••••',
      required: true,
      conditional: {
        field: 'sftpAuthMethod',
        value: 'password',
        operator: 'equals'
      }
    });

    steps.push({
      id: 'sftp-sshkey',
      title: 'SSH-Private-Key',
      description: 'Wählen Sie die SSH-Private-Key-Datei aus oder fügen Sie den Schlüsselinhalt ein.',
      inputType: 'filepicker',
      fieldName: 'sftpSshKey',
      placeholder: 'Pfad zur SSH-Key-Datei oder Key-Inhalt',
      required: true,
      conditional: {
        field: 'sftpAuthMethod',
        value: 'sshkey',
        operator: 'equals'
      },
      helpText: 'Sie können eine .pem oder .key Datei auswählen oder den Key-Inhalt direkt einfügen'
    });

    // Add FILE-specific steps
    if (adapterType === 'Source') {
      steps.push({
        id: 'receive-folder',
        title: 'Eingangsordner',
        description: 'Geben Sie den Ordnerpfad in Blob Storage an, aus dem CSV-Dateien gelesen werden sollen.',
        inputType: 'filepicker',
        fieldName: 'receiveFolder',
        placeholder: 'z.B. csv-files/csv-incoming',
        required: true,
        conditional: {
          field: 'csvAdapterType',
          value: 'FILE',
          operator: 'equals'
        },
        helpText: 'Der Pfad zum Ordner in Azure Blob Storage'
      });
    } else {
      steps.push({
        id: 'destination-folder',
        title: 'Ausgangsordner',
        description: 'Geben Sie den Ordnerpfad in Blob Storage an, in den CSV-Dateien geschrieben werden sollen.',
        inputType: 'filepicker',
        fieldName: 'destinationReceiveFolder',
        placeholder: 'z.B. csv-files/csv-outgoing',
        required: true,
        conditional: {
          field: 'csvAdapterType',
          value: 'FILE',
          operator: 'equals'
        }
      });
    }

    steps.push({
      id: 'file-mask',
      title: 'Dateimuster',
      description: 'Geben Sie das Dateimuster an, nach dem gesucht werden soll (z.B. *.csv, *.txt).',
      inputType: 'text',
      fieldName: adapterType === 'Source' ? 'fileMask' : 'destinationFileMask',
      placeholder: '*.txt',
      required: true,
      defaultValue: '*.txt',
      conditional: {
        field: 'csvAdapterType',
        value: 'FILE',
        operator: 'equals'
      }
    });

    // Add RAW-specific steps for Source
    if (adapterType === 'Source') {
      steps.push({
        id: 'csv-data',
        title: 'CSV-Daten eingeben',
        description: 'Fügen Sie hier die CSV-Daten ein, die verarbeitet werden sollen.',
        inputType: 'textarea',
        fieldName: 'csvData',
        placeholder: 'Spalte1║Spalte2║Spalte3\nWert1║Wert2║Wert3',
        required: true,
        conditional: {
          field: 'csvAdapterType',
          value: 'RAW',
          operator: 'equals'
        },
        helpText: 'Verwenden Sie ║ als Feldtrennzeichen'
      });
    }

    steps.push({
      id: 'field-separator',
      title: 'Feldtrennzeichen',
      description: 'Welches Zeichen trennt die Felder in Ihren CSV-Dateien?',
      inputType: 'select',
      fieldName: 'fieldSeparator',
      required: true,
      defaultValue: '║',
      options: [
        { value: '║', label: '║ (Pipe, Standard)' },
        { value: ',', label: ', (Komma)' },
        { value: ';', label: '; (Semikolon)' },
        { value: '\t', label: 'Tab' },
        { value: '|', label: '| (Pipe)' }
      ]
    });

    if (adapterType === 'Source') {
      steps.push({
        id: 'polling-interval',
        title: 'Abfrageintervall',
        description: 'Wie oft soll nach neuen Daten gesucht werden? (in Sekunden)',
        inputType: 'number',
        fieldName: 'csvPollingInterval',
        placeholder: '10',
        required: true,
        defaultValue: 10,
        min: 1,
        max: 3600,
        helpText: 'Empfohlen: 10-60 Sekunden'
      });
    }

    steps.push({
      id: 'batch-size',
      title: 'Batch-Größe',
      description: 'Wie viele Datensätze sollen auf einmal verarbeitet werden?',
      inputType: 'number',
      fieldName: 'batchSize',
      placeholder: '1000',
      required: true,
      defaultValue: 1000,
      min: 1,
      max: 10000,
      helpText: 'Größere Batches verbessern die Performance, benötigen aber mehr Speicher'
    });

    return {
      adapterName: 'CSV',
      adapterType,
      steps,
      onComplete: (values) => {
        const settings: any = {
          csvAdapterType: values.csvAdapterType || (adapterType === 'Destination' ? 'FILE' : 'RAW'),
          fieldSeparator: values.fieldSeparator || '║',
          batchSize: values.batchSize || 1000
        };

        if (adapterType === 'Source') {
          settings.receiveFolder = values.receiveFolder || '';
          settings.fileMask = values.fileMask || '*.txt';
          settings.csvData = values.csvData || '';
          settings.csvPollingInterval = values.csvPollingInterval || 10;
        } else {
          settings.destinationReceiveFolder = values.destinationReceiveFolder || '';
          settings.destinationFileMask = values.destinationFileMask || '*.txt';
        }

        if (values.csvAdapterType === 'SFTP') {
          settings.sftpHost = values.sftpHost || '';
          settings.sftpPort = values.sftpPort || 22;
          settings.sftpUsername = values.sftpUsername || '';
          if (values.sftpAuthMethod === 'password') {
            settings.sftpPassword = values.sftpPassword || '';
          } else if (values.sftpAuthMethod === 'sshkey') {
            settings.sftpSshKey = values.sftpSshKey || '';
          }
          settings.sftpFolder = values.sftpFolder || '';
          settings.sftpFileMask = values.sftpFileMask || '*.txt';
        }

        return settings;
      }
    };
  }

  getSqlServerWizardConfig(adapterType: 'Source' | 'Destination'): AdapterWizardConfig {
    const steps: WizardStep[] = [
      {
        id: 'server-name',
        title: 'SQL Server auswählen',
        description: 'Wählen Sie den SQL Server aus, mit dem Sie sich verbinden möchten.',
        inputType: 'select',
        fieldName: 'sqlServerName',
        required: true,
        options: [], // Will be populated with availableServers
        placeholder: 'Server auswählen oder eingeben...',
        helpText: 'Sie können einen Server aus der Liste auswählen oder einen neuen eingeben'
      },
      {
        id: 'database-name',
        title: 'Datenbankname',
        description: 'Geben Sie den Namen der Datenbank ein, mit der Sie arbeiten möchten.',
        inputType: 'text',
        fieldName: 'sqlDatabaseName',
        placeholder: 'z.B. MyDatabase',
        required: true
      },
      {
        id: 'auth-method',
        title: 'Authentifizierungsmethode',
        description: 'Wie möchten Sie sich beim SQL Server authentifizieren?',
        inputType: 'multichoice',
        fieldName: 'sqlAuthMethod',
        required: true,
        options: [
          {
            value: 'integrated',
            label: 'Windows-Authentifizierung',
            description: 'Verwendet Ihre Windows-Anmeldedaten (empfohlen für lokale Server)',
            icon: 'account_circle'
          },
          {
            value: 'sql',
            label: 'SQL-Authentifizierung',
            description: 'Benutzername und Passwort',
            icon: 'lock'
          },
          {
            value: 'managed',
            label: 'Azure Managed Identity',
            description: 'Verwendet Azure Managed Identity (für Azure SQL)',
            icon: 'cloud'
          }
        ],
        defaultValue: 'integrated'
      },
      {
        id: 'sql-username',
        title: 'SQL-Benutzername',
        description: 'Geben Sie den SQL Server-Benutzernamen ein.',
        inputType: 'text',
        fieldName: 'sqlUserName',
        placeholder: 'sa',
        required: true,
        conditional: {
          field: 'sqlAuthMethod',
          value: 'sql',
          operator: 'equals'
        }
      },
      {
        id: 'sql-password',
        title: 'SQL-Passwort',
        description: 'Geben Sie das SQL Server-Passwort ein.',
        inputType: 'password',
        fieldName: 'sqlPassword',
        placeholder: '••••••••',
        required: true,
        conditional: {
          field: 'sqlAuthMethod',
          value: 'sql',
          operator: 'equals'
        }
      },
      {
        id: 'resource-group',
        title: 'Azure Resource Group',
        description: 'Geben Sie die Azure Resource Group an (nur für Azure SQL mit Managed Identity).',
        inputType: 'text',
        fieldName: 'sqlResourceGroup',
        placeholder: 'rg-my-resources',
        conditional: {
          field: 'sqlAuthMethod',
          value: 'managed',
          operator: 'equals'
        }
      }
    ];

    if (adapterType === 'Source') {
      steps.push({
        id: 'table-name',
        title: 'Tabellenname',
        description: 'Geben Sie den Namen der Tabelle ein, aus der Daten gelesen werden sollen.',
        inputType: 'text',
        fieldName: 'tableName',
        placeholder: 'z.B. Orders',
        required: true
      });

      steps.push({
        id: 'polling-statement',
        title: 'Abfrage-Statement',
        description: 'Geben Sie das SQL-Statement ein, das ausgeführt werden soll, um neue Daten abzurufen.',
        inputType: 'textarea',
        fieldName: 'sqlPollingStatement',
        placeholder: 'SELECT * FROM Orders WHERE Processed = 0',
        required: false,
        helpText: 'Wenn leer gelassen, wird automatisch SELECT * FROM [Tabellenname] verwendet'
      });

      steps.push({
        id: 'polling-interval',
        title: 'Abfrageintervall',
        description: 'Wie oft soll das Abfrage-Statement ausgeführt werden? (in Sekunden)',
        inputType: 'number',
        fieldName: 'sqlPollingInterval',
        placeholder: '60',
        required: true,
        defaultValue: 60,
        min: 1,
        max: 3600
      });
    } else {
      steps.push({
        id: 'table-name',
        title: 'Ziel-Tabellenname',
        description: 'Geben Sie den Namen der Tabelle ein, in die Daten geschrieben werden sollen.',
        inputType: 'text',
        fieldName: 'tableName',
        placeholder: 'z.B. TransportData',
        required: true,
        defaultValue: 'TransportData'
      });
    }

    steps.push({
      id: 'use-transaction',
      title: 'Transaktionen verwenden',
      description: 'Sollen alle Schreibvorgänge in einer Transaktion ausgeführt werden?',
      inputType: 'toggle',
      fieldName: 'sqlUseTransaction',
      defaultValue: false,
      helpText: 'Empfohlen für kritische Daten, um Konsistenz zu gewährleisten'
    });

    steps.push({
      id: 'batch-size',
      title: 'Batch-Größe',
      description: 'Wie viele Datensätze sollen auf einmal verarbeitet werden?',
      inputType: 'number',
      fieldName: 'sqlBatchSize',
      placeholder: '1000',
      required: true,
      defaultValue: 1000,
      min: 1,
      max: 10000
    });

    return {
      adapterName: 'SqlServer',
      adapterType,
      steps,
      onComplete: (values) => {
        const settings: any = {
          sqlServerName: values.sqlServerName || '',
          sqlDatabaseName: values.sqlDatabaseName || '',
          sqlIntegratedSecurity: values.sqlAuthMethod === 'integrated',
          sqlResourceGroup: values.sqlResourceGroup || '',
          sqlUseTransaction: values.sqlUseTransaction || false,
          sqlBatchSize: values.sqlBatchSize || 1000,
          tableName: values.tableName || (adapterType === 'Destination' ? 'TransportData' : '')
        };

        if (values.sqlAuthMethod === 'sql') {
          settings.sqlUserName = values.sqlUserName || '';
          settings.sqlPassword = values.sqlPassword || '';
        }

        if (adapterType === 'Source') {
          settings.sqlPollingStatement = values.sqlPollingStatement || '';
          settings.sqlPollingInterval = values.sqlPollingInterval || 60;
        }

        return settings;
      }
    };
  }

  getSapWizardConfig(adapterType: 'Source' | 'Destination'): AdapterWizardConfig {
    const steps: WizardStep[] = [
      {
        id: 'connection-type',
        title: 'Verbindungstyp',
        description: 'Wie möchten Sie sich mit SAP verbinden?',
        inputType: 'multichoice',
        fieldName: 'sapConnectionType',
        required: true,
        options: [
          {
            value: 'rfc',
            label: 'RFC (Remote Function Call)',
            description: 'Klassische RFC-Verbindung zu SAP',
            icon: 'settings_ethernet'
          },
          {
            value: 'odata',
            label: 'OData (S/4HANA)',
            description: 'Moderne OData-Verbindung für SAP S/4HANA',
            icon: 'cloud'
          },
          {
            value: 'rest',
            label: 'REST API',
            description: 'REST API-Verbindung für SAP S/4HANA',
            icon: 'api'
          }
        ],
        defaultValue: 'rfc'
      },
      {
        id: 'sap-server',
        title: 'SAP-Server',
        description: 'Geben Sie den Hostnamen oder die IP-Adresse des SAP-Servers ein.',
        inputType: 'text',
        fieldName: 'sapApplicationServer',
        placeholder: 'z.B. sap-server.example.com',
        required: true,
        conditional: {
          field: 'sapConnectionType',
          value: 'rfc',
          operator: 'equals'
        }
      },
      {
        id: 'sap-system-number',
        title: 'Systemnummer',
        description: 'Geben Sie die SAP-Systemnummer ein (meist 00).',
        inputType: 'text',
        fieldName: 'sapSystemNumber',
        placeholder: '00',
        required: true,
        conditional: {
          field: 'sapConnectionType',
          value: 'rfc',
          operator: 'equals'
        }
      },
      {
        id: 'sap-client',
        title: 'SAP-Mandant',
        description: 'Geben Sie die SAP-Mandantennummer ein.',
        inputType: 'text',
        fieldName: 'sapClient',
        placeholder: 'z.B. 100',
        required: true,
        conditional: {
          field: 'sapConnectionType',
          value: 'rfc',
          operator: 'equals'
        }
      },
      {
        id: 'sap-username',
        title: 'SAP-Benutzername',
        description: 'Geben Sie den SAP-Benutzernamen ein.',
        inputType: 'text',
        fieldName: 'sapUsername',
        placeholder: 'SAP-Benutzername',
        required: true,
        conditional: {
          field: 'sapConnectionType',
          value: 'rfc',
          operator: 'equals'
        }
      },
      {
        id: 'sap-password',
        title: 'SAP-Passwort',
        description: 'Geben Sie das SAP-Passwort ein.',
        inputType: 'password',
        fieldName: 'sapPassword',
        placeholder: '••••••••',
        required: true,
        conditional: {
          field: 'sapConnectionType',
          value: 'rfc',
          operator: 'equals'
        }
      },
      {
        id: 'rfc-function',
        title: 'RFC-Funktionsmodul',
        description: 'Wählen Sie das RFC-Funktionsmodul aus, das verwendet werden soll.',
        inputType: 'select',
        fieldName: 'sapRfcFunctionModule',
        required: true,
        options: [], // Will be populated with availableRfcs
        placeholder: 'RFC auswählen...',
        conditional: {
          field: 'sapConnectionType',
          value: 'rfc',
          operator: 'equals'
        },
        helpText: 'Die verfügbaren RFCs werden vom SAP-System geladen'
      }
    ];

    if (adapterType === 'Source') {
      steps.push({
        id: 'idoc-type',
        title: 'IDOC-Typ',
        description: 'Geben Sie den IDOC-Typ ein, der gelesen werden soll.',
        inputType: 'text',
        fieldName: 'sapIdocType',
        placeholder: 'z.B. MATMAS01',
        conditional: {
          field: 'sapConnectionType',
          value: 'rfc',
          operator: 'equals'
        }
      });
    }

    return {
      adapterName: 'SAP',
      adapterType,
      steps,
      onComplete: (values) => {
        const settings: any = {
          sapUseRfc: values.sapConnectionType === 'rfc',
          sapUseOData: values.sapConnectionType === 'odata',
          sapUseRestApi: values.sapConnectionType === 'rest'
        };

        if (values.sapConnectionType === 'rfc') {
          settings.sapApplicationServer = values.sapApplicationServer || '';
          settings.sapSystemNumber = values.sapSystemNumber || '';
          settings.sapClient = values.sapClient || '';
          settings.sapUsername = values.sapUsername || '';
          settings.sapPassword = values.sapPassword || '';
          settings.sapRfcFunctionModule = values.sapRfcFunctionModule || '';
          if (adapterType === 'Source') {
            settings.sapIdocType = values.sapIdocType || '';
          }
        } else if (values.sapConnectionType === 'odata') {
          settings.sapODataServiceUrl = values.sapODataServiceUrl || '';
        } else if (values.sapConnectionType === 'rest') {
          settings.sapRestApiEndpoint = values.sapRestApiEndpoint || '';
        }

        return settings;
      }
    };
  }

  getDynamics365WizardConfig(adapterType: 'Source' | 'Destination'): AdapterWizardConfig {
    const steps: WizardStep[] = [
      {
        id: 'instance-url',
        title: 'Dynamics 365 Instanz-URL',
        description: 'Geben Sie die URL Ihrer Dynamics 365-Instanz ein.',
        inputType: 'text',
        fieldName: 'dynamics365InstanceUrl',
        placeholder: 'https://org.crm.dynamics.com',
        required: true,
        helpText: 'Die vollständige URL Ihrer Dynamics 365-Organisation'
      },
      {
        id: 'auth-method',
        title: 'Authentifizierung',
        description: 'Wie möchten Sie sich bei Dynamics 365 authentifizieren?',
        inputType: 'multichoice',
        fieldName: 'dynamics365AuthMethod',
        required: true,
        options: [
          {
            value: 'oauth',
            label: 'OAuth 2.0 (Client Credentials)',
            description: 'Empfohlen: Verwendet Azure AD App-Registrierung',
            icon: 'verified_user'
          }
        ],
        defaultValue: 'oauth'
      },
      {
        id: 'tenant-id',
        title: 'Azure AD Tenant ID',
        description: 'Geben Sie die Tenant ID Ihres Azure AD-Verzeichnisses ein.',
        inputType: 'text',
        fieldName: 'dynamics365TenantId',
        placeholder: 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx',
        required: true,
        conditional: {
          field: 'dynamics365AuthMethod',
          value: 'oauth',
          operator: 'equals'
        }
      },
      {
        id: 'client-id',
        title: 'Client ID (Application ID)',
        description: 'Geben Sie die Client ID Ihrer Azure AD App-Registrierung ein.',
        inputType: 'text',
        fieldName: 'dynamics365ClientId',
        placeholder: 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx',
        required: true,
        conditional: {
          field: 'dynamics365AuthMethod',
          value: 'oauth',
          operator: 'equals'
        }
      },
      {
        id: 'client-secret',
        title: 'Client Secret',
        description: 'Geben Sie das Client Secret Ihrer Azure AD App-Registrierung ein.',
        inputType: 'password',
        fieldName: 'dynamics365ClientSecret',
        placeholder: '••••••••',
        required: true,
        conditional: {
          field: 'dynamics365AuthMethod',
          value: 'oauth',
          operator: 'equals'
        }
      },
      {
        id: 'entity-name',
        title: 'Entity-Name',
        description: 'Geben Sie den Namen der Dynamics 365-Entity ein, mit der Sie arbeiten möchten.',
        inputType: 'text',
        fieldName: 'dynamics365EntityName',
        placeholder: 'z.B. accounts, contacts, invoices',
        required: true
      }
    ];

    if (adapterType === 'Source') {
      steps.push({
        id: 'odata-filter',
        title: 'OData-Filter (optional)',
        description: 'Geben Sie einen optionalen OData-Filter ein, um die abgerufenen Daten einzuschränken.',
        inputType: 'textarea',
        fieldName: 'dynamics365ODataFilter',
        placeholder: 'name eq \'Test\' and statuscode eq 1',
        helpText: 'OData-Filter-Syntax verwenden'
      });
    }

    steps.push({
      id: 'batch-size',
      title: 'Batch-Größe',
      description: 'Wie viele Datensätze sollen auf einmal verarbeitet werden?',
      inputType: 'number',
      fieldName: 'dynamics365BatchSize',
      placeholder: '100',
      required: true,
      defaultValue: 100,
      min: 1,
      max: 1000
    });

    return {
      adapterName: 'Dynamics365',
      adapterType,
      steps,
      onComplete: (values) => {
        return {
          dynamics365InstanceUrl: values.dynamics365InstanceUrl || '',
          dynamics365TenantId: values.dynamics365TenantId || '',
          dynamics365ClientId: values.dynamics365ClientId || '',
          dynamics365ClientSecret: values.dynamics365ClientSecret || '',
          dynamics365EntityName: values.dynamics365EntityName || '',
          dynamics365ODataFilter: values.dynamics365ODataFilter || '',
          dynamics365BatchSize: values.dynamics365BatchSize || 100
        };
      }
    };
  }

  getCrmWizardConfig(adapterType: 'Source' | 'Destination'): AdapterWizardConfig {
    // Similar to Dynamics365 but with CRM-specific fields
    return this.getDynamics365WizardConfig(adapterType);
  }

  getSftpWizardConfig(adapterType: 'Source' | 'Destination'): AdapterWizardConfig {
    // Similar to CSV SFTP configuration
    const steps: WizardStep[] = [
      {
        id: 'sftp-host',
        title: 'SFTP-Server',
        description: 'Geben Sie den Hostnamen oder die IP-Adresse des SFTP-Servers ein.',
        inputType: 'text',
        fieldName: 'sftpHost',
        placeholder: 'sftp.example.com',
        required: true
      },
      {
        id: 'sftp-port',
        title: 'SFTP-Port',
        description: 'Der Port für die SFTP-Verbindung (Standard: 22).',
        inputType: 'number',
        fieldName: 'sftpPort',
        placeholder: '22',
        required: true,
        defaultValue: 22,
        min: 1,
        max: 65535
      },
      {
        id: 'sftp-auth',
        title: 'Authentifizierung',
        description: 'Wie möchten Sie sich beim SFTP-Server authentifizieren?',
        inputType: 'multichoice',
        fieldName: 'sftpAuthMethod',
        required: true,
        options: [
          {
            value: 'password',
            label: 'Passwort',
            description: 'Authentifizierung mit Benutzername und Passwort',
            icon: 'lock'
          },
          {
            value: 'sshkey',
            label: 'SSH-Schlüssel',
            description: 'Authentifizierung mit einem SSH-Private-Key',
            icon: 'vpn_key'
          }
        ]
      },
      {
        id: 'sftp-username',
        title: 'SFTP-Benutzername',
        description: 'Geben Sie den Benutzernamen für die SFTP-Verbindung ein.',
        inputType: 'text',
        fieldName: 'sftpUsername',
        placeholder: 'benutzername',
        required: true
      },
      {
        id: 'sftp-password',
        title: 'SFTP-Passwort',
        description: 'Geben Sie das Passwort für die SFTP-Verbindung ein.',
        inputType: 'password',
        fieldName: 'sftpPassword',
        placeholder: '••••••••',
        required: true,
        conditional: {
          field: 'sftpAuthMethod',
          value: 'password',
          operator: 'equals'
        }
      },
      {
        id: 'sftp-sshkey',
        title: 'SSH-Private-Key',
        description: 'Wählen Sie die SSH-Private-Key-Datei aus oder fügen Sie den Schlüsselinhalt ein.',
        inputType: 'filepicker',
        fieldName: 'sftpSshKey',
        placeholder: 'Pfad zur SSH-Key-Datei oder Key-Inhalt',
        required: true,
        conditional: {
          field: 'sftpAuthMethod',
          value: 'sshkey',
          operator: 'equals'
        }
      },
      {
        id: 'sftp-folder',
        title: 'SFTP-Ordner',
        description: 'Geben Sie den Ordnerpfad auf dem SFTP-Server ein.',
        inputType: 'text',
        fieldName: 'sftpFolder',
        placeholder: '/home/user/data',
        required: true
      },
      {
        id: 'sftp-filemask',
        title: 'Dateimuster',
        description: 'Geben Sie das Dateimuster an (z.B. *.csv, *.txt).',
        inputType: 'text',
        fieldName: 'sftpFileMask',
        placeholder: '*.txt',
        required: true,
        defaultValue: '*.txt'
      }
    ];

    return {
      adapterName: 'SFTP',
      adapterType,
      steps,
      onComplete: (values) => {
        const settings: any = {
          sftpHost: values.sftpHost || '',
          sftpPort: values.sftpPort || 22,
          sftpUsername: values.sftpUsername || '',
          sftpFolder: values.sftpFolder || '',
          sftpFileMask: values.sftpFileMask || '*.txt'
        };

        if (values.sftpAuthMethod === 'password') {
          settings.sftpPassword = values.sftpPassword || '';
        } else if (values.sftpAuthMethod === 'sshkey') {
          settings.sftpSshKey = values.sftpSshKey || '';
        }

        return settings;
      }
    };
  }

  getDefaultWizardConfig(adapterName: string, adapterType: 'Source' | 'Destination'): AdapterWizardConfig {
    return {
      adapterName,
      adapterType,
      steps: [
        {
          id: 'basic-config',
          title: 'Grundkonfiguration',
          description: 'Bitte konfigurieren Sie die grundlegenden Einstellungen für diesen Adapter.',
          inputType: 'text',
          fieldName: 'instanceName',
          placeholder: 'Adapter-Instanzname',
          required: true
        }
      ],
      onComplete: (values) => values
    };
  }

  isStepVisible(step: WizardStep): boolean {
    if (!step.conditional) {
      return true;
    }

    const conditionalValue = this.stepValues[step.conditional.field];
    const targetValue = step.conditional.value;

    switch (step.conditional.operator || 'equals') {
      case 'equals':
        return conditionalValue === targetValue;
      case 'notEquals':
        return conditionalValue !== targetValue;
      case 'contains':
        return String(conditionalValue || '').includes(String(targetValue));
      case 'exists':
        return conditionalValue !== undefined && conditionalValue !== null && conditionalValue !== '';
      default:
        return true;
    }
  }

  getVisibleSteps(): WizardStep[] {
    if (!this.wizardConfig) {
      return [];
    }
    return this.wizardConfig.steps.filter(step => this.isStepVisible(step));
  }

  getCurrentVisibleStepIndex(): number {
    const visibleSteps = this.getVisibleSteps();
    const currentStep = this.currentStep;
    if (!currentStep) {
      return 0;
    }
    return visibleSteps.findIndex(s => s.id === currentStep.id);
  }

  goToStep(index: number): void {
    const visibleSteps = this.getVisibleSteps();
    if (index >= 0 && index < visibleSteps.length) {
      const targetStep = visibleSteps[index];
      const targetIndex = this.wizardConfig!.steps.findIndex(s => s.id === targetStep.id);
      if (targetIndex >= 0) {
        this.currentStepIndex = targetIndex;
        this.validationErrors = {};
      }
    }
  }

  goBack(): void {
    const visibleSteps = this.getVisibleSteps();
    const currentVisibleIndex = this.getCurrentVisibleStepIndex();
    if (currentVisibleIndex > 0) {
      this.goToStep(currentVisibleIndex - 1);
    }
  }

  goNext(): void {
    if (!this.currentStep) {
      return;
    }

    if (!this.isStepValid(this.currentStep)) {
      return;
    }

    const visibleSteps = this.getVisibleSteps();
    const currentVisibleIndex = this.getCurrentVisibleStepIndex();
    if (currentVisibleIndex < visibleSteps.length - 1) {
      this.goToStep(currentVisibleIndex + 1);
    }
  }

  isStepValid(step: WizardStep): boolean {
    const value = this.stepValues[step.fieldName];
    
    if (step.required && (value === undefined || value === null || value === '')) {
      this.validationErrors[step.fieldName] = 'Dieses Feld ist erforderlich';
      return false;
    }

    if (step.validation) {
      const validationResult = step.validation(value);
      if (!validationResult.valid) {
        this.validationErrors[step.fieldName] = validationResult.error || 'Ungültiger Wert';
        return false;
      }
    }

    delete this.validationErrors[step.fieldName];
    return true;
  }

  getValidationError(fieldName: string): string | undefined {
    return this.validationErrors[fieldName];
  }

  onValueChange(step: WizardStep, value: any): void {
    this.stepValues[step.fieldName] = value;
    
    // Call step completion callback if available
    if (this.wizardConfig?.onStepComplete) {
      this.wizardConfig.onStepComplete(step.id, value, this.stepValues);
    }

    // Clear validation error when value changes
    delete this.validationErrors[step.fieldName];
  }

  openFilePicker(step: WizardStep): void {
    // Create a file input element
    const input = document.createElement('input');
    input.type = 'file';
    input.style.display = 'none';
    
    // Set file filters based on step
    if (step.fieldName.includes('ssh') || step.fieldName.includes('key')) {
      input.accept = '.pem,.key,.ppk';
    } else if (step.fieldName.includes('folder') || step.fieldName.includes('path')) {
      // For folder selection, we'll use a text input with browse button
      // In a real implementation, you might want to use a folder picker API
      return;
    }

    input.onchange = (event: any) => {
      const file = event.target.files[0];
      if (file) {
        // Read file content
        const reader = new FileReader();
        reader.onload = (e: any) => {
          this.stepValues[step.fieldName] = e.target.result;
          this.onValueChange(step, e.target.result);
        };
        reader.readAsText(file);
      }
      document.body.removeChild(input);
    };

    document.body.appendChild(input);
    input.click();
  }

  openFolderPicker(step: WizardStep): void {
    // For folder paths in Blob Storage, show a dialog or use text input
    // In a real implementation, you might want to integrate with Blob Container Explorer
    const folderPath = prompt('Geben Sie den Ordnerpfad ein (z.B. csv-files/csv-incoming):');
    if (folderPath !== null) {
      this.stepValues[step.fieldName] = folderPath;
      this.onValueChange(step, folderPath);
    }
  }

  saveAndClose(): void {
    if (!this.wizardConfig) {
      return;
    }

    // Validate all steps
    const visibleSteps = this.getVisibleSteps();
    for (const step of visibleSteps) {
      if (!this.isStepValid(step)) {
        // Go to first invalid step
        const invalidIndex = visibleSteps.findIndex(s => s.id === step.id);
        if (invalidIndex >= 0) {
          this.goToStep(invalidIndex);
        }
        return;
      }
    }

    // Transform wizard values to adapter settings
    const settings = this.wizardConfig.onComplete 
      ? this.wizardConfig.onComplete(this.stepValues)
      : this.stepValues;

    // Update settings in parent dialog
    this.data.onSettingsUpdate(settings);

    // Close wizard
    this.dialogRef.close();
  }

  cancel(): void {
    this.dialogRef.close();
  }

  getStepOptions(step: WizardStep): WizardOption[] {
    if (step.inputType === 'select' && step.fieldName === 'sqlServerName') {
      // Merge available servers with options
      const serverOptions = this.availableServers.map(server => ({
        value: server,
        label: server
      }));
      return [...serverOptions, ...(step.options || [])];
    }

    if (step.inputType === 'select' && step.fieldName === 'sapRfcFunctionModule') {
      // Merge available RFCs with options
      const rfcOptions = this.availableRfcs.map(rfc => ({
        value: rfc,
        label: rfc
      }));
      return [...rfcOptions, ...(step.options || [])];
    }

    return step.options || [];
  }
}

