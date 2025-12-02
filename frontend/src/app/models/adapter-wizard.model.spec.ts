import {
  WizardStep,
  WizardOption,
  AdapterWizardValues,
  AdapterWizardConfig,
  AdapterWizardData
} from './adapter-wizard.model';

describe('Adapter Wizard Models', () => {
  describe('WizardStep', () => {
    it('should have required properties', () => {
      const step: WizardStep = {
        id: 'test-step',
        title: 'Test Step',
        description: 'Test description',
        inputType: 'text',
        fieldName: 'testField'
      };

      expect(step.id).toBe('test-step');
      expect(step.title).toBe('Test Step');
      expect(step.description).toBe('Test description');
      expect(step.inputType).toBe('text');
      expect(step.fieldName).toBe('testField');
    });

    it('should support all input types', () => {
      const inputTypes: WizardStep['inputType'][] = [
        'text',
        'number',
        'select',
        'multichoice',
        'filepicker',
        'password',
        'toggle',
        'textarea'
      ];

      inputTypes.forEach(inputType => {
        const step: WizardStep = {
          id: 'test',
          title: 'Test',
          description: 'Test',
          inputType,
          fieldName: 'test'
        };
        expect(step.inputType).toBe(inputType);
      });
    });

    it('should support optional properties', () => {
      const step: WizardStep = {
        id: 'test',
        title: 'Test',
        description: 'Test',
        inputType: 'text',
        fieldName: 'test',
        placeholder: 'Enter value',
        required: true,
        helpText: 'Help text',
        defaultValue: 'default',
        min: 0,
        max: 100,
        step: 1
      };

      expect(step.placeholder).toBe('Enter value');
      expect(step.required).toBe(true);
      expect(step.helpText).toBe('Help text');
      expect(step.defaultValue).toBe('default');
      expect(step.min).toBe(0);
      expect(step.max).toBe(100);
      expect(step.step).toBe(1);
    });

    it('should support validation function', () => {
      const step: WizardStep = {
        id: 'test',
        title: 'Test',
        description: 'Test',
        inputType: 'text',
        fieldName: 'test',
        validation: (value: any) => {
          return value.length > 5
            ? { valid: true }
            : { valid: false, error: 'Too short' };
        }
      };

      expect(step.validation).toBeDefined();
      expect(step.validation!('short')).toEqual({ valid: false, error: 'Too short' });
      expect(step.validation!('long enough')).toEqual({ valid: true });
    });

    it('should support conditional logic', () => {
      const step: WizardStep = {
        id: 'test',
        title: 'Test',
        description: 'Test',
        inputType: 'text',
        fieldName: 'test',
        conditional: {
          field: 'adapterType',
          value: 'CSV',
          operator: 'equals'
        }
      };

      expect(step.conditional).toBeDefined();
      expect(step.conditional!.field).toBe('adapterType');
      expect(step.conditional!.value).toBe('CSV');
      expect(step.conditional!.operator).toBe('equals');
    });

    it('should support all conditional operators', () => {
      const operators: Array<'equals' | 'notEquals' | 'contains' | 'exists'> = [
        'equals',
        'notEquals',
        'contains',
        'exists'
      ];

      operators.forEach(operator => {
        const step: WizardStep = {
          id: 'test',
          title: 'Test',
          description: 'Test',
          inputType: 'text',
          fieldName: 'test',
          conditional: {
            field: 'test',
            value: 'value',
            operator
          }
        };
        expect(step.conditional!.operator).toBe(operator);
      });
    });

    it('should support options for select input', () => {
      const step: WizardStep = {
        id: 'test',
        title: 'Test',
        description: 'Test',
        inputType: 'select',
        fieldName: 'test',
        options: [
          { value: 'option1', label: 'Option 1' },
          { value: 'option2', label: 'Option 2' }
        ]
      };

      expect(step.options).toBeDefined();
      expect(step.options!.length).toBe(2);
    });
  });

  describe('WizardOption', () => {
    it('should have required properties', () => {
      const option: WizardOption = {
        value: 'test',
        label: 'Test Option'
      };

      expect(option.value).toBe('test');
      expect(option.label).toBe('Test Option');
    });

    it('should support optional properties', () => {
      const option: WizardOption = {
        value: 'test',
        label: 'Test Option',
        description: 'Test description',
        icon: 'test-icon'
      };

      expect(option.description).toBe('Test description');
      expect(option.icon).toBe('test-icon');
    });
  });

  describe('AdapterWizardValues', () => {
    it('should support CSV adapter properties', () => {
      const values: AdapterWizardValues = {
        csvAdapterType: 'FILE',
        fieldSeparator: ',',
        batchSize: 100,
        receiveFolder: '/folder',
        fileMask: '*.csv',
        csvData: 'data',
        csvPollingInterval: 10
      };

      expect(values.csvAdapterType).toBe('FILE');
      expect(values.fieldSeparator).toBe(',');
      expect(values.batchSize).toBe(100);
    });

    it('should support SFTP properties', () => {
      const values: AdapterWizardValues = {
        sftpHost: 'host.com',
        sftpPort: 22,
        sftpUsername: 'user',
        sftpAuthMethod: 'password',
        sftpPassword: 'pass',
        sftpFolder: '/remote',
        sftpFileMask: '*.csv'
      };

      expect(values.sftpHost).toBe('host.com');
      expect(values.sftpPort).toBe(22);
      expect(values.sftpAuthMethod).toBe('password');
    });

    it('should support SQL Server properties', () => {
      const values: AdapterWizardValues = {
        sqlServerName: 'server',
        sqlDatabaseName: 'db',
        sqlAuthMethod: 'integrated',
        sqlUseTransaction: true,
        sqlBatchSize: 1000,
        tableName: 'Table',
        sqlPollingStatement: 'SELECT * FROM Table',
        sqlPollingInterval: 60
      };

      expect(values.sqlServerName).toBe('server');
      expect(values.sqlAuthMethod).toBe('integrated');
      expect(values.sqlUseTransaction).toBe(true);
    });

    it('should support SAP properties', () => {
      const values: AdapterWizardValues = {
        sapConnectionType: 'rfc',
        sapApplicationServer: 'server',
        sapSystemNumber: '00',
        sapClient: '100',
        sapUsername: 'user',
        sapPassword: 'pass',
        sapRfcFunctionModule: 'RFC_FUNCTION'
      };

      expect(values.sapConnectionType).toBe('rfc');
      expect(values.sapRfcFunctionModule).toBe('RFC_FUNCTION');
    });

    it('should support Dynamics365 properties', () => {
      const values: AdapterWizardValues = {
        dynamics365InstanceUrl: 'https://instance.crm.dynamics.com',
        dynamics365TenantId: 'tenant-id',
        dynamics365ClientId: 'client-id',
        dynamics365EntityName: 'Entity',
        dynamics365BatchSize: 100
      };

      expect(values.dynamics365InstanceUrl).toBeTruthy();
      expect(values.dynamics365EntityName).toBe('Entity');
    });

    it('should support dynamic properties', () => {
      const values: AdapterWizardValues = {
        customProperty: 'custom value',
        anotherProperty: 123
      };

      expect(values.customProperty).toBe('custom value');
      expect(values.anotherProperty).toBe(123);
    });
  });

  describe('AdapterWizardConfig', () => {
    it('should have required properties', () => {
      const config: AdapterWizardConfig = {
        adapterName: 'CSV',
        adapterType: 'Source',
        steps: []
      };

      expect(config.adapterName).toBe('CSV');
      expect(config.adapterType).toBe('Source');
      expect(config.steps).toEqual([]);
    });

    it('should support optional callbacks', () => {
      const onStepComplete = jest.fn();
      const onComplete = jest.fn();

      const config: AdapterWizardConfig = {
        adapterName: 'CSV',
        adapterType: 'Source',
        steps: [],
        onStepComplete,
        onComplete
      };

      expect(config.onStepComplete).toBe(onStepComplete);
      expect(config.onComplete).toBe(onComplete);
    });
  });

  describe('AdapterWizardData', () => {
    it('should have required properties', () => {
      const onSettingsUpdate = jest.fn();
      const data: AdapterWizardData = {
        adapterName: 'CSV',
        adapterType: 'Source',
        currentSettings: {},
        onSettingsUpdate
      };

      expect(data.adapterName).toBe('CSV');
      expect(data.adapterType).toBe('Source');
      expect(data.onSettingsUpdate).toBe(onSettingsUpdate);
    });
  });
});
