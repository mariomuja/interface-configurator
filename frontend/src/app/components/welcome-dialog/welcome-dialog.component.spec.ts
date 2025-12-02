import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogRef } from '@angular/material/dialog';
import { WelcomeDialogComponent } from './welcome-dialog.component';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('WelcomeDialogComponent', () => {
  let component: WelcomeDialogComponent;
  let fixture: ComponentFixture<WelcomeDialogComponent>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<WelcomeDialogComponent>>;

  beforeEach(async () => {
    const dialogRefSpy = jasmine.createSpyObj('MatDialogRef', ['close']);

    await TestBed.configureTestingModule({
      imports: [
        WelcomeDialogComponent,
        NoopAnimationsModule
      ],
      providers: [
        { provide: MatDialogRef, useValue: dialogRefSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WelcomeDialogComponent);
    component = fixture.componentInstance;
    dialogRef = TestBed.inject(MatDialogRef) as jasmine.SpyObj<MatDialogRef<WelcomeDialogComponent>>;
    fixture.detectChanges();
  });

  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('onClose', () => {
    it('should close dialog', () => {
      component.onClose();
      expect(dialogRef.close).toHaveBeenCalled();
    });
  });

  describe('onDontShowAgain', () => {
    it('should set localStorage flag and close dialog', () => {
      component.onDontShowAgain();
      
      expect(localStorage.getItem('welcomeDialogShown')).toBe('true');
      expect(dialogRef.close).toHaveBeenCalled();
    });

    it('should overwrite existing flag', () => {
      localStorage.setItem('welcomeDialogShown', 'false');
      
      component.onDontShowAgain();
      
      expect(localStorage.getItem('welcomeDialogShown')).toBe('true');
    });
  });
});
