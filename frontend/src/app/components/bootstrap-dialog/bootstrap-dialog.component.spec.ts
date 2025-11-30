import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { BootstrapDialogComponent, BootstrapDialogData } from './bootstrap-dialog.component';
import { BootstrapService, BootstrapResult } from '../../services/bootstrap.service';
import { TransportService } from '../../services/transport.service';
import { of, throwError, delay } from 'rxjs';

describe('BootstrapDialogComponent', () => {
  let component: BootstrapDialogComponent;
  let fixture: ComponentFixture<BootstrapDialogComponent>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<BootstrapDialogComponent>>;
  let bootstrapService: jasmine.SpyObj<BootstrapService>;
  let transportService: jasmine.SpyObj<TransportService>;

  const mockBootstrapResult: BootstrapResult = {
    timestamp: new Date().toISOString(),
    overallStatus: 'Healthy',
    healthyChecks: 5,
    totalChecks: 5,
    checks: [
      {
        name: 'Database',
        status: 'Healthy',
        message: 'Database connection successful'
      },
      {
        name: 'Blob Storage',
        status: 'Healthy',
        message: 'Blob Storage accessible'
      }
    ]
  };

  beforeEach(async () => {
    const dialogRefSpy = jasmine.createSpyObj('MatDialogRef', ['close']);
    const bootstrapServiceSpy = jasmine.createSpyObj('BootstrapService', ['runBootstrapAndRefreshLogs']);
    const transportServiceSpy = jasmine.createSpyObj('TransportService', ['loadProcessLogs']);

    await TestBed.configureTestingModule({
      imports: [
        BootstrapDialogComponent,
        MatDialogModule,
        NoopAnimationsModule
      ],
      providers: [
        { provide: MatDialogRef, useValue: dialogRefSpy },
        { provide: MAT_DIALOG_DATA, useValue: {} as BootstrapDialogData },
        { provide: BootstrapService, useValue: bootstrapServiceSpy },
        { provide: TransportService, useValue: transportServiceSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BootstrapDialogComponent);
    component = fixture.componentInstance;
    dialogRef = TestBed.inject(MatDialogRef) as jasmine.SpyObj<MatDialogRef<BootstrapDialogComponent>>;
    bootstrapService = TestBed.inject(BootstrapService) as jasmine.SpyObj<BootstrapService>;
    transportService = TestBed.inject(TransportService) as jasmine.SpyObj<TransportService>;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should auto-run bootstrap on init', () => {
    bootstrapService.runBootstrapAndRefreshLogs.and.returnValue(of(mockBootstrapResult));
    
    fixture.detectChanges();
    
    expect(bootstrapService.runBootstrapAndRefreshLogs).toHaveBeenCalled();
  });

  it('should not auto-run if autoRun is false', () => {
    component.data = { autoRun: false };
    fixture.detectChanges();
    
    // Should not call bootstrap immediately
    expect(bootstrapService.runBootstrapAndRefreshLogs).not.toHaveBeenCalled();
  });

  it('should display loading state while bootstrap runs', (done) => {
    // Create a delayed observable to simulate async operation
    bootstrapService.runBootstrapAndRefreshLogs.and.returnValue(
      of(mockBootstrapResult).pipe(delay(100))
    );
    
    component.runBootstrap();
    fixture.detectChanges();
    
    // Check immediately - should be loading
    expect(component.isLoading).toBe(true);
    
    // Wait for completion
    setTimeout(() => {
      expect(component.isLoading).toBe(false);
      done();
    }, 200);
  });

  it('should display bootstrap results after completion', (done) => {
    bootstrapService.runBootstrapAndRefreshLogs.and.returnValue(of(mockBootstrapResult));
    
    component.runBootstrap();
    
    setTimeout(() => {
      expect(component.bootstrapResult).toEqual(mockBootstrapResult);
      expect(component.isLoading).toBe(false);
      expect(component.error).toBeNull();
      done();
    }, 100);
  });

  it('should handle bootstrap errors', (done) => {
    const error = new Error('Bootstrap failed');
    bootstrapService.runBootstrapAndRefreshLogs.and.returnValue(throwError(() => error));
    
    component.runBootstrap();
    
    setTimeout(() => {
      expect(component.error).toContain('Bootstrap-Fehler');
      expect(component.isLoading).toBe(false);
      expect(component.bootstrapResult).toBeNull();
      done();
    }, 100);
  });

  it('should get correct status icon', () => {
    expect(component.getStatusIcon('Healthy')).toBe('check_circle');
    expect(component.getStatusIcon('Degraded')).toBe('warning');
    expect(component.getStatusIcon('Unhealthy')).toBe('error');
    expect(component.getStatusIcon('Unknown')).toBe('help');
  });

  it('should get correct status text', () => {
    expect(component.getStatusText('Healthy')).toBe('Gesund');
    expect(component.getStatusText('Degraded')).toBe('Beeinträchtigt');
    expect(component.getStatusText('Unhealthy')).toBe('Fehlerhaft');
    expect(component.getStatusText('Unknown')).toBe('Unknown');
  });

  it('should close dialog with result', () => {
    component.bootstrapResult = mockBootstrapResult;
    component.onClose();
    
    expect(dialogRef.close).toHaveBeenCalledWith(mockBootstrapResult);
  });

  it('should call onComplete callback if provided', (done) => {
    const onCompleteSpy = jasmine.createSpy('onComplete');
    component.data = { onComplete: onCompleteSpy };
    bootstrapService.runBootstrapAndRefreshLogs.and.returnValue(of(mockBootstrapResult));
    
    component.runBootstrap();
    
    setTimeout(() => {
      expect(onCompleteSpy).toHaveBeenCalledWith(mockBootstrapResult);
      done();
    }, 1600);
  });
});

