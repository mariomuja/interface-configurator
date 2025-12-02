import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { FeatureService, Feature } from './feature.service';
import { AuthService } from './auth.service';

describe('FeatureService', () => {
  let service: FeatureService;
  let httpMock: HttpTestingController;
  let authService: jasmine.SpyObj<AuthService>;

  beforeEach(() => {
    const authServiceSpy = jasmine.createSpyObj('AuthService', ['getAuthHeaders']);

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        FeatureService,
        { provide: AuthService, useValue: authServiceSpy }
      ]
    });
    service = TestBed.inject(FeatureService);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    
    authService.getAuthHeaders.and.returnValue({ 'Authorization': 'Bearer test-token' });
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('getFeatures', () => {
    it('should fetch features with auth headers', () => {
      const mockFeatures: Feature[] = [
        {
          id: 1,
          featureNumber: 1,
          title: 'Test Feature',
          description: 'Test Description',
          detailedDescription: 'Detailed Description',
          category: 'Test',
          priority: 'High',
          isEnabled: true,
          implementedDate: '2024-01-01',
          canToggle: true
        }
      ];

      service.getFeatures().subscribe(features => {
        expect(features).toEqual(mockFeatures);
      });

      const req = httpMock.expectOne((request) => {
        return request.url.includes('/GetFeatures') && 
               request.headers.get('Authorization') === 'Bearer test-token';
      });
      expect(req.request.method).toBe('GET');
      req.flush(mockFeatures);
    });

    it('should handle empty features list', () => {
      service.getFeatures().subscribe(features => {
        expect(features).toEqual([]);
      });

      const req = httpMock.expectOne((request) => request.url.includes('/GetFeatures'));
      req.flush([]);
    });
  });

  describe('toggleFeature', () => {
    it('should toggle feature with auth headers', () => {
      const mockResponse = { success: true };

      service.toggleFeature(1).subscribe(response => {
        expect(response).toEqual(mockResponse);
      });

      const req = httpMock.expectOne((request) => {
        return request.url.includes('/ToggleFeature') && 
               request.headers.get('Authorization') === 'Bearer test-token';
      });
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ featureId: 1 });
      req.flush(mockResponse);
    });

    it('should handle toggle failure', () => {
      const mockResponse = { success: false };

      service.toggleFeature(999).subscribe(response => {
        expect(response.success).toBe(false);
      });

      const req = httpMock.expectOne((request) => request.url.includes('/ToggleFeature'));
      req.flush(mockResponse);
    });
  });

  describe('updateTestComment', () => {
    it('should update test comment with auth headers', () => {
      const mockResponse = { success: true };
      const testComment = 'Test passed successfully';

      service.updateTestComment(1, testComment).subscribe(response => {
        expect(response).toEqual(mockResponse);
      });

      const req = httpMock.expectOne((request) => {
        return request.url.includes('/UpdateFeatureTestComment') && 
               request.headers.get('Authorization') === 'Bearer test-token';
      });
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ featureId: 1, testComment });
      req.flush(mockResponse);
    });

    it('should handle empty test comment', () => {
      const mockResponse = { success: true };

      service.updateTestComment(1, '').subscribe(response => {
        expect(response.success).toBe(true);
      });

      const req = httpMock.expectOne((request) => request.url.includes('/UpdateFeatureTestComment'));
      expect(req.request.body).toEqual({ featureId: 1, testComment: '' });
      req.flush(mockResponse);
    });
  });

  describe('getApiUrl', () => {
    it('should use configured API URL from window', () => {
      (window as any).INTERFACE_CONFIGURATOR_API_BASE_URL = 'https://custom-api.example.com/api';
      
      const newService = new FeatureService(
        TestBed.inject(HttpTestingController as any),
        authService
      );

      newService.getFeatures().subscribe();
      const req = httpMock.expectOne('https://custom-api.example.com/api/GetFeatures');
      expect(req).toBeTruthy();
      req.flush([]);
      
      delete (window as any).INTERFACE_CONFIGURATOR_API_BASE_URL;
    });

    it('should use localhost API URL for localhost', () => {
      spyOnProperty(window, 'location', 'get').and.returnValue({
        hostname: 'localhost',
        origin: 'http://localhost:4200'
      } as any);

      const newService = new FeatureService(
        TestBed.inject(HttpTestingController as any),
        authService
      );

      newService.getFeatures().subscribe();
      const req = httpMock.expectOne('http://localhost:7071/api/GetFeatures');
      expect(req).toBeTruthy();
      req.flush([]);
    });

    it('should use localStorage override if available', () => {
      localStorage.setItem('interfaceConfigurator.apiBaseUrl', 'https://local-override.com/api');
      
      const newService = new FeatureService(
        TestBed.inject(HttpTestingController as any),
        authService
      );

      newService.getFeatures().subscribe();
      const req = httpMock.expectOne('https://local-override.com/api/GetFeatures');
      expect(req).toBeTruthy();
      req.flush([]);
      
      localStorage.removeItem('interfaceConfigurator.apiBaseUrl');
    });
  });
});
