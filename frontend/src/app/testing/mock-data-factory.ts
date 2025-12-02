/**
 * Mock data factory for creating test data
 */

import { CsvRecord, SqlRecord, ProcessLog } from '../models/data.model';

export class MockDataFactory {
  /**
   * Create mock CSV record
   */
  static createCsvRecord(overrides: Partial<CsvRecord> = {}): CsvRecord {
    return {
      id: 1,
      name: 'Test User',
      email: 'test@test.com',
      age: 30,
      city: 'Berlin',
      salary: 50000,
      ...overrides,
    };
  }

  /**
   * Create array of mock CSV records
   */
  static createCsvRecords(count: number = 3): CsvRecord[] {
    return Array.from({ length: count }, (_, i) =>
      this.createCsvRecord({
        id: i + 1,
        name: `User ${i + 1}`,
        email: `user${i + 1}@test.com`,
      })
    );
  }

  /**
   * Create mock SQL record
   */
  static createSqlRecord(overrides: Partial<SqlRecord> = {}): SqlRecord {
    return {
      id: 1,
      name: 'Test User',
      email: 'test@test.com',
      age: 30,
      city: 'Berlin',
      salary: 50000,
      createdAt: '2024-01-01',
      ...overrides,
    };
  }

  /**
   * Create array of mock SQL records
   */
  static createSqlRecords(count: number = 3): SqlRecord[] {
    return Array.from({ length: count }, (_, i) =>
      this.createSqlRecord({
        id: i + 1,
        name: `User ${i + 1}`,
        email: `user${i + 1}@test.com`,
        createdAt: `2024-01-${String(i + 1).padStart(2, '0')}`,
      })
    );
  }

  /**
   * Create mock process log
   */
  static createProcessLog(overrides: Partial<ProcessLog> = {}): ProcessLog {
    return {
      id: 1,
      timestamp: new Date().toISOString(),
      level: 'info',
      message: 'Test log message',
      ...overrides,
    };
  }

  /**
   * Create array of mock process logs
   */
  static createProcessLogs(count: number = 5): ProcessLog[] {
    const levels: ProcessLog['level'][] = ['info', 'warning', 'error'];
    return Array.from({ length: count }, (_, i) =>
      this.createProcessLog({
        id: i + 1,
        level: levels[i % levels.length],
        message: `Log message ${i + 1}`,
        timestamp: new Date(Date.now() - i * 60000).toISOString(),
      })
    );
  }

  /**
   * Create mock interface configuration
   */
  static createInterfaceConfiguration(overrides: any = {}): any {
    return {
      interfaceName: 'TestInterface',
      sourceAdapterName: 'CSV',
      destinationAdapterName: 'SqlServer',
      sourceInstanceName: 'Source',
      destinationInstanceName: 'Destination',
      sourceIsEnabled: true,
      destinationIsEnabled: true,
      csvData: 'Id║Name\n1║Test User',
      csvPollingInterval: 10,
      ...overrides,
    };
  }

  /**
   * Create mock blob container folder
   */
  static createBlobContainerFolder(overrides: any = {}): any {
    return {
      path: '/csv-incoming',
      files: [
        {
          name: 'test-file.csv',
          fullPath: 'csv-incoming/test-file.csv',
          size: 1024,
          lastModified: new Date().toISOString(),
          contentType: 'text/csv',
        },
      ],
      ...overrides,
    };
  }

  /**
   * Create mock destination adapter instance
   */
  static createDestinationAdapterInstance(overrides: any = {}): any {
    return {
      adapterInstanceGuid: 'test-guid-' + Math.random().toString(36).substr(2, 9),
      instanceName: 'TestInstance',
      adapterName: 'CSV',
      isEnabled: true,
      configuration: {},
      ...overrides,
    };
  }

  /**
   * Create mock HTTP error response
   */
  static createHttpError(status: number = 500, message: string = 'Server Error'): any {
    return {
      status,
      statusText: message,
      error: { errorMessage: message },
    };
  }

  /**
   * Create mock login response
   */
  static createLoginResponse(success: boolean = true, overrides: any = {}): any {
    return {
      success,
      token: success ? 'mock-token' : undefined,
      user: success
        ? { id: 1, username: 'testuser', role: 'user' }
        : undefined,
      errorMessage: success ? undefined : 'Login failed',
      ...overrides,
    };
  }

  /**
   * Create mock version info
   */
  static createVersionInfo(overrides: any = {}): any {
    return {
      version: '1.0.0',
      buildNumber: 100,
      lastUpdated: new Date().toISOString(),
      ...overrides,
    };
  }

  /**
   * Create mock processing statistics
   */
  static createProcessingStatistics(overrides: any = {}): any {
    return {
      totalRows: 100,
      succeededRows: 95,
      failedRows: 5,
      lastProcessed: new Date().toISOString(),
      ...overrides,
    };
  }
}
