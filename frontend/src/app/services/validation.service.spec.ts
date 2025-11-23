import { TestBed } from '@angular/core/testing';
import { ValidationService } from './validation.service';

describe('ValidationService', () => {
  let service: ValidationService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ValidationService]
    });
    service = TestBed.inject(ValidationService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('validateInterfaceName', () => {
    it('should reject empty names', () => {
      const result = service.validateInterfaceName('');
      expect(result.valid).toBe(false);
      expect(result.error).toContain('leer');
    });

    it('should reject names shorter than 3 characters', () => {
      const result = service.validateInterfaceName('ab');
      expect(result.valid).toBe(false);
      expect(result.error).toContain('mindestens 3');
    });

    it('should reject names longer than 100 characters', () => {
      const result = service.validateInterfaceName('a'.repeat(101));
      expect(result.valid).toBe(false);
      expect(result.error).toContain('maximal 100');
    });

    it('should reject names with invalid characters', () => {
      const result = service.validateInterfaceName('test@name');
      expect(result.valid).toBe(false);
      expect(result.error).toContain('Buchstaben');
    });

    it('should accept valid names', () => {
      expect(service.validateInterfaceName('TestInterface123').valid).toBe(true);
      expect(service.validateInterfaceName('test-interface').valid).toBe(true);
      expect(service.validateInterfaceName('test_interface').valid).toBe(true);
    });
  });

  describe('validateFieldSeparator', () => {
    it('should reject empty separators', () => {
      const result = service.validateFieldSeparator('');
      expect(result.valid).toBe(false);
    });

    it('should reject separators longer than 10 characters', () => {
      const result = service.validateFieldSeparator('a'.repeat(11));
      expect(result.valid).toBe(false);
    });

    it('should accept valid separators', () => {
      expect(service.validateFieldSeparator('║').valid).toBe(true);
      expect(service.validateFieldSeparator(',').valid).toBe(true);
      expect(service.validateFieldSeparator(';').valid).toBe(true);
    });
  });

  describe('validateBatchSize', () => {
    it('should reject batch sizes less than 1', () => {
      const result = service.validateBatchSize(0);
      expect(result.valid).toBe(false);
    });

    it('should reject batch sizes greater than 10000', () => {
      const result = service.validateBatchSize(10001);
      expect(result.valid).toBe(false);
    });

    it('should accept valid batch sizes', () => {
      expect(service.validateBatchSize(1).valid).toBe(true);
      expect(service.validateBatchSize(100).valid).toBe(true);
      expect(service.validateBatchSize(10000).valid).toBe(true);
    });
  });

  describe('validatePollingInterval', () => {
    it('should reject intervals less than 1', () => {
      const result = service.validatePollingInterval(0);
      expect(result.valid).toBe(false);
    });

    it('should reject intervals greater than 3600', () => {
      const result = service.validatePollingInterval(3601);
      expect(result.valid).toBe(false);
    });

    it('should accept valid intervals', () => {
      expect(service.validatePollingInterval(1).valid).toBe(true);
      expect(service.validatePollingInterval(60).valid).toBe(true);
      expect(service.validatePollingInterval(3600).valid).toBe(true);
    });
  });

  describe('sanitizeInterfaceName', () => {
    it('should remove invalid characters', () => {
      expect(service.sanitizeInterfaceName('test@name#123')).toBe('testname123');
    });

    it('should trim whitespace', () => {
      expect(service.sanitizeInterfaceName('  test  ')).toBe('test');
    });

    it('should preserve valid characters', () => {
      expect(service.sanitizeInterfaceName('test-name_123')).toBe('test-name_123');
    });
  });
});

