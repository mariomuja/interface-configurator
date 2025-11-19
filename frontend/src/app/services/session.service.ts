import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class SessionService {
  private readonly SESSION_ID_KEY = 'app-session-id';

  /**
   * Gets the current session ID, creating a new one if it doesn't exist
   */
  getSessionId(): string {
    let sessionId = localStorage.getItem(this.SESSION_ID_KEY);
    
    if (!sessionId) {
      // Generate a new session ID (UUID v4)
      sessionId = this.generateSessionId();
      localStorage.setItem(this.SESSION_ID_KEY, sessionId);
    }
    
    return sessionId;
  }

  /**
   * Resets the session ID, creating a new one
   */
  resetSession(): string {
    const newSessionId = this.generateSessionId();
    localStorage.setItem(this.SESSION_ID_KEY, newSessionId);
    return newSessionId;
  }

  /**
   * Generates a UUID v4 session ID
   */
  private generateSessionId(): string {
    // Generate UUID v4
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
      const r = Math.random() * 16 | 0;
      const v = c === 'x' ? r : (r & 0x3 | 0x8);
      return v.toString(16);
    });
  }
}

