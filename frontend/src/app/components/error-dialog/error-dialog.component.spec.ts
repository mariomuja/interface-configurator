import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ErrorDialogComponent, ErrorDialogData } from './error-dialog.component';
import { ErrorTrackingService } from '../../services/error-tracking.service';

describe('ErrorDialogComponent', () => {
  let component: ErrorDialogComponent;
  let fixture: ComponentFixture<ErrorDialogComponent>;
  let errorTrackingService: ErrorTrackingService;
  let mockDialogRef: jasmine.SpyObj<MatDialogRef<ErrorDialogComponent>>;

  const mockErrorData: ErrorDialogData = {
    error: new Error('Test error message'),
    functionName: 'testFunction',
    component: 'TestComponent',
    context: { test: 'value' }
  };

  beforeEach(async () => {
    mockDialogRef = jasmine.createSpyObj('MatDialogRef', ['close']);

    await TestBed.configureTestingModule({
      imports: [
        ErrorDialogComponent,
        MatDialogModule,
        MatSnackBarModule,
        HttpClientTestingModule
      ],
      providers: [
        { provide: MatDialogRef, useValue: mockDialogRef },
        { provide: MAT_DIALOG_DATA, useValue: mockErrorData },
        ErrorTrackingService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ErrorDialogComponent);
    component = fixture.componentInstance;
    errorTrackingService = TestBed.inject(ErrorTrackingService);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should display error message', () => {
    expect(component.errorMessage).toBe('Test error message');
  });

  it('should display function name', () => {
    expect(component.functionName).toBe('testFunction');
  });

  it('should display component name', () => {
    expect(component.component).toBe('TestComponent');
  });

  it('should close dialog', () => {
    component.close();
    expect(mockDialogRef.close).toHaveBeenCalled();
  });

  it('should copy error details to clipboard', async () => {
    spyOn(navigator.clipboard, 'writeText').and.returnValue(Promise.resolve());
    spyOn(component['snackBar'], 'open');

    await component.copyErrorDetails();

    expect(navigator.clipboard.writeText).toHaveBeenCalled();
    expect(component['snackBar'].open).toHaveBeenCalled();
  });

  it('should download error report', () => {
    spyOn(document, 'createElement').and.callThrough();
    spyOn(document.body, 'appendChild');
    spyOn(document.body, 'removeChild');
    spyOn(window.URL, 'createObjectURL').and.returnValue('blob:url');
    spyOn(window.URL, 'revokeObjectURL');

    component.downloadErrorReport();

    expect(document.createElement).toHaveBeenCalledWith('a');
  });

  it('should submit error to AI', () => {
    spyOn(errorTrackingService, 'submitErrorToAI').and.returnValue({
      subscribe: (callbacks: any) => {
        callbacks.next({ success: true });
        return { unsubscribe: () => {} };
      }
    } as any);
    spyOn(component['snackBar'], 'open');
    spyOn(component, 'close');

    component.submitToAI();

    expect(errorTrackingService.submitErrorToAI).toHaveBeenCalled();
  });
});

