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
import { MatTabsModule } from '@angular/material/tabs';
import { TransportService } from '../../services/transport.service';

@Component({
  selector: 'app-schema-comparison',
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
    MatTabsModule
  ],
  templateUrl: './schema-comparison.component.html',
  styleUrl: './schema-comparison.component.css'
})
export class SchemaComparisonComponent implements OnInit {
  @Input() interfaceName: string = '';
  @Input() csvBlobPath: string = '';
  @Input() tableName: string = 'TransportData';

  comparisonResult: any = null;
  isLoading = false;
  error: string | null = null;

  displayedColumns: string[] = ['columnName', 'csvType', 'sqlType', 'status'];

  constructor(private transportService: TransportService) {}

  ngOnInit(): void {
    if (this.interfaceName && this.csvBlobPath) {
      this.compare();
    }
  }

  compare(): void {
    if (!this.interfaceName || !this.csvBlobPath) {
      this.error = 'Interface name and CSV blob path are required';
      return;
    }

    this.isLoading = true;
    this.error = null;

    this.transportService.compareCsvSqlSchema(this.interfaceName, this.csvBlobPath, this.tableName).subscribe({
      next: (data) => {
        this.comparisonResult = data;
        this.isLoading = false;
      },
      error: (err) => {
        this.error = err.error?.error || err.message || 'Failed to compare schemas';
        this.isLoading = false;
        console.error('Error comparing schemas:', err);
      }
    });
  }

  getTypeMismatchDataSource(): any[] {
    if (!this.comparisonResult?.typeMismatches) return [];
    return this.comparisonResult.typeMismatches.map((m: any) => ({
      columnName: m.columnName,
      csvType: m.csvType,
      sqlType: m.sqlType,
      csvSqlTypeDefinition: m.csvSqlTypeDefinition,
      sqlSqlTypeDefinition: m.sqlSqlTypeDefinition,
      status: 'Mismatch'
    }));
  }
}

