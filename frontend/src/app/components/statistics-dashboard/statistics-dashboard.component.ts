import { Component, OnInit, OnDestroy, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { FormsModule } from '@angular/forms';
import { TransportService } from '../../services/transport.service';
import { interval, Subscription } from 'rxjs';

@Component({
  selector: 'app-statistics-dashboard',
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
    MatSlideToggleModule,
    FormsModule
  ],
  templateUrl: './statistics-dashboard.component.html',
  styleUrl: './statistics-dashboard.component.css'
})
export class StatisticsDashboardComponent implements OnInit, OnDestroy, OnChanges {
  @Input() interfaceName?: string;
  
  statistics: any = null;
  recentStats: any[] = [];
  isLoading = false;
  error: string | null = null;
  autoRefresh = true;
  refreshInterval = 30000; // 30 seconds
  private refreshSubscription?: Subscription;
  
  startDate?: Date;
  endDate?: Date;

  displayedColumns: string[] = ['processingEndTime', 'rowsProcessed', 'rowsSucceeded', 'rowsFailed', 'successRate', 'duration'];

  constructor(private transportService: TransportService) {}

  ngOnInit(): void {
    this.loadStatistics();
    if (this.autoRefresh) {
      this.startAutoRefresh();
    }
  }

  ngOnDestroy(): void {
    if (this.refreshSubscription) {
      this.refreshSubscription.unsubscribe();
    }
  }

  loadStatistics(): void {
    this.isLoading = true;
    this.error = null;

    const startDateStr = this.startDate ? this.startDate.toISOString() : undefined;
    const endDateStr = this.endDate ? this.endDate.toISOString() : undefined;

    this.transportService.getProcessingStatistics(this.interfaceName, startDateStr, endDateStr).subscribe({
      next: (data) => {
        if (this.interfaceName) {
          this.statistics = data;
        } else {
          this.recentStats = Array.isArray(data) ? data : [];
        }
        this.isLoading = false;
      },
      error: (err) => {
        this.error = err.error?.error || err.message || 'Failed to load statistics';
        this.isLoading = false;
        console.error('Error loading statistics:', err);
      }
    });
  }

  startAutoRefresh(): void {
    this.refreshSubscription = interval(this.refreshInterval).subscribe(() => {
      this.loadStatistics();
    });
  }

  stopAutoRefresh(): void {
    if (this.refreshSubscription) {
      this.refreshSubscription.unsubscribe();
      this.refreshSubscription = undefined;
    }
  }

  toggleAutoRefresh(): void {
    if (this.autoRefresh) {
      this.startAutoRefresh();
    } else {
      this.stopAutoRefresh();
    }
  }

  formatDuration(ms: number): string {
    if (ms < 1000) return `${ms}ms`;
    if (ms < 60000) return `${(ms / 1000).toFixed(1)}s`;
    return `${(ms / 60000).toFixed(1)}min`;
  }

  formatDate(date: string | Date): string {
    const d = typeof date === 'string' ? new Date(date) : date;
    return d.toLocaleString();
  }
}

