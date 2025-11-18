import { Component, OnInit, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TransportService } from '../../services/transport.service';

@Component({
  selector: 'app-sql-schema-preview',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    MatIconModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule
  ],
  templateUrl: './sql-schema-preview.component.html',
  styleUrl: './sql-schema-preview.component.css'
})
export class SqlSchemaPreviewComponent implements OnInit {
  @Input() interfaceName: string = '';
  @Input() tableName: string = 'TransportData';

  schema: any = null;
  isLoading = false;
  error: string | null = null;

  displayedColumns: string[] = ['columnName', 'dataType', 'sqlTypeDefinition', 'precision', 'scale', 'isNullable'];

  constructor(private transportService: TransportService) {}

  ngOnInit(): void {
    if (this.interfaceName) {
      this.loadSchema();
    }
  }

  loadSchema(): void {
    if (!this.interfaceName) {
      this.error = 'Interface name is required';
      return;
    }

    this.isLoading = true;
    this.error = null;

    this.transportService.getSqlTableSchema(this.interfaceName, this.tableName).subscribe({
      next: (data) => {
        this.schema = data;
        this.isLoading = false;
      },
      error: (err) => {
        this.error = err.error?.error || err.message || 'Failed to load SQL schema';
        this.isLoading = false;
        console.error('Error loading SQL schema:', err);
      }
    });
  }

  getDataTypeColor(dataType: string): string {
    const type = dataType.toUpperCase();
    if (type.includes('INT') || type.includes('BIGINT')) return 'primary';
    if (type.includes('VARCHAR') || type.includes('CHAR') || type.includes('TEXT')) return 'accent';
    if (type.includes('DATE') || type.includes('TIME')) return 'warn';
    if (type.includes('DECIMAL') || type.includes('FLOAT') || type.includes('MONEY')) return 'primary';
    return '';
  }
}

