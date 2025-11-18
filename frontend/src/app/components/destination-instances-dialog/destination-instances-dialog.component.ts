import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatChipsModule } from '@angular/material/chips';

export interface DestinationAdapterInstance {
  adapterInstanceGuid: string;
  instanceName: string;
  adapterName: 'CSV' | 'SqlServer';
  isEnabled: boolean;
  configuration?: any;
}

export interface DestinationInstancesDialogData {
  instances: DestinationAdapterInstance[];
  availableAdapters: { name: 'CSV' | 'SqlServer'; displayName: string; icon: string }[];
}

@Component({
  selector: 'app-destination-instances-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatTooltipModule,
    MatChipsModule
  ],
  templateUrl: './destination-instances-dialog.component.html',
  styleUrl: './destination-instances-dialog.component.css'
})
export class DestinationInstancesDialogComponent implements OnInit {
  instances: DestinationAdapterInstance[] = [];
  availableAdapters: { name: 'CSV' | 'SqlServer'; displayName: string; icon: string }[] = [
    { name: 'CSV', displayName: 'CSV', icon: 'description' },
    { name: 'SqlServer', displayName: 'SQL Server', icon: 'storage' }
  ];

  constructor(
    public dialogRef: MatDialogRef<DestinationInstancesDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: DestinationInstancesDialogData
  ) {}

  ngOnInit(): void {
    this.instances = JSON.parse(JSON.stringify(this.data.instances || [])); // Deep copy
    this.availableAdapters = this.data.availableAdapters || this.availableAdapters;
  }

  onAddAdapter(adapterName: 'CSV' | 'SqlServer'): void {
    const newInstance: DestinationAdapterInstance = {
      adapterInstanceGuid: this.generateGuid(),
      instanceName: this.getDefaultInstanceName(adapterName),
      adapterName: adapterName,
      isEnabled: true,
      configuration: {}
    };
    this.instances.push(newInstance);
  }

  onRemoveAdapter(instanceGuid: string): void {
    const index = this.instances.findIndex(i => i.adapterInstanceGuid === instanceGuid);
    if (index !== -1) {
      this.instances.splice(index, 1);
    }
  }

  onOpenSettings(instance: DestinationAdapterInstance, event: Event): void {
    event.stopPropagation();
    // Emit event to parent component to open settings dialog
    this.dialogRef.close({ action: 'settings', instance });
  }

  onSave(): void {
    this.dialogRef.close({ action: 'save', instances: this.instances });
  }

  onCancel(): void {
    this.dialogRef.close();
  }

  getAdapterIcon(adapterName: 'CSV' | 'SqlServer'): string {
    const adapter = this.availableAdapters.find(a => a.name === adapterName);
    return adapter?.icon || 'extension';
  }

  getAdapterDisplayName(adapterName: 'CSV' | 'SqlServer'): string {
    const adapter = this.availableAdapters.find(a => a.name === adapterName);
    return adapter?.displayName || adapterName;
  }

  private generateGuid(): string {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
      const r = Math.random() * 16 | 0;
      const v = c === 'x' ? r : (r & 0x3 | 0x8);
      return v.toString(16);
    });
  }

  private getDefaultInstanceName(adapterName: 'CSV' | 'SqlServer'): string {
    // Count all existing instances to determine the next number
    const totalCount = this.instances.length;
    let counter = totalCount + 1;
    let name = `Destination ${counter}`;
    
    // Ensure uniqueness by checking if name already exists
    const existingNames = this.instances.map(i => i.instanceName);
    while (existingNames.includes(name)) {
      counter++;
      name = `Destination ${counter}`;
    }
    
    return name;
  }
}

