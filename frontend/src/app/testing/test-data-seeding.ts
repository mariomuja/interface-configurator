/**
 * Test Data Seeding Utilities
 */

import { CsvRecordBuilder, SqlRecordBuilder, ProcessLogBuilder, InterfaceConfigurationBuilder } from './test-data-builders';

/**
 * Test data seeding utilities
 */
export class TestDataSeeding {
  /**
   * Seed CSV records
   */
  static seedCsvRecords(count: number = 10): any[] {
    return CsvRecordBuilder.create()
      .withName('Test User')
      .withEmail('test@example.com')
      .buildArray(count);
  }

  /**
   * Seed SQL records
   */
  static seedSqlRecords(count: number = 10): any[] {
    return SqlRecordBuilder.create()
      .withName('Test User')
      .withEmail('test@example.com')
      .withCreatedAt(new Date().toISOString())
      .buildArray(count);
  }

  /**
   * Seed process logs
   */
  static seedProcessLogs(count: number = 20): any[] {
    const logs: any[] = [];
    const levels: Array<'info' | 'warning' | 'error'> = ['info', 'warning', 'error'];

    for (let i = 0; i < count; i++) {
      const level = levels[i % levels.length];
      logs.push(
        ProcessLogBuilder.create()
          .withId(i + 1)
          .withLevel(level)
          .withMessage(`Log message ${i + 1}`)
          .withTimestamp(new Date(Date.now() - i * 60000).toISOString())
          .build()
      );
    }

    return logs;
  }

  /**
   * Seed interface configurations
   */
  static seedInterfaceConfigs(count: number = 5): any[] {
    const configs: any[] = [];
    const adapters = ['CSV', 'SqlServer', 'SAP', 'Dynamics365', 'CRM'];

    for (let i = 0; i < count; i++) {
      configs.push(
        InterfaceConfigurationBuilder.create()
          .withInterfaceName(`TestInterface${i + 1}`)
          .withSourceAdapter(adapters[i % adapters.length])
          .withDestinationAdapter('SqlServer')
          .withPollingInterval(10 + i)
          .enabled()
          .build()
      );
    }

    return configs;
  }

  /**
   * Seed complete test dataset
   */
  static seedCompleteDataset(options: {
    csvRecords?: number;
    sqlRecords?: number;
    processLogs?: number;
    interfaceConfigs?: number;
  } = {}): {
    csvRecords: any[];
    sqlRecords: any[];
    processLogs: any[];
    interfaceConfigs: any[];
  } {
    return {
      csvRecords: this.seedCsvRecords(options.csvRecords || 10),
      sqlRecords: this.seedSqlRecords(options.sqlRecords || 10),
      processLogs: this.seedProcessLogs(options.processLogs || 20),
      interfaceConfigs: this.seedInterfaceConfigs(options.interfaceConfigs || 5)
    };
  }

  /**
   * Seed test data into localStorage
   */
  static seedLocalStorage(data: Record<string, any>): void {
    Object.keys(data).forEach(key => {
      localStorage.setItem(key, JSON.stringify(data[key]));
    });
  }

  /**
   * Seed test data into sessionStorage
   */
  static seedSessionStorage(data: Record<string, any>): void {
    Object.keys(data).forEach(key => {
      sessionStorage.setItem(key, JSON.stringify(data[key]));
    });
  }

  /**
   * Clear seeded data
   */
  static clearSeededData(): void {
    localStorage.clear();
    sessionStorage.clear();
  }

  /**
   * Seed mock API responses
   */
  static seedMockApiResponses(responses: Record<string, any>): Record<string, any> {
    return responses;
  }
}
