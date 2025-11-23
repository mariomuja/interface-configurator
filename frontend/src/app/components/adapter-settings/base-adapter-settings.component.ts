import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

/**
 * Base component for adapter settings
 * Provides common properties and methods shared by all adapter settings components
 */
@Component({
  selector: 'app-base-adapter-settings',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSlideToggleModule,
    MatIconModule,
    MatTooltipModule
  ],
  template: '' // Base component has no template - it's abstract
})
export abstract class BaseAdapterSettingsComponent {
  @Input() instanceName: string = '';
  @Input() isEnabled: boolean = true;
  @Input() adapterInstanceGuid: string = '';
  @Input() adapterType: 'Source' | 'Destination' = 'Source';
  
  @Output() instanceNameChange = new EventEmitter<string>();
  @Output() isEnabledChange = new EventEmitter<boolean>();
  @Output() settingsChange = new EventEmitter<any>();

  /**
   * Get the current settings as an object
   * Must be implemented by each adapter-specific component
   */
  abstract getSettings(): any;

  /**
   * Initialize settings from provided data
   * Must be implemented by each adapter-specific component
   */
  abstract initializeSettings(data: any): void;

  /**
   * Validate settings
   * Can be overridden by adapter-specific components
   */
  validateSettings(): { valid: boolean; errors: string[] } {
    const errors: string[] = [];
    
    if (!this.instanceName || this.instanceName.trim().length === 0) {
      errors.push('Instance name is required');
    }
    
    return {
      valid: errors.length === 0,
      errors
    };
  }

  onInstanceNameChange(value: string): void {
    this.instanceName = value;
    this.instanceNameChange.emit(value);
    this.emitSettingsChange();
  }

  onEnabledChange(value: boolean): void {
    this.isEnabled = value;
    this.isEnabledChange.emit(value);
    this.emitSettingsChange();
  }

  protected emitSettingsChange(): void {
    this.settingsChange.emit(this.getSettings());
  }
}

