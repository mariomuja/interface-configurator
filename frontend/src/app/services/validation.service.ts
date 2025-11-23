import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class ValidationService {
  /**
   * Sanitize interface name
   */
  sanitizeInterfaceName(name: string): string {
    return name.trim().replace(/[^a-zA-Z0-9_-]/g, '');
  }

  /**
   * Validate interface name
   */
  validateInterfaceName(name: string): { valid: boolean; error?: string } {
    if (!name || name.trim().length === 0) {
      return { valid: false, error: 'Interface-Name darf nicht leer sein' };
    }
    if (name.length < 3) {
      return { valid: false, error: 'Interface-Name muss mindestens 3 Zeichen lang sein' };
    }
    if (name.length > 100) {
      return { valid: false, error: 'Interface-Name darf maximal 100 Zeichen lang sein' };
    }
    if (!/^[a-zA-Z0-9_-]+$/.test(name)) {
      return { valid: false, error: 'Interface-Name darf nur Buchstaben, Zahlen, Bindestrich und Unterstrich enthalten' };
    }
    return { valid: true };
  }

  /**
   * Validate field separator
   */
  validateFieldSeparator(separator: string): { valid: boolean; error?: string } {
    if (!separator || separator.length === 0) {
      return { valid: false, error: 'Field Separator darf nicht leer sein' };
    }
    if (separator.length > 10) {
      return { valid: false, error: 'Field Separator darf maximal 10 Zeichen lang sein' };
    }
    return { valid: true };
  }

  /**
   * Validate batch size
   */
  validateBatchSize(size: number): { valid: boolean; error?: string } {
    if (size < 1) {
      return { valid: false, error: 'Batch Size muss mindestens 1 sein' };
    }
    if (size > 10000) {
      return { valid: false, error: 'Batch Size darf maximal 10.000 sein' };
    }
    return { valid: true };
  }

  /**
   * Validate polling interval
   */
  validatePollingInterval(interval: number): { valid: boolean; error?: string } {
    if (interval < 1) {
      return { valid: false, error: 'Polling Interval muss mindestens 1 Sekunde sein' };
    }
    if (interval > 3600) {
      return { valid: false, error: 'Polling Interval darf maximal 3600 Sekunden (1 Stunde) sein' };
    }
    return { valid: true };
  }

  /**
   * Sanitize file mask
   */
  sanitizeFileMask(fileMask: string): string {
    return fileMask.trim();
  }

  /**
   * Validate file mask
   */
  validateFileMask(fileMask: string): { valid: boolean; error?: string } {
    if (!fileMask || fileMask.trim().length === 0) {
      return { valid: false, error: 'File Mask darf nicht leer sein' };
    }
    if (fileMask.length > 100) {
      return { valid: false, error: 'File Mask darf maximal 100 Zeichen lang sein' };
    }
    return { valid: true };
  }
}

