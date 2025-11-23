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
  selector: 'app-sap-adapter-settings',
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
  templateUrl: './sap-adapter-settings.component.html',
  styleUrl: './sap-adapter-settings.component.css'
})
export class SapAdapterSettingsComponent extends BaseAdapterSettingsComponent implements OnInit {
  // Target System Selection
  targetSystemId: string = 'SAP';
  selectedEndpoint: TargetSystemEndpoint | null = null;
  availableEndpoints: TargetSystemEndpoint[] = [];
  
  // SAP Connection Properties
  sapApplicationServer: string = '';
  sapSystemNumber: string = '';
  sapClient: string = '';
  sapUsername: string = '';
  sapPassword: string = '';
  sapLanguage: string = 'EN';
  
  // SAP Endpoint Configuration
  endpointId: string = '';
  entityName: string = '';
  sapODataServiceUrl: string = '';
  sapRestApiEndpoint: string = '';
  sapRfcFunctionModule: string = '';
  sapRfcParameters: string = '';
  sapIdocType: string = '';
  sapIdocFilter: string = '';
  
  // SAP Options
  sapUseOData: boolean = false;
  sapUseRestApi: boolean = false;
  sapUseRfc: boolean = true;
  sapBatchSize: number = 100;
  sapPollingInterval: number = 60;
  sapConnectionTimeout: number = 30;

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
        const sapSystem = systems.find(s => s.id === 'SAP');
        if (sapSystem) {
          this.availableEndpoints = sapSystem.endpoints;
          this.emitSettingsChange();
        }
      },
      error: (err) => {
        console.error('Error loading target systems:', err);
        // Fallback to default endpoints
        this.availableEndpoints = [
          { id: 'odata', name: 'OData Service (S/4HANA)', description: 'SAP S/4HANA OData Service', apiVersion: 'v2', basePath: '/sap/opu/odata/sap', commonEntities: ['SalesOrder', 'PurchaseOrder', 'Material', 'Customer', 'Vendor'], moduleType: 'OData', isCustom: false },
          { id: 'restapi', name: 'REST API (S/4HANA)', description: 'SAP S/4HANA REST API', apiVersion: 'v1', basePath: '/sap/bc/rest', commonEntities: ['SalesOrder', 'PurchaseOrder', 'Material'], moduleType: 'REST', isCustom: false },
          { id: 'rfc', name: 'RFC Gateway', description: 'SAP RFC Gateway', apiVersion: '1.0', basePath: '/sap/bc/soap/rfc', commonEntities: ['IDOC_INBOUND_ASYNCHRONOUS', 'BAPI_SALESORDER_CREATEFROMDAT2'], moduleType: 'RFC', isCustom: false },
          { id: 'idoc', name: 'IDOC', description: 'SAP IDOC', apiVersion: '1.0', basePath: '/sap/bc/idoc', commonEntities: ['ORDERS05', 'INVOIC02', 'MATMAS05'], moduleType: 'IDOC', isCustom: false }
        ];
      }
    });
  }

  onEndpointChange(): void {
    this.selectedEndpoint = this.availableEndpoints.find(e => e.id === this.endpointId) || null;
    
    if (this.selectedEndpoint) {
      // Auto-configure based on endpoint type
      if (this.selectedEndpoint.moduleType === 'OData') {
        this.sapUseOData = true;
        this.sapUseRestApi = false;
        this.sapUseRfc = false;
        if (this.selectedEndpoint.commonEntities.length > 0) {
          this.entityName = this.selectedEndpoint.commonEntities[0];
        }
      } else if (this.selectedEndpoint.moduleType === 'REST') {
        this.sapUseOData = false;
        this.sapUseRestApi = true;
        this.sapUseRfc = false;
      } else if (this.selectedEndpoint.moduleType === 'RFC') {
        this.sapUseOData = false;
        this.sapUseRestApi = false;
        this.sapUseRfc = true;
        if (this.selectedEndpoint.commonEntities.length > 0) {
          this.sapRfcFunctionModule = this.selectedEndpoint.commonEntities[0];
        }
      } else if (this.selectedEndpoint.moduleType === 'IDOC') {
        this.sapUseOData = false;
        this.sapUseRestApi = false;
        this.sapUseRfc = false;
        if (this.selectedEndpoint.commonEntities.length > 0) {
          this.sapIdocType = this.selectedEndpoint.commonEntities[0];
        }
      }
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
      // SAP Connection
      sapApplicationServer: this.sapApplicationServer,
      sapSystemNumber: this.sapSystemNumber,
      sapClient: this.sapClient,
      sapUsername: this.sapUsername,
      sapPassword: this.sapPassword,
      sapLanguage: this.sapLanguage,
      // Endpoint Configuration
      endpointId: this.endpointId,
      entityName: this.entityName,
      sapODataServiceUrl: this.sapODataServiceUrl || (this.selectedEndpoint?.basePath ? `${this.selectedEndpoint.basePath}/${this.entityName}` : ''),
      sapRestApiEndpoint: this.sapRestApiEndpoint || (this.selectedEndpoint?.basePath ? `${this.selectedEndpoint.basePath}/${this.entityName}` : ''),
      sapRfcFunctionModule: this.sapRfcFunctionModule,
      sapRfcParameters: this.sapRfcParameters,
      sapIdocType: this.sapIdocType,
      sapIdocFilter: this.sapIdocFilter,
      // Options
      sapUseOData: this.sapUseOData,
      sapUseRestApi: this.sapUseRestApi,
      sapUseRfc: this.sapUseRfc,
      sapBatchSize: this.sapBatchSize,
      sapPollingInterval: this.sapPollingInterval,
      sapConnectionTimeout: this.sapConnectionTimeout
    };
  }

  override initializeSettings(data: any): void {
    this.sapApplicationServer = data.sapApplicationServer || '';
    this.sapSystemNumber = data.sapSystemNumber || '';
    this.sapClient = data.sapClient || '';
    this.sapUsername = data.sapUsername || '';
    this.sapPassword = data.sapPassword || '';
    this.sapLanguage = data.sapLanguage || 'EN';
    this.endpointId = data.endpointId || '';
    this.entityName = data.entityName || data.sapEntityName || '';
    this.sapODataServiceUrl = data.sapODataServiceUrl || '';
    this.sapRestApiEndpoint = data.sapRestApiEndpoint || '';
    this.sapRfcFunctionModule = data.sapRfcFunctionModule || '';
    this.sapRfcParameters = data.sapRfcParameters || '';
    this.sapIdocType = data.sapIdocType || '';
    this.sapIdocFilter = data.sapIdocFilter || '';
    this.sapUseOData = data.sapUseOData || false;
    this.sapUseRestApi = data.sapUseRestApi || false;
    this.sapUseRfc = data.sapUseRfc !== undefined ? data.sapUseRfc : true;
    this.sapBatchSize = data.sapBatchSize || 100;
    this.sapPollingInterval = data.sapPollingInterval || 60;
    this.sapConnectionTimeout = data.sapConnectionTimeout || 30;
    
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

  get showODataFields(): boolean {
    return this.sapUseOData && this.selectedEndpoint?.moduleType === 'OData';
  }

  get showRestApiFields(): boolean {
    return this.sapUseRestApi && this.selectedEndpoint?.moduleType === 'REST';
  }

  get showRfcFields(): boolean {
    return this.sapUseRfc && this.selectedEndpoint?.moduleType === 'RFC';
  }

  get showIdocFields(): boolean {
    return !this.sapUseOData && !this.sapUseRestApi && !this.sapUseRfc && this.selectedEndpoint?.moduleType === 'IDOC';
  }
}

