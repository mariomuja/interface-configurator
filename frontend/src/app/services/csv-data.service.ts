import { Injectable } from '@angular/core';
import { CsvRecord } from '../models/data.model';

@Injectable({
  providedIn: 'root'
})
export class CsvDataService {
  private readonly FIELD_SEPARATOR = '‖'; // Double Vertical Line (U+2016)
  
  // Color palette for CSV columns - dark, readable colors
  private readonly COLUMN_COLORS = [
    '#1a237e', // Dark blue
    '#b71c1c', // Dark red
    '#004d40', // Dark teal
    '#e65100', // Dark orange
    '#4a148c', // Dark purple
    '#006064', // Dark cyan
    '#3e2723', // Dark brown
    '#1b5e20', // Dark green
    '#880e4f', // Dark pink
    '#212121', // Dark gray
    '#0d47a1', // Blue
    '#c62828', // Red
    '#00695c', // Teal
    '#e64a19', // Orange
    '#6a1b9a', // Purple
  ];

  getFieldSeparator(): string {
    return this.FIELD_SEPARATOR;
  }

  /**
   * Parse CSV line into array of values
   */
  parseCsvLine(line: string, separator?: string): string[] {
    const sep = separator || this.FIELD_SEPARATOR;
    const values: string[] = [];
    let currentValue = '';
    let inQuotes = false;
    
    for (let i = 0; i < line.length; i++) {
      const char = line[i];
      
      if (char === '"') {
        if (inQuotes && line[i + 1] === '"') {
          // Escaped quote
          currentValue += '"';
          i++; // Skip next quote
        } else {
          // Toggle quote state
          inQuotes = !inQuotes;
        }
      } else if (char === sep && !inQuotes) {
        // Separator found outside quotes
        values.push(currentValue);
        currentValue = '';
      } else {
        currentValue += char;
      }
    }
    
    // Add last value
    values.push(currentValue);
    
    return values;
  }

  /**
   * Convert CSV records to text format
   */
  convertRecordsToCsvText(records: CsvRecord[], separator?: string): string {
    if (!records || records.length === 0) {
      return '';
    }

    const sep = separator || this.FIELD_SEPARATOR;
    const columns = this.extractColumns(records);
    
    // Build header row
    const headerRow = columns.join(sep);
    
    // Build data rows
    const dataRows = records.map(row => {
      return columns.map(col => {
        const value = this.getCellValue(row, col);
        const valueStr = String(value || '');
        // Escape separator and quotes in values
        if (valueStr.includes(sep) || valueStr.includes('"')) {
          return `"${valueStr.replace(/"/g, '""')}"`;
        }
        return valueStr;
      }).join(sep);
    });
    
    // Combine header and data rows
    return [headerRow, ...dataRows].join('\n');
  }

  /**
   * Format CSV data as text (for display)
   */
  formatCsvAsText(records: CsvRecord[], separator?: string): string {
    if (!records || records.length === 0) {
      return '';
    }

    return this.convertRecordsToCsvText(records, separator);
  }

  /**
   * Format CSV data as HTML with colored columns
   */
  formatCsvAsHtml(csvText: string, fieldSeparator?: string): string {
    if (!csvText || csvText.trim() === '') {
      return '';
    }

    try {
      const lines = csvText.split('\n').filter(line => line.trim() !== '');
      if (lines.length === 0) {
        return '';
      }

      // Detect separator from first line for display
      const firstLine = lines[0];
      let displaySeparator = fieldSeparator || this.FIELD_SEPARATOR;
      if (firstLine.includes(".'")) {
        displaySeparator = ".'";
      } else if (firstLine.includes('|')) {
        displaySeparator = '|';
      } else if (firstLine.includes(';')) {
        displaySeparator = ';';
      } else if (firstLine.includes(this.FIELD_SEPARATOR)) {
        displaySeparator = this.FIELD_SEPARATOR;
      }
      
      // Build HTML with colored columns
      const htmlLines = lines.map((line) => {
        const values = this.parseCsvLine(line, displaySeparator);
        const cells = values.map((value, colIndex) => {
          const color = this.COLUMN_COLORS[colIndex % this.COLUMN_COLORS.length];
          const escapedValue = this.escapeHtml(value.trim().replace(/^"|"$/g, ''));
          return `<span style="color: ${color};">${escapedValue}</span>`;
        });
        
        // Add separator between cells (use detected separator)
        const separator = `<span style="color: #999;">${this.escapeHtml(displaySeparator)}</span>`;
        return cells.join(separator);
      });

      return htmlLines.join('<br>');
    } catch (error) {
      console.error('Error formatting CSV as HTML:', error);
      return this.escapeHtml(csvText);
    }
  }

  /**
   * Parse CSV text into records
   */
  parseCsvText(csvText: string, separator?: string): CsvRecord[] {
    if (!csvText || csvText.trim() === '') {
      return [];
    }

    try {
      const lines = csvText.split('\n').filter(line => line.trim() !== '');
      if (lines.length === 0) {
        return [];
      }

      const sep = separator || this.FIELD_SEPARATOR;
      
      // Parse header row
      const headers = this.parseCsvLine(lines[0], sep).map(h => h.trim().replace(/^"|"$/g, ''));
      
      // Parse data rows
      const records: CsvRecord[] = [];
      for (let i = 1; i < lines.length; i++) {
        const values = this.parseCsvLine(lines[i], sep);
        const record: CsvRecord = {};
        
        headers.forEach((header, index) => {
          let value = values[index] || '';
          value = value.trim().replace(/^"|"$/g, '');
          record[header] = value;
        });
        
        records.push(record);
      }
      
      return records;
    } catch (error) {
      console.error('Error parsing CSV text:', error);
      return [];
    }
  }

  /**
   * Generate sample CSV data
   */
  generateSampleCsvData(): CsvRecord[] {
    return [
      { 'ID': '1', 'Name': 'John Doe', 'Email': 'john.doe@example.com', 'Age': '30', 'City': 'New York' },
      { 'ID': '2', 'Name': 'Jane Smith', 'Email': 'jane.smith@example.com', 'Age': '25', 'City': 'Los Angeles' },
      { 'ID': '3', 'Name': 'Bob Johnson', 'Email': 'bob.johnson@example.com', 'Age': '35', 'City': 'Chicago' },
      { 'ID': '4', 'Name': 'Alice Williams', 'Email': 'alice.williams@example.com', 'Age': '28', 'City': 'Houston' },
      { 'ID': '5', 'Name': 'Charlie Brown', 'Email': 'charlie.brown@example.com', 'Age': '32', 'City': 'Phoenix' }
    ];
  }

  /**
   * Extract all unique column names from records
   */
  private extractColumns(records: CsvRecord[]): string[] {
    const columns = new Set<string>();
    records.forEach(record => {
      Object.keys(record).forEach(key => columns.add(key));
    });
    return Array.from(columns);
  }

  /**
   * Get cell value from record
   */
  private getCellValue(record: CsvRecord, column: string): string {
    return record[column] || '';
  }

  /**
   * Escape HTML special characters
   */
  private escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }
}










