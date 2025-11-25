/**
 * Models for Adapter Configuration Wizard
 */

export interface WizardStep {
  id: string;
  title: string;
  description: string;
  inputType: 'text' | 'number' | 'select' | 'multichoice' | 'filepicker' | 'password' | 'toggle' | 'textarea';
  fieldName: string;
  placeholder?: string;
  required?: boolean;
  options?: WizardOption[];
  validation?: (value: any) => { valid: boolean; error?: string };
  helpText?: string;
  conditional?: {
    field: string;
    value: any;
    operator?: 'equals' | 'notEquals' | 'contains' | 'exists';
  };
  defaultValue?: any;
  min?: number;
  max?: number;
  step?: number;
}

export interface WizardOption {
  value: any;
  label: string;
  description?: string;
  icon?: string;
}

export interface AdapterWizardValues {
  csvAdapterType?: 'RAW' | 'FILE' | 'SFTP';
  fieldSeparator?: string;
  batchSize?: number;
  receiveFolder?: string;
  fileMask?: string;
  csvData?: string;
  csvPollingInterval?: number;
  destinationReceiveFolder?: string;
  destinationFileMask?: string;
  sftpHost?: string;
  sftpPort?: number;
  sftpUsername?: string;
  sftpAuthMethod?: 'password' | 'sshkey';
  sftpPassword?: string;
  sftpSshKey?: string;
  sftpFolder?: string;
  sftpFileMask?: string;
  sqlServerName?: string;
  sqlDatabaseName?: string;
  sqlAuthMethod?: 'integrated' | 'sql' | 'managed';
  sqlResourceGroup?: string;
  sqlUseTransaction?: boolean;
  sqlBatchSize?: number;
  tableName?: string;
  sqlUserName?: string;
  sqlPassword?: string;
  sqlPollingStatement?: string;
  sqlPollingInterval?: number;
  sapConnectionType?: 'rfc' | 'odata' | 'rest';
  sapApplicationServer?: string;
  sapSystemNumber?: string;
  sapClient?: string;
  sapUsername?: string;
  sapPassword?: string;
  sapRfcFunctionModule?: string;
  sapIdocType?: string;
  sapODataServiceUrl?: string;
  sapRestApiEndpoint?: string;
  dynamics365InstanceUrl?: string;
  dynamics365TenantId?: string;
  dynamics365ClientId?: string;
  dynamics365ClientSecret?: string;
  dynamics365EntityName?: string;
  dynamics365ODataFilter?: string;
  dynamics365BatchSize?: number;
  [key: string]: any;
}

export interface AdapterWizardConfig {
  adapterName: string;
  adapterType: 'Source' | 'Destination';
  steps: WizardStep[];
  onStepComplete?: (stepId: string, value: any, allValues: AdapterWizardValues) => void;
  onComplete?: (allValues: AdapterWizardValues) => Record<string, any>; // Transform wizard values to adapter settings
}

export interface AdapterWizardData {
  adapterName: string;
  adapterType: 'Source' | 'Destination';
  currentSettings: AdapterWizardValues;
  onSettingsUpdate: (settings: Record<string, any>) => void;
}
