import { CsvRecord, SqlRecord, ProcessLog } from './data.model';

describe('Data Models', () => {
  describe('CsvRecord', () => {
    it('should accept dynamic properties', () => {
      const record: CsvRecord = {
        id: 1,
        name: 'Test',
        email: 'test@test.com',
        age: 30,
        customField: 'custom value'
      };

      expect(record.id).toBe(1);
      expect(record.name).toBe('Test');
      expect(record.customField).toBe('custom value');
    });

    it('should handle empty record', () => {
      const record: CsvRecord = {};
      expect(Object.keys(record).length).toBe(0);
    });

    it('should handle numeric and string values', () => {
      const record: CsvRecord = {
        id: '123',
        count: 456,
        price: 99.99,
        name: 'Product'
      };

      expect(typeof record.id).toBe('string');
      expect(typeof record.count).toBe('number');
      expect(typeof record.price).toBe('number');
    });
  });

  describe('SqlRecord', () => {
    it('should have id property', () => {
      const record: SqlRecord = {
        id: 1,
        name: 'Test'
      };

      expect(record.id).toBeDefined();
    });

    it('should accept string id', () => {
      const record: SqlRecord = {
        id: 'guid-123',
        name: 'Test'
      };

      expect(typeof record.id).toBe('string');
    });

    it('should accept number id', () => {
      const record: SqlRecord = {
        id: 123,
        name: 'Test'
      };

      expect(typeof record.id).toBe('number');
    });

    it('should support datetime_created field', () => {
      const record: SqlRecord = {
        id: 1,
        datetime_created: '2024-01-01T00:00:00Z'
      };

      expect(record.datetime_created).toBe('2024-01-01T00:00:00Z');
    });

    it('should support createdAt field for backward compatibility', () => {
      const record: SqlRecord = {
        id: 1,
        createdAt: '2024-01-01T00:00:00Z'
      };

      expect(record.createdAt).toBe('2024-01-01T00:00:00Z');
    });

    it('should support both datetime_created and createdAt', () => {
      const record: SqlRecord = {
        id: 1,
        datetime_created: '2024-01-01T00:00:00Z',
        createdAt: '2024-01-01T00:00:00Z'
      };

      expect(record.datetime_created).toBeDefined();
      expect(record.createdAt).toBeDefined();
    });

    it('should accept dynamic properties', () => {
      const record: SqlRecord = {
        id: 1,
        name: 'Test',
        email: 'test@test.com',
        customField: 'value'
      };

      expect(record.customField).toBe('value');
    });
  });

  describe('ProcessLog', () => {
    it('should have required properties', () => {
      const log: ProcessLog = {
        id: 1,
        timestamp: '2024-01-01T00:00:00Z',
        level: 'info',
        message: 'Test message'
      };

      expect(log.id).toBe(1);
      expect(log.timestamp).toBe('2024-01-01T00:00:00Z');
      expect(log.level).toBe('info');
      expect(log.message).toBe('Test message');
    });

    it('should support standard log levels', () => {
      const levels: ProcessLog['level'][] = ['info', 'warning', 'error'];
      
      levels.forEach(level => {
        const log: ProcessLog = {
          id: 1,
          timestamp: '2024-01-01T00:00:00Z',
          level,
          message: 'Test'
        };
        expect(log.level).toBe(level);
      });
    });

    it('should support custom log levels', () => {
      const log: ProcessLog = {
        id: 1,
        timestamp: '2024-01-01T00:00:00Z',
        level: 'debug',
        message: 'Test'
      };

      expect(log.level).toBe('debug');
    });

    it('should support optional details field', () => {
      const log: ProcessLog = {
        id: 1,
        timestamp: '2024-01-01T00:00:00Z',
        level: 'error',
        message: 'Error occurred',
        details: 'Detailed error information'
      };

      expect(log.details).toBe('Detailed error information');
    });

    it('should support optional component field', () => {
      const log: ProcessLog = {
        id: 1,
        timestamp: '2024-01-01T00:00:00Z',
        level: 'info',
        message: 'Component action',
        component: 'TransportService'
      };

      expect(log.component).toBe('TransportService');
    });

    it('should support optional interfaceName field', () => {
      const log: ProcessLog = {
        id: 1,
        timestamp: '2024-01-01T00:00:00Z',
        level: 'info',
        message: 'Interface action',
        interfaceName: 'TestInterface'
      };

      expect(log.interfaceName).toBe('TestInterface');
    });

    it('should support optional messageId field', () => {
      const log: ProcessLog = {
        id: 1,
        timestamp: '2024-01-01T00:00:00Z',
        level: 'info',
        message: 'Message processed',
        messageId: 'msg-123'
      };

      expect(log.messageId).toBe('msg-123');
    });

    it('should support datetime_created field', () => {
      const log: ProcessLog = {
        id: 1,
        timestamp: '2024-01-01T00:00:00Z',
        datetime_created: '2024-01-01T00:00:00Z',
        level: 'info',
        message: 'Test'
      };

      expect(log.datetime_created).toBeDefined();
    });

    it('should handle all optional fields together', () => {
      const log: ProcessLog = {
        id: 1,
        timestamp: '2024-01-01T00:00:00Z',
        datetime_created: '2024-01-01T00:00:00Z',
        level: 'error',
        message: 'Error occurred',
        details: 'Details',
        component: 'Service',
        interfaceName: 'Interface',
        messageId: 'msg-123'
      };

      expect(log.details).toBeDefined();
      expect(log.component).toBeDefined();
      expect(log.interfaceName).toBeDefined();
      expect(log.messageId).toBeDefined();
    });
  });
});
