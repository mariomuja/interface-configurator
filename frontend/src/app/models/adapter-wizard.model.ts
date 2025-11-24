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

export interface AdapterWizardConfig {
  adapterName: string;
  adapterType: 'Source' | 'Destination';
  steps: WizardStep[];
  onStepComplete?: (stepId: string, value: any, allValues: Record<string, any>) => void;
  onComplete?: (allValues: Record<string, any>) => Record<string, any>; // Transform wizard values to adapter settings
}

export interface AdapterWizardData {
  adapterName: string;
  adapterType: 'Source' | 'Destination';
  currentSettings: Record<string, any>;
  onSettingsUpdate: (settings: Record<string, any>) => void;
}
