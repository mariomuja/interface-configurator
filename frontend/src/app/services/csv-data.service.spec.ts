import { TestBed } from '@angular/core/testing';
import { CsvDataService } from './csv-data.service';
import { CsvRecord } from '../models/data.model';

describe('CsvDataService', () => {
  let service: CsvDataService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(CsvDataService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should return field separator', () => {
    expect(service.getFieldSeparator()).toBe('‖');
  });

  it('should parse CSV line with default separator', () => {
    const line = 'Name‖Age‖City';
    const result = service.parseCsvLine(line);
    expect(result).toEqual(['Name', 'Age', 'City']);
  });

  it('should parse CSV line with custom separator', () => {
    const line = 'Name,Age,City';
    const result = service.parseCsvLine(line, ',');
    expect(result).toEqual(['Name', 'Age', 'City']);
  });

  it('should handle quoted values in CSV line', () => {
    const line = '"John Doe"‖30‖"New York"';
    const result = service.parseCsvLine(line);
    expect(result).toEqual(['John Doe', '30', 'New York']);
  });

  it('should handle escaped quotes in CSV line', () => {
    const line = '"John ""Johnny"" Doe"‖30';
    const result = service.parseCsvLine(line);
    expect(result).toEqual(['John "Johnny" Doe', '30']);
  });

  it('should convert records to CSV text', () => {
    const records: CsvRecord[] = [
      { ID: '1', Name: 'John', Age: '30' },
      { ID: '2', Name: 'Jane', Age: '25' }
    ];
    const result = service.convertRecordsToCsvText(records);
    expect(result).toContain('ID‖Name‖Age');
    expect(result).toContain('1‖John‖30');
    expect(result).toContain('2‖Jane‖25');
  });

  it('should escape separator in values', () => {
    const records: CsvRecord[] = [
      { Name: 'John‖Doe', Age: '30' }
    ];
    const result = service.convertRecordsToCsvText(records);
    expect(result).toContain('"John‖Doe"');
  });

  it('should format CSV as text', () => {
    const records: CsvRecord[] = [
      { ID: '1', Name: 'John' }
    ];
    const result = service.formatCsvAsText(records);
    expect(result).toContain('ID‖Name');
    expect(result).toContain('1‖John');
  });

  it('should return empty string for empty records', () => {
    expect(service.formatCsvAsText([])).toBe('');
    expect(service.convertRecordsToCsvText([])).toBe('');
  });

  it('should format CSV as HTML with colors', () => {
    const csvText = 'ID‖Name‖Age\n1‖John‖30';
    const result = service.formatCsvAsHtml(csvText);
    expect(result).toContain('<span');
    expect(result).toContain('ID');
    expect(result).toContain('John');
  });

  it('should handle empty CSV text in HTML formatting', () => {
    expect(service.formatCsvAsHtml('')).toBe('');
    expect(service.formatCsvAsHtml('   ')).toBe('');
  });

  it('should parse CSV text to records', () => {
    const csvText = 'ID‖Name‖Age\n1‖John‖30\n2‖Jane‖25';
    const result = service.parseCsvText(csvText);
    expect(result.length).toBe(2);
    expect(result[0]['ID']).toBe('1');
    expect(result[0]['Name']).toBe('John');
    expect(result[0]['Age']).toBe('30');
    expect(result[1]['ID']).toBe('2');
    expect(result[1]['Name']).toBe('Jane');
  });

  it('should handle quoted values in parsing', () => {
    const csvText = 'Name‖Age\n"John Doe"‖30';
    const result = service.parseCsvText(csvText);
    expect(result[0]['Name']).toBe('John Doe');
  });

  it('should generate sample CSV data', () => {
    const result = service.generateSampleCsvData();
    expect(result.length).toBe(5);
    expect(result[0]['ID']).toBe('1');
    expect(result[0]['Name']).toBe('John Doe');
    expect(result[0]['Email']).toBe('john.doe@example.com');
  });

  it('should handle empty CSV text in parsing', () => {
    expect(service.parseCsvText('')).toEqual([]);
    expect(service.parseCsvText('   ')).toEqual([]);
  });

  it('should handle CSV with only headers', () => {
    const csvText = 'ID‖Name‖Age';
    const result = service.parseCsvText(csvText);
    expect(result).toEqual([]);
  });

  it('should detect different separators in HTML formatting', () => {
    const csvText = 'ID,Name,Age\n1,John,30';
    const result = service.formatCsvAsHtml(csvText, ',');
    expect(result).toContain('ID');
    expect(result).toContain('John');
  });
});

