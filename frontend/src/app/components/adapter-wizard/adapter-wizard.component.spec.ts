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
});
