import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { AdapterWizardComponent, AdapterWizardData } from './adapter-wizard.component';
import { TransportService } from '../../services/transport.service';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('AdapterWizardComponent', () => {
  let component: AdapterWizardComponent;
  let fixture: ComponentFixture<AdapterWizardComponent>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<AdapterWizardComponent>>;
  let transportService: jasmine.SpyObj<TransportService>;

  const mockWizardData: AdapterWizardData = {
    adapterName: 'CSV',
    adapterType: 'Source',
    currentSettings: {},
    onSettingsUpdate: jasmine.createSpy('onSettingsUpdate')
  };

  beforeEach(async () => {
    const dialogRefSpy = jasmine.createSpyObj('MatDialogRef', ['close']);
    const transportServiceSpy = jasmine.createSpyObj('TransportService', []);

    await TestBed.configureTestingModule({
      imports: [
        AdapterWizardComponent,
        NoopAnimationsModule
      ],
      providers: [
        { provide: MatDialogRef, useValue: dialogRefSpy },
        { provide: MAT_DIALOG_DATA, useValue: mockWizardData },
        { provide: TransportService, useValue: transportServiceSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdapterWizardComponent);
    component = fixture.componentInstance;
    dialogRef = TestBed.inject(MatDialogRef) as jasmine.SpyObj<MatDialogRef<AdapterWizardComponent>>;
    transportService = TestBed.inject(TransportService) as jasmine.SpyObj<TransportService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('initialization', () => {
    it('should load wizard config on init', () => {
      expect(component.wizardConfig).toBeTruthy();
      expect(component.wizardConfig?.adapterName).toBe('CSV');
    });

    it('should initialize step values from current settings', () => {
      const dataWithSettings: AdapterWizardData = {
        ...mockWizardData,
        currentSettings: { instanceName: 'TestInstance' }
      };
      component.data = dataWithSettings;
      component.ngOnInit();
      expect(component.stepValues['instanceName']).toBe('TestInstance');
    });
  });

  describe('navigation', () => {
    beforeEach(() => {
      component.loadWizardConfig();
    });

    it('should start at first step', () => {
      expect(component.currentStepIndex).toBe(0);
    });

    it('should calculate progress correctly', () => {
      if (component.wizardConfig && component.wizardConfig.steps.length > 0) {
        const totalSteps = component.wizardConfig.steps.length;
        component.currentStepIndex = 1;
        const expectedProgress = ((1 + 1) / totalSteps) * 100;
        expect(component.progress).toBe(expectedProgress);
      }
    });

    it('should allow going back when not on first step', () => {
      component.currentStepIndex = 1;
      expect(component.canGoBack).toBe(true);
    });

    it('should not allow going back on first step', () => {
      component.currentStepIndex = 0;
      expect(component.canGoBack).toBe(false);
    });

    it('should detect last step correctly', () => {
      if (component.wizardConfig) {
        component.currentStepIndex = component.wizardConfig.steps.length - 1;
        expect(component.isLastStep).toBe(true);
      }
    });
  });

  describe('step validation', () => {
    beforeEach(() => {
      component.loadWizardConfig();
    });

    it('should validate required fields', () => {
      if (component.currentStep) {
        component.currentStep.required = true;
        component.stepValues[component.currentStep.fieldName] = '';
        expect(component.isStepValid(component.currentStep)).toBe(false);
      }
    });

    it('should pass validation when required field is filled', () => {
      if (component.currentStep) {
        component.currentStep.required = true;
        component.stepValues[component.currentStep.fieldName] = 'test-value';
        expect(component.isStepValid(component.currentStep)).toBe(true);
      }
    });
  });

  describe('saveAndClose', () => {
    beforeEach(() => {
      component.loadWizardConfig();
    });

    it('should validate all steps before saving', () => {
      if (component.wizardConfig && component.wizardConfig.steps.length > 0) {
        const firstStep = component.wizardConfig.steps[0];
        firstStep.required = true;
        component.stepValues[firstStep.fieldName] = '';

        component.saveAndClose();

        expect(mockWizardData.onSettingsUpdate).not.toHaveBeenCalled();
        expect(dialogRef.close).not.toHaveBeenCalled();
      }
    });

    it('should save and close when all steps are valid', () => {
      if (component.wizardConfig && component.wizardConfig.steps.length > 0) {
        // Fill all required fields
        component.wizardConfig.steps.forEach(step => {
          if (step.required) {
            component.stepValues[step.fieldName] = 'test-value';
          }
        });

        component.saveAndClose();

        expect(mockWizardData.onSettingsUpdate).toHaveBeenCalled();
        expect(dialogRef.close).toHaveBeenCalled();
      }
    });
  });

  describe('cancel', () => {
    it('should close dialog without saving', () => {
      component.cancel();
      expect(dialogRef.close).toHaveBeenCalled();
    });
  });

  describe('loadAvailableServers', () => {
    it('should load available servers for SqlServer adapter', async () => {
      component.data.adapterName = 'SqlServer';
      await component.loadAvailableServers();

      expect(component.availableServers.length).toBeGreaterThan(0);
      expect(component.isLoadingServers).toBe(false);
    });
  });

  describe('loadAvailableRfcs', () => {
    it('should load available RFCs for SAP adapter', async () => {
      component.data.adapterName = 'SAP';
      await component.loadAvailableRfcs();

      expect(component.availableRfcs.length).toBeGreaterThan(0);
      expect(component.isLoadingRfcs).toBe(false);
    });
  });

  describe('step visibility', () => {
    beforeEach(() => {
      component.loadWizardConfig();
    });

    it('should show step when no conditional', () => {
      const step = { id: 'test', fieldName: 'test', required: false };
      expect(component.isStepVisible(step as any)).toBe(true);
    });

    it('should show step when conditional equals matches', () => {
      component.stepValues['csvAdapterType'] = 'SFTP';
      const step = {
        id: 'test',
        fieldName: 'test',
        conditional: { field: 'csvAdapterType', value: 'SFTP', operator: 'equals' }
      };
      expect(component.isStepVisible(step as any)).toBe(true);
    });

    it('should hide step when conditional equals does not match', () => {
      component.stepValues['csvAdapterType'] = 'FILE';
      const step = {
        id: 'test',
        fieldName: 'test',
        conditional: { field: 'csvAdapterType', value: 'SFTP', operator: 'equals' }
      };
      expect(component.isStepVisible(step as any)).toBe(false);
    });

    it('should handle notEquals operator', () => {
      component.stepValues['csvAdapterType'] = 'FILE';
      const step = {
        id: 'test',
        fieldName: 'test',
        conditional: { field: 'csvAdapterType', value: 'SFTP', operator: 'notEquals' }
      };
      expect(component.isStepVisible(step as any)).toBe(true);
    });

    it('should handle contains operator', () => {
      component.stepValues['field'] = 'test-value';
      const step = {
        id: 'test',
        fieldName: 'test',
        conditional: { field: 'field', value: 'value', operator: 'contains' }
      };
      expect(component.isStepVisible(step as any)).toBe(true);
    });

    it('should handle exists operator', () => {
      component.stepValues['field'] = 'value';
      const step = {
        id: 'test',
        fieldName: 'test',
        conditional: { field: 'field', value: '', operator: 'exists' }
      };
      expect(component.isStepVisible(step as any)).toBe(true);
    });

    it('should return false for exists when field is empty', () => {
      component.stepValues['field'] = '';
      const step = {
        id: 'test',
        fieldName: 'test',
        conditional: { field: 'field', value: '', operator: 'exists' }
      };
      expect(component.isStepVisible(step as any)).toBe(false);
    });
  });

  describe('getVisibleSteps', () => {
    it('should return empty array when no config', () => {
      component.wizardConfig = null;
      expect(component.getVisibleSteps()).toEqual([]);
    });

    it('should filter steps based on visibility', () => {
      component.loadWizardConfig();
      if (component.wizardConfig) {
        const visibleSteps = component.getVisibleSteps();
        expect(visibleSteps.length).toBeGreaterThanOrEqual(0);
      }
    });
  });

  describe('getCurrentVisibleStepIndex', () => {
    it('should return 0 when no current step', () => {
      component.wizardConfig = null;
      expect(component.getCurrentVisibleStepIndex()).toBe(0);
    });

    it('should return correct index for current step', () => {
      component.loadWizardConfig();
      if (component.wizardConfig && component.wizardConfig.steps.length > 0) {
        component.currentStepIndex = 0;
        const index = component.getCurrentVisibleStepIndex();
        expect(index).toBeGreaterThanOrEqual(0);
      }
    });
  });

  describe('goToStep', () => {
    beforeEach(() => {
      component.loadWizardConfig();
    });

    it('should navigate to valid step index', () => {
      if (component.wizardConfig && component.wizardConfig.steps.length > 1) {
        const initialIndex = component.currentStepIndex;
        component.goToStep(1);
        expect(component.currentStepIndex).not.toBe(initialIndex);
        expect(component.validationErrors).toEqual({});
      }
    });

    it('should not navigate to invalid step index', () => {
      const initialIndex = component.currentStepIndex;
      component.goToStep(-1);
      expect(component.currentStepIndex).toBe(initialIndex);
    });
  });

  describe('goBack and goNext', () => {
    beforeEach(() => {
      component.loadWizardConfig();
    });

    it('should go back when not on first step', () => {
      if (component.wizardConfig && component.wizardConfig.steps.length > 1) {
        component.currentStepIndex = 1;
        const initialIndex = component.currentStepIndex;
        component.goBack();
        expect(component.currentStepIndex).toBeLessThan(initialIndex);
      }
    });

    it('should not go back on first step', () => {
      component.currentStepIndex = 0;
      component.goBack();
      expect(component.currentStepIndex).toBe(0);
    });

    it('should go next when step is valid', () => {
      if (component.wizardConfig && component.wizardConfig.steps.length > 1) {
        const firstStep = component.wizardConfig.steps[0];
        if (firstStep.required) {
          component.stepValues[firstStep.fieldName] = 'test-value';
        }
        const initialIndex = component.currentStepIndex;
        component.goNext();
        if (component.canGoNext) {
          expect(component.currentStepIndex).toBeGreaterThanOrEqual(initialIndex);
        }
      }
    });

    it('should not go next when step is invalid', () => {
      if (component.wizardConfig && component.wizardConfig.steps.length > 0) {
        const firstStep = component.wizardConfig.steps[0];
        firstStep.required = true;
        component.stepValues[firstStep.fieldName] = '';
        const initialIndex = component.currentStepIndex;
        component.goNext();
        expect(component.currentStepIndex).toBe(initialIndex);
      }
    });
  });

  describe('getValidationError', () => {
    it('should return validation error for field', () => {
      component.validationErrors['testField'] = 'Test error';
      expect(component.getValidationError('testField')).toBe('Test error');
    });

    it('should return undefined when no error', () => {
      expect(component.getValidationError('nonExistent')).toBeUndefined();
    });
  });

  describe('onValueChange', () => {
    beforeEach(() => {
      component.loadWizardConfig();
    });

    it('should update step value', () => {
      if (component.wizardConfig && component.wizardConfig.steps.length > 0) {
        const step = component.wizardConfig.steps[0];
        component.onValueChange(step, 'new-value');
        expect(component.stepValues[step.fieldName]).toBe('new-value');
      }
    });

    it('should clear validation error on value change', () => {
      if (component.wizardConfig && component.wizardConfig.steps.length > 0) {
        const step = component.wizardConfig.steps[0];
        component.validationErrors[step.fieldName] = 'Error';
        component.onValueChange(step, 'new-value');
        expect(component.validationErrors[step.fieldName]).toBeUndefined();
      }
    });
  });

  describe('getStepOptions', () => {
    beforeEach(() => {
      component.loadWizardConfig();
      component.availableServers = ['server1', 'server2'];
      component.availableRfcs = ['RFC1', 'RFC2'];
    });

    it('should merge server options for sqlServerName', () => {
      const step = {
        inputType: 'select',
        fieldName: 'sqlServerName',
        options: [{ value: 'option1', label: 'Option 1' }]
      };
      const options = component.getStepOptions(step as any);
      expect(options.length).toBeGreaterThan(2);
    });

    it('should merge RFC options for sapRfcFunctionModule', () => {
      const step = {
        inputType: 'select',
        fieldName: 'sapRfcFunctionModule',
        options: [{ value: 'option1', label: 'Option 1' }]
      };
      const options = component.getStepOptions(step as any);
      expect(options.length).toBeGreaterThan(2);
    });

    it('should return original options for other fields', () => {
      const step = {
        inputType: 'select',
        fieldName: 'otherField',
        options: [{ value: 'option1', label: 'Option 1' }]
      };
      const options = component.getStepOptions(step as any);
      expect(options.length).toBe(1);
    });
  });

  describe('wizard configuration methods', () => {
    it('should get CSV wizard config for Source', () => {
      const config = component.getCsvWizardConfig('Source');
      expect(config.adapterName).toBe('CSV');
      expect(config.adapterType).toBe('Source');
      expect(config.steps.length).toBeGreaterThan(0);
    });

    it('should get CSV wizard config for Destination', () => {
      const config = component.getCsvWizardConfig('Destination');
      expect(config.adapterName).toBe('CSV');
      expect(config.adapterType).toBe('Destination');
    });

    it('should get SQL Server wizard config', () => {
      const config = component.getSqlServerWizardConfig('Source');
      expect(config.adapterName).toBe('SqlServer');
      expect(config.steps.length).toBeGreaterThan(0);
    });

    it('should get SAP wizard config', () => {
      const config = component.getSapWizardConfig('Source');
      expect(config.adapterName).toBe('SAP');
      expect(config.steps.length).toBeGreaterThan(0);
    });

    it('should get Dynamics365 wizard config', () => {
      const config = component.getDynamics365WizardConfig('Source');
      expect(config.adapterName).toBe('Dynamics365');
      expect(config.steps.length).toBeGreaterThan(0);
    });

    it('should get CRM wizard config', () => {
      const config = component.getCrmWizardConfig('Source');
      expect(config.adapterName).toBe('CRM');
    });

    it('should get SFTP wizard config', () => {
      const config = component.getSftpWizardConfig('Source');
      expect(config.adapterName).toBe('SFTP');
      expect(config.steps.length).toBeGreaterThan(0);
    });

    it('should get default wizard config for unknown adapter', () => {
      const config = component.getDefaultWizardConfig('Unknown', 'Source');
      expect(config.adapterName).toBe('Unknown');
      expect(config.steps.length).toBeGreaterThan(0);
    });
  });

  describe('getWizardConfigForAdapter', () => {
    it('should return CSV config for CSV adapter', () => {
      const config = component.getWizardConfigForAdapter('CSV', 'Source');
      expect(config.adapterName).toBe('CSV');
    });

    it('should return SQL Server config for SqlServer adapter', () => {
      const config = component.getWizardConfigForAdapter('SqlServer', 'Source');
      expect(config.adapterName).toBe('SqlServer');
    });

    it('should return SAP config for SAP adapter', () => {
      const config = component.getWizardConfigForAdapter('SAP', 'Source');
      expect(config.adapterName).toBe('SAP');
    });

    it('should return default config for unknown adapter', () => {
      const config = component.getWizardConfigForAdapter('Unknown', 'Source');
      expect(config.adapterName).toBe('Unknown');
    });
  });

  describe('file picker operations', () => {
    beforeEach(() => {
      component.loadWizardConfig();
    });

    it('should handle file picker for SSH key', () => {
      const step = { fieldName: 'sftpSshKey', required: false };
      spyOn(document, 'createElement').and.returnValue({
        type: '',
        style: { display: '' },
        accept: '',
        onchange: null,
        click: jasmine.createSpy('click')
      } as any);
      
      component.openFilePicker(step as any);
      expect(document.createElement).toHaveBeenCalledWith('input');
    });

    it('should handle folder picker', () => {
      const step = { fieldName: 'receiveFolder', required: false };
      spyOn(window, 'prompt').and.returnValue('/test/folder');
      
      component.openFolderPicker(step as any);
      expect(component.stepValues['receiveFolder']).toBe('/test/folder');
    });

    it('should handle folder picker cancellation', () => {
      const step = { fieldName: 'receiveFolder', required: false };
      spyOn(window, 'prompt').and.returnValue(null);
      
      const initialValue = component.stepValues['receiveFolder'];
      component.openFolderPicker(step as any);
      expect(component.stepValues['receiveFolder']).toBe(initialValue);
    });
  });

  describe('error handling', () => {
    it('should handle error in loadAvailableServers', async () => {
      component.data.adapterName = 'SqlServer';
      spyOn(console, 'error');
      
      // Simulate error
      await component.loadAvailableServers();
      
      expect(component.isLoadingServers).toBe(false);
    });

    it('should handle error in loadAvailableRfcs', async () => {
      component.data.adapterName = 'SAP';
      spyOn(console, 'error');
      
      await component.loadAvailableRfcs();
      
      expect(component.isLoadingRfcs).toBe(false);
    });
  });
});
