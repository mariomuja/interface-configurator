import { Component, Input, Output, EventEmitter } from '@angular/core';
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
export class AdapterCardComponent {
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

  @Output() instanceNameChange = new EventEmitter<string>();
  @Output() enabledChange = new EventEmitter<boolean>();
  @Output() receiveFolderChange = new EventEmitter<string>();
  @Output() restart = new EventEmitter<void>();
  @Output() expandedChange = new EventEmitter<boolean>();
  @Output() primaryAction = new EventEmitter<void>(); // For "Start Transport" or "Drop Table" buttons
  @Output() settingsClick = new EventEmitter<void>(); // For opening settings dialog

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
}

