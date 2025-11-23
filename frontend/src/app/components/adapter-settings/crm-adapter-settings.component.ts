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
  selector: 'app-crm-adapter-settings',
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
  templateUrl: './crm-adapter-settings.component.html',
  styleUrl: './crm-adapter-settings.component.css'
})
export class CrmAdapterSettingsComponent extends BaseAdapterSettingsComponent implements OnInit {
  // Target System Selection
  targetSystemId: string = 'CRM';
  selectedEndpoint: TargetSystemEndpoint | null = null;
  availableEndpoints: TargetSystemEndpoint[] = [];
  
  // CRM Authentication
  crmOrganizationUrl: string = '';
  crmUsername: string = '';
  crmPassword: string = '';
  
  // Module/Endpoint Configuration
  endpointId: string = '';
  entityName: string = '';
  crmFetchXml: string = '';
  
  // Options
  crmBatchSize: number = 100;
  crmPollingInterval: number = 60;
  crmUseBatch: boolean = true;

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
        const crmSystem = systems.find(s => s.id === 'CRM');
        if (crmSystem) {
          this.availableEndpoints = crmSystem.endpoints;
          this.emitSettingsChange();
        }
      },
      error: (err) => {
        console.error('Error loading target systems:', err);
        // Fallback to default endpoints
        this.availableEndpoints = [
          { id: 'sales', name: 'Sales', description: 'CRM Sales entities', apiVersion: 'v9.2', basePath: '/api/data/v9.2', commonEntities: ['leads', 'opportunities', 'quotes', 'orders'], moduleType: 'Sales', isCustom: false },
          { id: 'service', name: 'Customer Service', description: 'CRM Service entities', apiVersion: 'v9.2', basePath: '/api/data/v9.2', commonEntities: ['cases', 'knowledgearticles', 'queues'], moduleType: 'Service', isCustom: false },
          { id: 'marketing', name: 'Marketing', description: 'CRM Marketing entities', apiVersion: 'v9.2', basePath: '/api/data/v9.2', commonEntities: ['campaigns', 'marketinglists', 'contacts'], moduleType: 'Marketing', isCustom: false },
          { id: 'custom', name: 'Custom Entity', description: 'Custom CRM entity', apiVersion: 'v9.2', basePath: '/api/data/v9.2', commonEntities: [], moduleType: 'Custom', isCustom: true }
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
      crmOrganizationUrl: this.crmOrganizationUrl,
      crmUsername: this.crmUsername,
      crmPassword: this.crmPassword,
      // Module Configuration
      endpointId: this.endpointId,
      entityName: this.entityName,
      crmFetchXml: this.crmFetchXml,
      // Options
      crmBatchSize: this.crmBatchSize,
      crmPollingInterval: this.crmPollingInterval,
      crmUseBatch: this.crmUseBatch
    };
  }

  override initializeSettings(data: any): void {
    this.crmOrganizationUrl = data.crmOrganizationUrl || '';
    this.crmUsername = data.crmUsername || '';
    this.crmPassword = data.crmPassword || '';
    this.endpointId = data.endpointId || '';
    this.entityName = data.entityName || data.crmEntityName || '';
    this.crmFetchXml = data.crmFetchXml || '';
    this.crmBatchSize = data.crmBatchSize || 100;
    this.crmPollingInterval = data.crmPollingInterval || 60;
    this.crmUseBatch = data.crmUseBatch !== undefined ? data.crmUseBatch : true;
    
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

