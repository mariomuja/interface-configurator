import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { VersionService, VersionInfo } from './version.service';

describe('VersionService', () => {
  let service: VersionService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [VersionService]
    });
    service = TestBed.inject(VersionService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('getVersion', () => {
    it('should fetch version from assets/version.json', () => {
      const mockVersion: VersionInfo = {
        version: '1.2.3',
        buildNumber: 456,
        lastUpdated: '2024-01-01T00:00:00Z'
      };

      service.getVersion().subscribe(version => {
        expect(version).toEqual(mockVersion);
      });

      const req = httpMock.expectOne('/assets/version.json');
      expect(req.request.method).toBe('GET');
      req.flush(mockVersion);
    });

    it('should cache version after first fetch', () => {
      const mockVersion: VersionInfo = {
        version: '1.2.3',
        buildNumber: 456,
        lastUpdated: '2024-01-01T00:00:00Z'
      };

      // First call
      service.getVersion().subscribe();
      const req1 = httpMock.expectOne('/assets/version.json');
      req1.flush(mockVersion);

      // Second call should use cache
      service.getVersion().subscribe(version => {
        expect(version).toEqual(mockVersion);
      });

      httpMock.expectNone('/assets/version.json');
    });

    it('should return fallback version on error', () => {
      service.getVersion().subscribe(version => {
        expect(version.version).toBe('1.0.0');
        expect(version.buildNumber).toBe(0);
        expect(version.lastUpdated).toBeTruthy();
      });

      const req = httpMock.expectOne('/assets/version.json');
      req.error(new ErrorEvent('Network error'));
    });

    it('should cache fallback version after error', () => {
      // First call - error
      service.getVersion().subscribe();
      const req1 = httpMock.expectOne('/assets/version.json');
      req1.error(new ErrorEvent('Network error'));

      // Second call should use cached fallback
      service.getVersion().subscribe(version => {
        expect(version.version).toBe('1.0.0');
      });

      httpMock.expectNone('/assets/version.json');
    });
  });

  describe('getVersionString', () => {
    it('should return formatted version string', () => {
      const mockVersion: VersionInfo = {
        version: '1.2.3',
        buildNumber: 456,
        lastUpdated: '2024-01-01T00:00:00Z'
      };

      service.getVersionString().subscribe(versionString => {
        expect(versionString).toBe('v1.2.3 (build 456)');
      });

      const req = httpMock.expectOne('/assets/version.json');
      req.flush(mockVersion);
    });

    it('should return formatted fallback version string on error', () => {
      service.getVersionString().subscribe(versionString => {
        expect(versionString).toBe('v1.0.0 (build 0)');
      });

      const req = httpMock.expectOne('/assets/version.json');
      req.error(new ErrorEvent('Network error'));
    });

    it('should use cached version for version string', () => {
      const mockVersion: VersionInfo = {
        version: '2.0.0',
        buildNumber: 789,
        lastUpdated: '2024-01-01T00:00:00Z'
      };

      // Cache version
      service.getVersion().subscribe();
      const req1 = httpMock.expectOne('/assets/version.json');
      req1.flush(mockVersion);

      // Get version string should use cache
      service.getVersionString().subscribe(versionString => {
        expect(versionString).toBe('v2.0.0 (build 789)');
      });

      httpMock.expectNone('/assets/version.json');
    });
  });
});
