import { Component, Input, Output, EventEmitter, OnChanges, SimpleChanges, ViewChild, ElementRef, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

export type AdapterType = 'Source' | 'Destination';
export type AdapterName = 'CSV' | 'SqlServer';

@Component({
  selector: 'app-adapter-card',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatExpansionModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatSlideToggleModule,
    MatTooltipModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './adapter-card.component.html',
  styleUrl: './adapter-card.component.css'
})
export class AdapterCardComponent implements OnChanges, AfterViewInit {
  @ViewChild('csvEditor', { static: false }) csvEditor?: ElementRef<HTMLDivElement>;
  @Input() adapterType: AdapterType = 'Source';
  @Input() adapterName: AdapterName = 'CSV';
  @Input() instanceName: string = '';
  @Input() isEnabled: boolean = true;
  @Input() receiveFolder: string = '';
  @Input() adapterInstanceGuid: string = '';
  @Input() isRestarting: boolean = false;
  @Input() isLoading: boolean = false;
  @Input() expanded: boolean = true;
  @Input() showReceiveFolder: boolean = false; // Only show for CSV Source adapters
  @Input() cardContent: string = ''; // Content to display in the card body (e.g., CSV text or table)
  @Input() csvData: string = ''; // CSV data content (bound to CsvData property)
  @Input() isDisabled: boolean = false; // If true, card appears greyed out (for unsupported multiple destinations)
  @Input() primaryActionDisabled: boolean = false; // If true, the primary action button is disabled

  @Output() instanceNameChange = new EventEmitter<string>();
  @Output() enabledChange = new EventEmitter<boolean>();
  @Output() receiveFolderChange = new EventEmitter<string>();
  @Output() csvDataChange = new EventEmitter<string>(); // Emit when CSV data changes
  @Output() restart = new EventEmitter<void>();
  @Output() expandedChange = new EventEmitter<boolean>();
  @Output() primaryAction = new EventEmitter<void>(); // For "Start Transport" or "Drop Table" buttons
  @Output() settingsClick = new EventEmitter<void>(); // For opening settings dialog

  private readonly FIELD_SEPARATOR = '║';
  private readonly COLUMN_COLORS = [
    '#1a237e', '#b71c1c', '#004d40', '#e65100', '#4a148c',
    '#006064', '#3e2723', '#1b5e20', '#880e4f', '#212121',
    '#0d47a1', '#c62828', '#00695c', '#e64a19', '#6a1b9a'
  ];
  private hasViewInitialized = false;

  onInstanceNameBlur(): void {
    this.instanceNameChange.emit(this.instanceName);
  }

  onInstanceNameEnter(): void {
    this.instanceNameChange.emit(this.instanceName);
  }

  onEnabledChange(): void {
    this.enabledChange.emit(this.isEnabled);
  }

  onReceiveFolderBlur(): void {
    if (this.showReceiveFolder) {
      this.receiveFolderChange.emit(this.receiveFolder);
    }
  }

  onReceiveFolderEnter(): void {
    if (this.showReceiveFolder) {
      this.receiveFolderChange.emit(this.receiveFolder);
    }
  }

  onRestart(): void {
    this.restart.emit();
  }

  onExpandedChange(expanded: boolean): void {
    this.expandedChange.emit(expanded);
  }

  onPrimaryAction(): void {
    this.primaryAction.emit();
  }

  onSettings(): void {
    this.settingsClick.emit();
  }

  onCsvDataInput(event: Event): void {
    const element = event.target as HTMLElement;
    if (element) {
      this.csvData = element.textContent || '';
    }
  }

  onCsvDataBlur(): void {
    this.csvData = this.csvEditor?.nativeElement.textContent || this.csvData || '';
    this.csvDataChange.emit(this.csvData);
    setTimeout(() => this.renderCsvData(), 0);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['csvData'] && this.hasViewInitialized) {
      this.renderCsvData();
    }
  }

  ngAfterViewInit(): void {
    this.hasViewInitialized = true;
    this.renderCsvData();
  }

  getPrimaryActionLabel(): string {
    if (this.adapterType === 'Source') {
      return 'Transport starten';
    } else {
      return 'Tabelle löschen';
    }
  }

  getPrimaryActionIcon(): string {
    if (this.adapterType === 'Source') {
      return 'play_arrow';
    } else {
      return 'delete_forever';
    }
  }

  getPrimaryActionColor(): string {
    if (this.adapterType === 'Source') {
      return 'primary';
    } else {
      return 'accent';
    }
  }

  private renderCsvData(): void {
    if (!this.csvEditor) {
      return;
    }

    if (!this.csvData) {
      this.csvEditor.nativeElement.innerHTML = '';
      return;
    }

    this.csvEditor.nativeElement.innerHTML = this.formatCsvAsHtml(this.csvData);
  }

  private formatCsvAsHtml(csvText: string): string {
    const lines = csvText.split(/\r?\n/);
    if (!lines.length) {
      return '';
    }

    const htmlLines = lines.map((line) => {
      if (line.trim() === '') {
        return '<div><br></div>';
      }
      const values = this.parseCsvLine(line);
      const cells = values.map((value, index) => {
        const color = this.COLUMN_COLORS[index % this.COLUMN_COLORS.length];
        const sanitized = this.escapeHtml(value.trim().replace(/^"|"$/g, ''));
        return `<span style="color:${color};">${sanitized}</span>`;
      });
      const separator = `<span style="color:#999;">${this.FIELD_SEPARATOR}</span>`;
      return `<div>${cells.join(separator)}</div>`;
    });

    return htmlLines.join('');
  }

  private parseCsvLine(line: string): string[] {
    const values: string[] = [];
    let current = '';
    let inQuotes = false;

    for (let i = 0; i < line.length; i++) {
      const char = line[i];
      const nextChar = line[i + 1];

      if (char === '"') {
        if (inQuotes && nextChar === '"') {
          current += '"';
          i++;
        } else {
          inQuotes = !inQuotes;
        }
      } else if (char === this.FIELD_SEPARATOR && !inQuotes) {
        values.push(current);
        current = '';
      } else {
        current += char;
      }
    }

    values.push(current);
    return values;
  }

  private escapeHtml(text: string): string {
    return text
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }
}

