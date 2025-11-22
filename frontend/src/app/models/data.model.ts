// Dynamic CSV/SQL records - structure matches CSV columns exactly
export interface CsvRecord {
  [key: string]: any; // Dynamic structure based on CSV columns
}

export interface SqlRecord {
  id: string | number; // GUID or number
  [key: string]: any; // Dynamic structure - all CSV columns plus datetime_created
  datetime_created?: string;
  createdAt?: string; // Backward compatibility
}

export interface ProcessLog {
  id: number;
  timestamp: string; // Maps from backend datetime_created
  datetime_created?: string; // Backend field name
  level: 'info' | 'warning' | 'error' | string; // Backend may return other levels
  message: string;
  details?: string;
  component?: string;
  interfaceName?: string;
  messageId?: string;
}


