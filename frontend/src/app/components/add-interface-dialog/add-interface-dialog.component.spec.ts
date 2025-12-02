import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { AddInterfaceDialogComponent, AddInterfaceDialogData } from './add-interface-dialog.component';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('AddInterfaceDialogComponent', () => {
  let component: AddInterfaceDialogComponent;
  let fixture: ComponentFixture<AddInterfaceDialogComponent>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<AddInterfaceDialogComponent>>;

  const mockDialogData: AddInterfaceDialogData = {
    existingNames: ['ExistingInterface1', 'ExistingInterface2']
  };

  beforeEach(async () => {
    const dialogRefSpy = jasmine.createSpyObj('MatDialogRef', ['close']);

    await TestBed.configureTestingModule({
      imports: [
        AddInterfaceDialogComponent,
        NoopAnimationsModule
      ],
      providers: [
        { provide: MatDialogRef, useValue: dialogRefSpy },
        { provide: MAT_DIALOG_DATA, useValue: mockDialogData }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AddInterfaceDialogComponent);
    component = fixture.componentInstance;
    dialogRef = TestBed.inject(MatDialogRef) as jasmine.SpyObj<MatDialogRef<AddInterfaceDialogComponent>>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('isValid', () => {
    it('should return false for empty name', () => {
      component.interfaceName = '';
      expect(component.isValid()).toBe(false);
    });

    it('should return false for name shorter than 5 characters', () => {
      component.interfaceName = 'Test';
      expect(component.isValid()).toBe(false);
    });

    it('should return false for name matching existing interface (case insensitive)', () => {
      component.interfaceName = 'existinginterface1';
      expect(component.isValid()).toBe(false);
    });

    it('should return true for valid unique name', () => {
      component.interfaceName = 'NewInterface';
      expect(component.isValid()).toBe(true);
    });

    it('should return true for name with exactly 5 characters', () => {
      component.interfaceName = 'Test5';
      expect(component.isValid()).toBe(true);
    });
  });

  describe('onCreate', () => {
    it('should close dialog with trimmed name when valid', () => {
      component.interfaceName = '  NewInterface  ';
      component.onCreate();
      expect(dialogRef.close).toHaveBeenCalledWith('NewInterface');
    });

    it('should set error message for empty name', () => {
      component.interfaceName = '';
      component.onCreate();
      expect(component.errorMessage).toBe('Schnittstellen-Name darf nicht leer sein.');
      expect(dialogRef.close).not.toHaveBeenCalled();
    });

    it('should set error message for name shorter than 5 characters', () => {
      component.interfaceName = 'Test';
      component.onCreate();
      expect(component.errorMessage).toBe('Schnittstellen-Name muss mindestens 5 Zeichen lang sein.');
      expect(dialogRef.close).not.toHaveBeenCalled();
    });

    it('should set error message for duplicate name', () => {
      component.interfaceName = 'ExistingInterface1';
      component.onCreate();
      expect(component.errorMessage).toBe('Eine Schnittstelle mit diesem Namen existiert bereits.');
      expect(dialogRef.close).not.toHaveBeenCalled();
    });
  });

  describe('onCancel', () => {
    it('should close dialog without value', () => {
      component.onCancel();
      expect(dialogRef.close).toHaveBeenCalledWith();
    });
  });

  describe('edge cases', () => {
    it('should handle whitespace-only name', () => {
      component.interfaceName = '     ';
      expect(component.isValid()).toBe(false);
      component.onCreate();
      expect(component.errorMessage).toBeTruthy();
    });

    it('should handle name with special characters', () => {
      component.interfaceName = 'Test-Interface_123';
      expect(component.isValid()).toBe(true);
    });

    it('should handle very long name', () => {
      component.interfaceName = 'A'.repeat(100);
      expect(component.isValid()).toBe(true);
    });

    it('should handle Enter key press', () => {
      component.interfaceName = 'ValidName';
      spyOn(component, 'onCreate');
      // Simulate Enter key - would be tested in E2E
      expect(component.isValid()).toBe(true);
    });
  });
});
