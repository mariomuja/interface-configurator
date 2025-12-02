import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { ContainerAppProgressDialogComponent, ContainerAppProgressData } from './container-app-progress-dialog.component';
import { TransportService } from '../../services/transport.service';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('ContainerAppProgressDialogComponent', () => {
  let component: ContainerAppProgressDialogComponent;
  let fixture: ComponentFixture<ContainerAppProgressDialogComponent>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<ContainerAppProgressDialogComponent>>;
  let transportService: jasmine.SpyObj<TransportService>;

  const mockData: ContainerAppProgressData = {
    adapterInstanceGuid: 'test-guid',
    adapterName: 'CSV',
    adapterType: 'Source',
    interfaceName: 'TestInterface',
    instanceName: 'TestInstance'
  };

  beforeEach(async () => {
    const dialogRefSpy = jasmine.createSpyObj('MatDialogRef', ['close']);
    const transportServiceSpy = jasmine.createSpyObj('TransportService', []);

    await TestBed.configureTestingModule({
      imports: [
        ContainerAppProgressDialogComponent,
        NoopAnimationsModule
      ],
      providers: [
        { provide: MatDialogRef, useValue: dialogRefSpy },
        { provide: MAT_DIALOG_DATA, useValue: mockData },
        { provide: TransportService, useValue: transportServiceSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ContainerAppProgressDialogComponent);
    component = fixture.componentInstance;
    dialogRef = TestBed.inject(MatDialogRef) as jasmine.SpyObj<MatDialogRef<ContainerAppProgressDialogComponent>>;
    transportService = TestBed.inject(TransportService) as jasmine.SpyObj<TransportService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('progress calculation', () => {
    it('should calculate progress percentage correctly', () => {
      component.steps = [
        { id: '1', label: 'Step 1', status: 'completed' },
        { id: '2', label: 'Step 2', status: 'in-progress' },
        { id: '3', label: 'Step 3', status: 'pending' }
      ];
      
      const progress = component.progressPercentage;
      expect(progress).toBeGreaterThanOrEqual(0);
      expect(progress).toBeLessThanOrEqual(100);
    });
  });

  describe('step icons', () => {
    it('should return correct icon for pending status', () => {
      expect(component.getStepIcon('pending')).toBe('schedule');
    });

    it('should return correct icon for in-progress status', () => {
      expect(component.getStepIcon('in-progress')).toBe('hourglass_empty');
    });

    it('should return correct icon for completed status', () => {
      expect(component.getStepIcon('completed')).toBe('check_circle');
    });

    it('should return correct icon for error status', () => {
      expect(component.getStepIcon('error')).toBe('error');
    });
  });

  describe('step icon classes', () => {
    it('should return correct class for pending status', () => {
      expect(component.getStepIconClass('pending')).toContain('pending');
    });

    it('should return correct class for completed status', () => {
      expect(component.getStepIconClass('completed')).toContain('completed');
    });
  });

  describe('error handling', () => {
    it('should detect errors', () => {
      component.steps = [
        { id: '1', label: 'Step 1', status: 'error' }
      ];
      
      expect(component.hasErrors).toBe(true);
    });

    it('should collect error messages', () => {
      component.steps = [
        { id: '1', label: 'Step 1', status: 'error', message: 'Error message' }
      ];
      
      expect(component.errorMessages.length).toBeGreaterThan(0);
    });
  });

  describe('close', () => {
    it('should close dialog', () => {
      component.close();
      expect(dialogRef.close).toHaveBeenCalled();
    });
  });
});
