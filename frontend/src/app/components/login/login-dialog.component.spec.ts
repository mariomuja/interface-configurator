import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { LoginDialogComponent } from './login-dialog.component';
import { AuthService, LoginResponse } from '../../services/auth.service';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';

describe('LoginDialogComponent', () => {
  let component: LoginDialogComponent;
  let fixture: ComponentFixture<LoginDialogComponent>;
  let authService: jasmine.SpyObj<AuthService>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<LoginDialogComponent>>;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(async () => {
    const authServiceSpy = jasmine.createSpyObj('AuthService', ['login']);
    const dialogRefSpy = jasmine.createSpyObj('MatDialogRef', ['close']);
    const snackBarSpy = jasmine.createSpyObj('MatSnackBar', ['open']);

    await TestBed.configureTestingModule({
      imports: [
        LoginDialogComponent,
        NoopAnimationsModule
      ],
      providers: [
        { provide: AuthService, useValue: authServiceSpy },
        { provide: MatDialogRef, useValue: dialogRefSpy },
        { provide: MatSnackBar, useValue: snackBarSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LoginDialogComponent);
    component = fixture.componentInstance;
    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    dialogRef = TestBed.inject(MatDialogRef) as jasmine.SpyObj<MatDialogRef<LoginDialogComponent>>;
    snackBar = TestBed.inject(MatSnackBar) as jasmine.SpyObj<MatSnackBar>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('loginAsDemo', () => {
    it('should login as demo user successfully', () => {
      const mockResponse: LoginResponse = {
        success: true,
        token: 'demo-token',
        user: { id: 0, username: 'test', role: 'user' }
      };
      authService.login.and.returnValue(of(mockResponse));

      component.loginAsDemo();

      expect(authService.login).toHaveBeenCalledWith('test', '');
      expect(component.loggingIn).toBe(false);
      expect(component.isDemoLogin).toBe(false);
      expect(snackBar.open).toHaveBeenCalled();
      expect(dialogRef.close).toHaveBeenCalledWith(true);
    });

    it('should handle demo login failure', () => {
      const mockResponse: LoginResponse = {
        success: false,
        errorMessage: 'Demo login failed'
      };
      authService.login.and.returnValue(of(mockResponse));

      component.loginAsDemo();

      expect(component.errorMessage).toBe('Demo login failed');
      expect(dialogRef.close).not.toHaveBeenCalled();
    });

    it('should handle demo login error', () => {
      authService.login.and.returnValue(throwError(() => ({ status: 500, message: 'Server error' })));

      component.loginAsDemo();

      expect(component.loggingIn).toBe(false);
      expect(component.errorMessage).toContain('Demo-Anmeldung fehlgeschlagen');
      expect(snackBar.open).toHaveBeenCalled();
    });
  });

  describe('login', () => {
    it('should show error if username or password is missing', () => {
      component.username = '';
      component.password = '';

      component.login();

      expect(component.errorMessage).toBe('Bitte geben Sie Benutzername und Passwort ein');
      expect(authService.login).not.toHaveBeenCalled();
    });

    it('should login successfully with valid credentials', () => {
      const mockResponse: LoginResponse = {
        success: true,
        token: 'token',
        user: { id: 1, username: 'admin', role: 'admin' }
      };
      authService.login.and.returnValue(of(mockResponse));
      component.username = 'admin';
      component.password = 'password';

      component.login();

      expect(authService.login).toHaveBeenCalledWith('admin', 'password');
      expect(component.loggingIn).toBe(false);
      expect(snackBar.open).toHaveBeenCalled();
      expect(dialogRef.close).toHaveBeenCalledWith(true);
    });

    it('should handle login failure', () => {
      const mockResponse: LoginResponse = {
        success: false,
        errorMessage: 'Invalid credentials'
      };
      authService.login.and.returnValue(of(mockResponse));
      component.username = 'admin';
      component.password = 'wrong';

      component.login();

      expect(component.errorMessage).toBe('Invalid credentials');
      expect(dialogRef.close).not.toHaveBeenCalled();
    });

    it('should handle login error', () => {
      authService.login.and.returnValue(throwError(() => ({ status: 401, error: { errorMessage: 'Unauthorized' } })));
      component.username = 'admin';
      component.password = 'password';

      component.login();

      expect(component.loggingIn).toBe(false);
      expect(component.errorMessage).toBeTruthy();
      expect(snackBar.open).toHaveBeenCalled();
    });
  });

  describe('close', () => {
    it('should close dialog with false', () => {
      component.close();
      expect(dialogRef.close).toHaveBeenCalledWith(false);
    });
  });

  describe('error handling edge cases', () => {
    it('should handle network timeout error', () => {
      authService.login.and.returnValue(throwError(() => ({ status: 0, message: 'Timeout' })));
      component.username = 'admin';
      component.password = 'password';

      component.login();

      expect(component.errorMessage).toBeTruthy();
      expect(component.loggingIn).toBe(false);
    });

    it('should handle 403 forbidden error', () => {
      authService.login.and.returnValue(throwError(() => ({ status: 403, error: { errorMessage: 'Forbidden' } })));
      component.username = 'admin';
      component.password = 'password';

      component.login();

      expect(component.errorMessage).toBeTruthy();
    });

    it('should handle error with nested error object', () => {
      authService.login.and.returnValue(throwError(() => ({ 
        status: 500, 
        error: { error: { message: 'Nested error' } } 
      })));
      component.username = 'admin';
      component.password = 'password';

      component.login();

      expect(component.errorMessage).toBeTruthy();
    });

    it('should handle error with string message', () => {
      authService.login.and.returnValue(throwError(() => ({ 
        status: 500, 
        error: 'String error message' 
      })));
      component.username = 'admin';
      component.password = 'password';

      component.login();

      expect(component.errorMessage).toBeTruthy();
    });
  });
});
