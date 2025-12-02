/**
 * Test Data Builders - Fluent interface for creating test data
 */

import { CsvRecord, SqlRecord, ProcessLog } from '../models/data.model';

/**
 * Builder for CsvRecord
 */
export class CsvRecordBuilder {
  private record: CsvRecord = {};

  static create(): CsvRecordBuilder {
    return new CsvRecordBuilder();
  }

  withId(id: number | string): CsvRecordBuilder {
    this.record.id = id;
    return this;
  }

  withName(name: string): CsvRecordBuilder {
    this.record.name = name;
    return this;
  }

  withEmail(email: string): CsvRecordBuilder {
    this.record.email = email;
    return this;
  }

  withAge(age: number): CsvRecordBuilder {
    this.record.age = age;
    return this;
  }

  withCity(city: string): CsvRecordBuilder {
    this.record.city = city;
    return this;
  }

  withSalary(salary: number): CsvRecordBuilder {
    this.record.salary = salary;
    return this;
  }

  withField(field: string, value: any): CsvRecordBuilder {
    this.record[field] = value;
    return this;
  }

  build(): CsvRecord {
    return { ...this.record };
  }

  buildArray(count: number): CsvRecord[] {
    return Array.from({ length: count }, (_, i) => {
      const builder = new CsvRecordBuilder();
      Object.keys(this.record).forEach(key => {
        builder.withField(key, this.record[key]);
      });
      return builder.withId(i + 1).build();
    });
  }
}

/**
 * Builder for SqlRecord
 */
export class SqlRecordBuilder {
  private record: SqlRecord = { id: 1 };

  static create(): SqlRecordBuilder {
    return new SqlRecordBuilder();
  }

  withId(id: number | string): SqlRecordBuilder {
    this.record.id = id;
    return this;
  }

  withName(name: string): SqlRecordBuilder {
    this.record.name = name;
    return this;
  }

  withEmail(email: string): SqlRecordBuilder {
    this.record.email = email;
    return this;
  }

  withCreatedAt(date: string): SqlRecordBuilder {
    this.record.createdAt = date;
    this.record.datetime_created = date;
    return this;
  }

  withField(field: string, value: any): SqlRecordBuilder {
    this.record[field] = value;
    return this;
  }

  build(): SqlRecord {
    return { ...this.record };
  }

  buildArray(count: number): SqlRecord[] {
    return Array.from({ length: count }, (_, i) => {
      const builder = new SqlRecordBuilder();
      Object.keys(this.record).forEach(key => {
        if (key !== 'id') {
          builder.withField(key, this.record[key]);
        }
      });
      return builder.withId(i + 1).build();
    });
  }
}

/**
 * Builder for ProcessLog
 */
export class ProcessLogBuilder {
  private log: ProcessLog = {
    id: 1,
    timestamp: new Date().toISOString(),
    level: 'info',
    message: 'Test log'
  };

  static create(): ProcessLogBuilder {
    return new ProcessLogBuilder();
  }

  withId(id: number): ProcessLogBuilder {
    this.log.id = id;
    return this;
  }

  withTimestamp(timestamp: string): ProcessLogBuilder {
    this.log.timestamp = timestamp;
    this.log.datetime_created = timestamp;
    return this;
  }

  withLevel(level: 'info' | 'warning' | 'error' | string): ProcessLogBuilder {
    this.log.level = level;
    return this;
  }

  withMessage(message: string): ProcessLogBuilder {
    this.log.message = message;
    return this;
  }

  withDetails(details: string): ProcessLogBuilder {
    this.log.details = details;
    return this;
  }

  withComponent(component: string): ProcessLogBuilder {
    this.log.component = component;
    return this;
  }

  withInterfaceName(interfaceName: string): ProcessLogBuilder {
    this.log.interfaceName = interfaceName;
    return this;
  }

  withMessageId(messageId: string): ProcessLogBuilder {
    this.log.messageId = messageId;
    return this;
  }

  asError(): ProcessLogBuilder {
    return this.withLevel('error');
  }

  asWarning(): ProcessLogBuilder {
    return this.withLevel('warning');
  }

  asInfo(): ProcessLogBuilder {
    return this.withLevel('info');
  }

  build(): ProcessLog {
    return { ...this.log };
  }

  buildArray(count: number): ProcessLog[] {
    return Array.from({ length: count }, (_, i) => {
      const builder = new ProcessLogBuilder();
      Object.keys(this.log).forEach(key => {
        if (key !== 'id' && key !== 'timestamp') {
          (builder as any)[`with${key.charAt(0).toUpperCase() + key.slice(1)}`] = (value: any) => {
            (builder as any).log[key] = value;
            return builder;
          };
        }
      });
      return builder
        .withId(i + 1)
        .withTimestamp(new Date(Date.now() - i * 60000).toISOString())
        .build();
    });
  }
}

/**
 * Builder for Interface Configuration
 */
export class InterfaceConfigurationBuilder {
  private config: any = {
    interfaceName: 'TestInterface',
    sourceAdapterName: 'CSV',
    destinationAdapterName: 'SqlServer'
  };

  static create(): InterfaceConfigurationBuilder {
    return new InterfaceConfigurationBuilder();
  }

  withInterfaceName(name: string): InterfaceConfigurationBuilder {
    this.config.interfaceName = name;
    return this;
  }

  withSourceAdapter(name: string): InterfaceConfigurationBuilder {
    this.config.sourceAdapterName = name;
    return this;
  }

  withDestinationAdapter(name: string): InterfaceConfigurationBuilder {
    this.config.destinationAdapterName = name;
    return this;
  }

  withCsvData(data: string): InterfaceConfigurationBuilder {
    this.config.csvData = data;
    return this;
  }

  withPollingInterval(interval: number): InterfaceConfigurationBuilder {
    this.config.csvPollingInterval = interval;
    return this;
  }

  enabled(): InterfaceConfigurationBuilder {
    this.config.sourceIsEnabled = true;
    this.config.destinationIsEnabled = true;
    return this;
  }

  disabled(): InterfaceConfigurationBuilder {
    this.config.sourceIsEnabled = false;
    this.config.destinationIsEnabled = false;
    return this;
  }

  withField(field: string, value: any): InterfaceConfigurationBuilder {
    this.config[field] = value;
    return this;
  }

  build(): any {
    return { ...this.config };
  }
}
