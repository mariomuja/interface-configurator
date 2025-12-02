import { TestBed } from '@angular/core/testing';
import { SessionService } from './session.service';

describe('SessionService', () => {
  let service: SessionService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
    service = TestBed.inject(SessionService);
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('getSessionId', () => {
    it('should generate a new session ID if none exists', () => {
      const sessionId = service.getSessionId();
      
      expect(sessionId).toBeTruthy();
      expect(sessionId.length).toBeGreaterThan(0);
      expect(localStorage.getItem('app-session-id')).toBe(sessionId);
    });

    it('should return existing session ID if one exists', () => {
      const existingId = 'existing-session-id';
      localStorage.setItem('app-session-id', existingId);
      
      const sessionId = service.getSessionId();
      
      expect(sessionId).toBe(existingId);
    });

    it('should generate UUID v4 format session ID', () => {
      const sessionId = service.getSessionId();
      
      // UUID v4 format: xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx
      const uuidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
      expect(sessionId).toMatch(uuidRegex);
    });

    it('should return the same session ID on multiple calls', () => {
      const sessionId1 = service.getSessionId();
      const sessionId2 = service.getSessionId();
      
      expect(sessionId1).toBe(sessionId2);
    });
  });

  describe('resetSession', () => {
    it('should create a new session ID', () => {
      const oldSessionId = service.getSessionId();
      const newSessionId = service.resetSession();
      
      expect(newSessionId).not.toBe(oldSessionId);
      expect(newSessionId).toBeTruthy();
      expect(localStorage.getItem('app-session-id')).toBe(newSessionId);
    });

    it('should generate UUID v4 format for new session ID', () => {
      service.getSessionId(); // Create initial session
      const newSessionId = service.resetSession();
      
      const uuidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
      expect(newSessionId).toMatch(uuidRegex);
    });

    it('should update stored session ID', () => {
      const oldSessionId = service.getSessionId();
      const newSessionId = service.resetSession();
      
      expect(localStorage.getItem('app-session-id')).toBe(newSessionId);
      expect(localStorage.getItem('app-session-id')).not.toBe(oldSessionId);
    });

    it('should return new session ID on subsequent getSessionId calls after reset', () => {
      service.getSessionId();
      const resetId = service.resetSession();
      const retrievedId = service.getSessionId();
      
      expect(retrievedId).toBe(resetId);
    });
  });

  describe('generateSessionId', () => {
    it('should generate unique session IDs', () => {
      const id1 = service.getSessionId();
      service.resetSession();
      const id2 = service.getSessionId();
      
      expect(id1).not.toBe(id2);
    });

    it('should generate valid UUID v4 format', () => {
      const ids: string[] = [];
      for (let i = 0; i < 10; i++) {
        service.resetSession();
        ids.push(service.getSessionId());
      }
      
      const uuidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
      ids.forEach(id => {
        expect(id).toMatch(uuidRegex);
      });
    });
  });
});
