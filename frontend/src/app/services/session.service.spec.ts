import { TestBed } from '@angular/core/testing';
import { SessionService } from './session.service';

describe('SessionService', () => {
  let service: SessionService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(SessionService);
    // Clear localStorage before each test
    localStorage.clear();
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('getSessionId', () => {
    it('should generate a new session ID when none exists', () => {
      const sessionId = service.getSessionId();
      expect(sessionId).toBeTruthy();
      expect(sessionId.length).toBeGreaterThan(0);
    });

    it('should return the same session ID on subsequent calls', () => {
      const sessionId1 = service.getSessionId();
      const sessionId2 = service.getSessionId();
      expect(sessionId1).toBe(sessionId2);
    });

    it('should generate valid UUID v4 format', () => {
      const sessionId = service.getSessionId();
      const uuidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
      expect(sessionId).toMatch(uuidRegex);
    });

    it('should persist session ID in localStorage', () => {
      const sessionId = service.getSessionId();
      const storedId = localStorage.getItem('app-session-id');
      expect(storedId).toBe(sessionId);
    });
  });

  describe('resetSession', () => {
    it('should generate a new session ID', () => {
      const oldSessionId = service.getSessionId();
      const newSessionId = service.resetSession();
      
      expect(newSessionId).toBeTruthy();
      expect(newSessionId).not.toBe(oldSessionId);
    });

    it('should update stored session ID in localStorage', () => {
      const oldSessionId = service.getSessionId();
      const newSessionId = service.resetSession();
      const storedId = localStorage.getItem('app-session-id');
      
      expect(storedId).toBe(newSessionId);
      expect(storedId).not.toBe(oldSessionId);
    });

    it('should return valid UUID v4 format', () => {
      const sessionId = service.resetSession();
      const uuidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
      expect(sessionId).toMatch(uuidRegex);
    });

    it('should generate unique session IDs on multiple resets', () => {
      const sessionId1 = service.resetSession();
      const sessionId2 = service.resetSession();
      
      expect(sessionId1).not.toBe(sessionId2);
    });
  });

  describe('edge cases', () => {
    it('should handle localStorage being unavailable gracefully', () => {
      // Mock localStorage to throw error
      const originalSetItem = localStorage.setItem;
      spyOn(localStorage, 'setItem').and.throwError('QuotaExceededError');
      
      // Should still generate session ID
      expect(() => service.getSessionId()).not.toThrow();
      
      // Restore
      localStorage.setItem = originalSetItem;
    });

    it('should handle corrupted localStorage data', () => {
      localStorage.setItem('app-session-id', 'invalid-uuid');
      
      // Should generate new valid UUID
      const sessionId = service.getSessionId();
      const uuidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
      expect(sessionId).toMatch(uuidRegex);
    });
  });
});
