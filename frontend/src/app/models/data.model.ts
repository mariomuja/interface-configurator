export interface CsvRecord {
  id: number;
  name: string;
  email: string;
  age: number;
  city: string;
  salary: number;
}

export interface SqlRecord {
  id: number;
  name: string;
  email: string;
  age: number;
  city: string;
  salary: number;
  createdAt: string;
}

export interface ProcessLog {
  id: number;
  timestamp: string;
  level: 'info' | 'warning' | 'error';
  message: string;
  details?: string;
  component?: string;
}


