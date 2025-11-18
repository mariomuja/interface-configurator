import { ComponentFixture, TestBed } from '@angular/core/testing';
import { InterfaceJsonViewDialogComponent, InterfaceJsonViewData } from './interface-json-view-dialog.component';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { provideAnimations } from '@angular/platform-browser/animations';

describe('InterfaceJsonViewDialogComponent', () => {
  let component: InterfaceJsonViewDialogComponent;
  let fixture: ComponentFixture<InterfaceJsonViewDialogComponent>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<InterfaceJsonViewDialogComponent>>;
  const mockData: InterfaceJsonViewData = {
    interfaceName: 'TestInterface',
    jsonString: '{"name": "TestInterface", "source": "CSV"}'
  };

  beforeEach(async () => {
    const dialogRefSpy = jasmine.createSpyObj('MatDialogRef', ['close']);

    await TestBed.configureTestingModule({
      imports: [InterfaceJsonViewDialogComponent],
      providers: [
        provideAnimations(),
        { provide: MatDialogRef, useValue: dialogRefSpy },
        { provide: MAT_DIALOG_DATA, useValue: mockData }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(InterfaceJsonViewDialogComponent);
    component = fixture.componentInstance;
    dialogRef = TestBed.inject(MatDialogRef) as jasmine.SpyObj<MatDialogRef<InterfaceJsonViewDialogComponent>>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with dialog data', () => {
    expect(component.data.interfaceName).toBe('TestInterface');
    expect(component.data.jsonString).toBe('{"name": "TestInterface", "source": "CSV"}');
  });

  it('should close dialog on close', () => {
    component.onClose();
    expect(dialogRef.close).toHaveBeenCalled();
  });

  it('should copy JSON to clipboard', async () => {
    const clipboardSpy = spyOn(navigator.clipboard, 'writeText').and.returnValue(Promise.resolve());
    
    component.copyToClipboard();
    
    await fixture.whenStable();
    expect(clipboardSpy).toHaveBeenCalledWith('{"name": "TestInterface", "source": "CSV"}');
  });

  it('should handle clipboard copy error', async () => {
    const consoleErrorSpy = spyOn(console, 'error');
    const error = new Error('Clipboard error');
    const clipboardSpy = spyOn(navigator.clipboard, 'writeText').and.returnValue(Promise.reject(error));
    
    component.copyToClipboard();
    
    // Wait for the promise to reject
    await new Promise(resolve => setTimeout(resolve, 10));
    
    expect(clipboardSpy).toHaveBeenCalled();
    expect(consoleErrorSpy).toHaveBeenCalledWith('Failed to copy to clipboard:', error);
  });

  it('should handle empty JSON string', () => {
    const emptyData: InterfaceJsonViewData = {
      interfaceName: 'EmptyInterface',
      jsonString: ''
    };
    
    component.data = emptyData;
    fixture.detectChanges();
    
    expect(component.data.jsonString).toBe('');
  });
});

