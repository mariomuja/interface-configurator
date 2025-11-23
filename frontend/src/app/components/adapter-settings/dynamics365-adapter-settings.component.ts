import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { HttpClient } from '@angular/common/http';
import { BaseAdapterSettingsComponent } from './base-adapter-settings.component';

interface TargetSystem {
  id: string;
  name: string;
  description: string;
  endpoints: TargetSystemEndpoint[];
}

interface TargetSystemEndpoint {
  id: string;
  name: string;
  description: string;
  apiVersion: string;
  basePath: string;
  commonEntities: string[];
  moduleType: string;
  isCustom: boolean;
}

@Component({
  selector: 'app-dynamics365-adapter-settings',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatTooltipModule
  ],
  templateUrl: './dynamics365-adapter-settings.component.html',
  styleUrl: './dynamics365-adapter-settings.component.css'
})
export class Dynamics365AdapterSettingsComponent extends BaseAdapterSettingsComponent implements OnInit {
  // Target System Selection
  targetSystemId: string = 'Dynamics365';
  selectedEndpoint: TargetSystemEndpoint | null = null;
  availableEndpoints: TargetSystemEndpoint[] = [];
  
  // Dynamics 365 Authentication
  dynamics365TenantId: string = '';
  dynamics365ClientId: string = '';
  dynamics365ClientSecret: string = '';
  dynamics365InstanceUrl: string = '';
  
  // Module/Endpoint Configuration
  endpointId: string = '';
  entityName: string = '';
  dynamics365ODataFilter: string = '';
  
  // Options
  dynamics365BatchSize: number = 100;
  dynamics365PageSize: number = 50;
  dynamics365PollingInterval: number = 60;
  dynamics365UseBatch: boolean = true;

  constructor(private http: HttpClient) {
    super();
  }

  ngOnInit(): void {
    this.loadTargetSystems();
  }

  loadTargetSystems(): void {
    const functionAppUrl = (window as any).__FUNCTION_APP_URL__ || 'https://func-integration-main.azurewebsites.net';
    this.http.get<TargetSystem[]>(`${functionAppUrl}/api/GetTargetSystems`).subscribe({
      next: (systems) => {
        const dynamicsSystem = systems.find(s => s.id === 'Dynamics365');
        if (dynamicsSystem) {
          this.availableEndpoints = dynamicsSystem.endpoints;
          this.emitSettingsChange();
        }
      },
      error: (err) => {
        console.error('Error loading target systems:', err);
        // Fallback to default endpoints
        this.availableEndpoints = [
          { id: 'finance', name: 'Finance', description: 'Dynamics 365 Finance', apiVersion: 'v9.2', basePath: '/api/data/v9.2', commonEntities: ['accounts', 'invoices', 'customers', 'vendors'], moduleType: 'Finance', isCustom: false },
          { id: 'supplychain', name: 'Supply Chain Management', description: 'Dynamics 365 Supply Chain', apiVersion: 'v9.2', basePath: '/api/data/v9.2', commonEntities: ['products', 'inventory', 'purchaseOrders'], moduleType: 'SupplyChain', isCustom: false },
          { id: 'sales', name: 'Sales', description: 'Dynamics 365 Sales', apiVersion: 'v9.2', basePath: '/api/data/v9.2', commonEntities: ['accounts', 'contacts', 'leads', 'opportunities'], moduleType: 'Sales', isCustom: false },
          { id: 'custom', name: 'Custom Entity', description: 'Custom entity', apiVersion: 'v9.2', basePath: '/api/data/v9.2', commonEntities: [], moduleType: 'Custom', isCustom: true }
        ];
      }
    });
  }

  onEndpointChange(): void {
    this.selectedEndpoint = this.availableEndpoints.find(e => e.id === this.endpointId) || null;
    
    if (this.selectedEndpoint && this.selectedEndpoint.commonEntities.length > 0) {
      this.entityName = this.selectedEndpoint.commonEntities[0];
    } else {
      this.entityName = '';
    }
    
    this.emitSettingsChange();
  }

  onEntityChange(): void {
    this.emitSettingsChange();
  }

  override getSettings(): any {
    return {
      instanceName: this.instanceName,
      isEnabled: this.isEnabled,
      adapterInstanceGuid: this.adapterInstanceGuid,
      // Authentication
      dynamics365TenantId: this.dynamics365TenantId,
      dynamics365ClientId: this.dynamics365ClientId,
      dynamics365ClientSecret: this.dynamics365ClientSecret,
      dynamics365InstanceUrl: this.dynamics365InstanceUrl,
      // Module Configuration
      endpointId: this.endpointId,
      entityName: this.entityName,
      dynamics365ODataFilter: this.dynamics365ODataFilter,
      // Options
      dynamics365BatchSize: this.dynamics365BatchSize,
      dynamics365PageSize: this.dynamics365PageSize,
      dynamics365PollingInterval: this.dynamics365PollingInterval,
      dynamics365UseBatch: this.dynamics365UseBatch
    };
  }

  override initializeSettings(data: any): void {
    this.dynamics365TenantId = data.dynamics365TenantId || '';
    this.dynamics365ClientId = data.dynamics365ClientId || '';
    this.dynamics365ClientSecret = data.dynamics365ClientSecret || '';
    this.dynamics365InstanceUrl = data.dynamics365InstanceUrl || '';
    this.endpointId = data.endpointId || '';
    this.entityName = data.entityName || data.dynamics365EntityName || '';
    this.dynamics365ODataFilter = data.dynamics365ODataFilter || '';
    this.dynamics365BatchSize = data.dynamics365BatchSize || 100;
    this.dynamics365PageSize = data.dynamics365PageSize || 50;
    this.dynamics365PollingInterval = data.dynamics365PollingInterval || 60;
    this.dynamics365UseBatch = data.dynamics365UseBatch !== undefined ? data.dynamics365UseBatch : true;
    
    if (this.endpointId) {
      this.onEndpointChange();
    }
  }

  get availableEntities(): string[] {
    if (this.selectedEndpoint) {
      return this.selectedEndpoint.commonEntities;
    }
    return [];
  }

  get showCustomEntityInput(): boolean {
    return this.selectedEndpoint?.isCustom || (!this.availableEntities.includes(this.entityName) && this.entityName.length > 0);
  }
}

