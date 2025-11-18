import { Component, OnInit, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TransportService } from '../../services/transport.service';

@Component({
  selector: 'app-csv-validation-results',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatListModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    MatIconModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule
  ],
  templateUrl: './csv-validation-results.component.html',
  styleUrl: './csv-validation-results.component.css'
})
export class CsvValidationResultsComponent implements OnInit {
  @Input() blobPath: string = '';
  @Input() delimiter?: string;

  validationResult: any = null;
  isLoading = false;
  error: string | null = null;

  constructor(private transportService: TransportService) {}

  ngOnInit(): void {
    if (this.blobPath) {
      this.validate();
    }
  }

  validate(): void {
    if (!this.blobPath) {
      this.error = 'Blob path is required';
      return;
    }

    this.isLoading = true;
    this.error = null;

    this.transportService.validateCsvFile(this.blobPath, this.delimiter).subscribe({
      next: (data) => {
        this.validationResult = data;
        this.isLoading = false;
      },
      error: (err) => {
        this.error = err.error?.error || err.message || 'Failed to validate CSV file';
        this.isLoading = false;
        console.error('Error validating CSV:', err);
      }
    });
  }

  getSeverityIcon(issue: string): string {
    if (issue.toLowerCase().includes('error') || issue.toLowerCase().includes('failed')) {
      return 'error';
    }
    if (issue.toLowerCase().includes('warning') || issue.toLowerCase().includes('warn')) {
      return 'warning';
    }
    return 'info';
  }

  getSeverityColor(issue: string): string {
    if (issue.toLowerCase().includes('error') || issue.toLowerCase().includes('failed')) {
      return 'warn';
    }
    if (issue.toLowerCase().includes('warning') || issue.toLowerCase().includes('warn')) {
      return 'accent';
    }
    return 'primary';
  }
}

