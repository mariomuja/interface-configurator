/**
 * Test Isolation Helpers - Ensure tests don't interfere with each other
 */

/**
 * Test isolation utilities
 */
export class TestIsolationHelpers {
  /**
   * Clean up localStorage
   */
  static cleanupLocalStorage(): void {
    localStorage.clear();
  }

  /**
   * Clean up sessionStorage
   */
  static cleanupSessionStorage(): void {
    sessionStorage.clear();
  }

  /**
   * Clean up all storage
   */
  static cleanupStorage(): void {
    this.cleanupLocalStorage();
    this.cleanupSessionStorage();
  }

  /**
   * Reset Date.now mock
   */
  static resetDateMock(): void {
    if ((Date as any).now.restore) {
      (Date as any).now.restore();
    }
  }

  /**
   * Reset all mocks
   */
  static resetMocks(): void {
    jasmine.getEnv().currentSpec = null;
  }

  /**
   * Clean up DOM
   */
  static cleanupDOM(): void {
    document.body.innerHTML = '';
    document.head.innerHTML = '';
  }

  /**
   * Clean up all test artifacts
   */
  static cleanupAll(): void {
    this.cleanupStorage();
    this.cleanupDOM();
    this.resetMocks();
    this.resetDateMock();
  }

  /**
   * Isolate test execution
   */
  static isolateTest(testFn: () => void | Promise<void>): () => void | Promise<void> {
    return async () => {
      this.cleanupAll();
      try {
        await testFn();
      } finally {
        this.cleanupAll();
      }
    };
  }

  /**
   * Create isolated test context
   */
  static createIsolatedContext(): {
    cleanup: () => void;
    storage: {
      localStorage: Storage;
      sessionStorage: Storage;
    };
  } {
    const originalLocalStorage = { ...localStorage };
    const originalSessionStorage = { ...sessionStorage };

    return {
      cleanup: () => {
        localStorage.clear();
        sessionStorage.clear();
        Object.keys(originalLocalStorage).forEach(key => {
          localStorage.setItem(key, originalLocalStorage[key]);
        });
        Object.keys(originalSessionStorage).forEach(key => {
          sessionStorage.setItem(key, originalSessionStorage[key]);
        });
      },
      storage: {
        localStorage,
        sessionStorage
      }
    };
  }
}
