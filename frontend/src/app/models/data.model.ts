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
  timestamp: string;
  level: 'info' | 'warning' | 'error';
  message: string;
  details?: string;
  component?: string;
}


