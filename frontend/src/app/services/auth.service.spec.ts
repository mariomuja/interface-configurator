import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { AuthService, User, LoginResponse } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    // Clear localStorage before each test
    localStorage.clear();

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [AuthService]
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('login', () => {
    it('should login successfully and store token and user', () => {
      const mockResponse: LoginResponse = {
        success: true,
        token: 'test-token-123',
        user: { id: 1, username: 'testuser', role: 'user' }
      };

      service.login('testuser', 'password').subscribe(response => {
        expect(response).toEqual(mockResponse);
        expect(localStorage.getItem('auth_token')).toBe('test-token-123');
        expect(localStorage.getItem('current_user')).toBe(JSON.stringify(mockResponse.user));
      });

      const req = httpMock.expectOne('https://func-integration-main.azurewebsites.net/api/Login');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ username: 'testuser', password: 'password' });
      req.flush(mockResponse);
    });

    it('should handle login failure', () => {
      const mockResponse: LoginResponse = {
        success: false,
        errorMessage: 'Invalid credentials'
      };

      service.login('testuser', 'wrongpassword').subscribe(response => {
        expect(response.success).toBe(false);
        expect(response.errorMessage).toBe('Invalid credentials');
        expect(localStorage.getItem('auth_token')).toBeNull();
      });

      const req = httpMock.expectOne('https://func-integration-main.azurewebsites.net/api/Login');
      req.flush(mockResponse);
    });

    it('should update currentUser$ observable on successful login', (done) => {
      const mockResponse: LoginResponse = {
        success: true,
        token: 'test-token',
        user: { id: 1, username: 'testuser', role: 'admin' }
      };

      service.currentUser$.subscribe(user => {
        if (user) {
          expect(user.username).toBe('testuser');
          expect(user.role).toBe('admin');
          done();
        }
      });

      service.login('testuser', 'password').subscribe();
      const req = httpMock.expectOne('https://func-integration-main.azurewebsites.net/api/Login');
      req.flush(mockResponse);
    });

    it('should not store credentials if login response lacks token or user', () => {
      const mockResponse: LoginResponse = {
        success: true
        // Missing token and user
      };

      service.login('testuser', 'password').subscribe();
      const req = httpMock.expectOne('https://func-integration-main.azurewebsites.net/api/Login');
      req.flush(mockResponse);

      expect(localStorage.getItem('auth_token')).toBeNull();
      expect(localStorage.getItem('current_user')).toBeNull();
    });
  });

  describe('logout', () => {
    it('should clear token and user from localStorage', () => {
      localStorage.setItem('auth_token', 'test-token');
      localStorage.setItem('current_user', JSON.stringify({ id: 1, username: 'test', role: 'user' }));

      service.logout();

      expect(localStorage.getItem('auth_token')).toBeNull();
      expect(localStorage.getItem('current_user')).toBeNull();
    });

    it('should update currentUser$ observable to null', (done) => {
      // First login
      const mockResponse: LoginResponse = {
        success: true,
        token: 'test-token',
        user: { id: 1, username: 'testuser', role: 'user' }
      };

      service.login('testuser', 'password').subscribe();
      const loginReq = httpMock.expectOne('https://func-integration-main.azurewebsites.net/api/Login');
      loginReq.flush(mockResponse);

      // Then logout
      service.currentUser$.subscribe(user => {
        if (user === null) {
          done();
        }
      });

      service.logout();
    });
  });

  describe('getCurrentUser', () => {
    it('should return current user from BehaviorSubject', () => {
      const mockUser: User = { id: 1, username: 'testuser', role: 'admin' };
      localStorage.setItem('current_user', JSON.stringify(mockUser));
      
      // Recreate service to load from localStorage
      const newService = new AuthService(TestBed.inject(HttpTestingController as any));
      expect(newService.getCurrentUser()).toEqual(mockUser);
    });

    it('should return null if no user is stored', () => {
      expect(service.getCurrentUser()).toBeNull();
    });
  });

  describe('getToken', () => {
    it('should return token from localStorage', () => {
      localStorage.setItem('auth_token', 'test-token-123');
      expect(service.getToken()).toBe('test-token-123');
    });

    it('should return null if no token is stored', () => {
      expect(service.getToken()).toBeNull();
    });
  });

  describe('isAuthenticated', () => {
    it('should return true if token exists', () => {
      localStorage.setItem('auth_token', 'test-token');
      expect(service.isAuthenticated()).toBe(true);
    });

    it('should return false if no token exists', () => {
      expect(service.isAuthenticated()).toBe(false);
    });
  });

  describe('isAdmin', () => {
    it('should return true if user role is admin', () => {
      const mockUser: User = { id: 1, username: 'admin', role: 'admin' };
      localStorage.setItem('current_user', JSON.stringify(mockUser));
      
      const newService = new AuthService(TestBed.inject(HttpTestingController as any));
      expect(newService.isAdmin()).toBe(true);
    });

    it('should return false if user role is not admin', () => {
      const mockUser: User = { id: 1, username: 'user', role: 'user' };
      localStorage.setItem('current_user', JSON.stringify(mockUser));
      
      const newService = new AuthService(TestBed.inject(HttpTestingController as any));
      expect(newService.isAdmin()).toBe(false);
    });

    it('should return false if no user exists', () => {
      expect(service.isAdmin()).toBe(false);
    });
  });

  describe('getAuthHeaders', () => {
    it('should return Authorization header with token if token exists', () => {
      localStorage.setItem('auth_token', 'test-token-123');
      const headers = service.getAuthHeaders();
      expect(headers).toEqual({ 'Authorization': 'Bearer test-token-123' });
    });

    it('should return empty object if no token exists', () => {
      const headers = service.getAuthHeaders();
      expect(headers).toEqual({});
    });
  });

  describe('setDemoUser', () => {
    it('should set demo user in localStorage and BehaviorSubject', () => {
      service.setDemoUser();

      expect(localStorage.getItem('auth_token')).toBe('demo-token');
      const storedUser = JSON.parse(localStorage.getItem('current_user')!);
      expect(storedUser).toEqual({ id: 0, username: 'test', role: 'user' });

      service.currentUser$.subscribe(user => {
        expect(user).toEqual({ id: 0, username: 'test', role: 'user' });
      });
    });
  });

  describe('initialization', () => {
    it('should load user from localStorage on construction', () => {
      const mockUser: User = { id: 1, username: 'storeduser', role: 'user' };
      localStorage.setItem('current_user', JSON.stringify(mockUser));

      const newService = new AuthService(TestBed.inject(HttpTestingController as any));
      
      newService.currentUser$.subscribe(user => {
        expect(user).toEqual(mockUser);
      });
    });

    it('should not load user if localStorage is empty', () => {
      localStorage.clear();
      const newService = new AuthService(TestBed.inject(HttpTestingController as any));
      
      expect(newService.getCurrentUser()).toBeNull();
    });
  });

  describe('edge cases and error handling', () => {
    it('should handle invalid JSON in localStorage', () => {
      localStorage.setItem('current_user', 'invalid-json');
      
      const newService = new AuthService(TestBed.inject(HttpTestingController as any));
      
      // Should not throw, but handle gracefully
      expect(() => newService.getCurrentUser()).not.toThrow();
    });

    it('should handle login with empty username', () => {
      service.login('', 'password').subscribe({
        next: () => {},
        error: () => {}
      });

      const req = httpMock.expectOne('https://func-integration-main.azurewebsites.net/api/Login');
      req.flush({ success: false, errorMessage: 'Username required' });
    });

    it('should handle login with empty password', () => {
      service.login('username', '').subscribe({
        next: () => {},
        error: () => {}
      });

      const req = httpMock.expectOne('https://func-integration-main.azurewebsites.net/api/Login');
      req.flush({ success: false, errorMessage: 'Password required' });
    });

    it('should handle network error during login', () => {
      service.login('user', 'pass').subscribe({
        next: () => fail('should have failed'),
        error: (error) => {
          expect(error).toBeTruthy();
        }
      });

      const req = httpMock.expectOne('https://func-integration-main.azurewebsites.net/api/Login');
      req.error(new ErrorEvent('Network error'));
    });

    it('should handle HTTP 500 error during login', () => {
      service.login('user', 'pass').subscribe({
        next: () => fail('should have failed'),
        error: (error: any) => {
          expect(error.status).toBe(500);
        }
      });

      const req = httpMock.expectOne('https://func-integration-main.azurewebsites.net/api/Login');
      req.flush(null, { status: 500, statusText: 'Server Error' });
    });

    it('should handle concurrent login attempts', () => {
      const mockResponse: LoginResponse = {
        success: true,
        token: 'token',
        user: { id: 1, username: 'user', role: 'user' }
      };

      service.login('user', 'pass').subscribe();
      service.login('user', 'pass').subscribe();

      const requests = httpMock.match('https://func-integration-main.azurewebsites.net/api/Login');
      expect(requests.length).toBe(2);
      requests.forEach(req => req.flush(mockResponse));
    });

    it('should handle token expiration scenario', () => {
      localStorage.setItem('auth_token', 'expired-token');
      expect(service.isAuthenticated()).toBe(true);
      
      // In real scenario, API would return 401 and token would be cleared
      service.logout();
      expect(service.isAuthenticated()).toBe(false);
    });

    it('should handle storage quota exceeded', () => {
      // Simulate storage quota exceeded
      spyOn(localStorage, 'setItem').and.throwError('QuotaExceededError');
      
      const mockResponse: LoginResponse = {
        success: true,
        token: 'token',
        user: { id: 1, username: 'user', role: 'user' }
      };

      service.login('user', 'pass').subscribe({
        next: () => {
          // Should handle gracefully even if storage fails
        }
      });

      const req = httpMock.expectOne('https://func-integration-main.azurewebsites.net/api/Login');
      req.flush(mockResponse);
    });
  });
});
