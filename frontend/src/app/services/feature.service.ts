import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthService } from './auth.service';

export interface Feature {
  id: number;
  featureNumber: number;
  title: string;
  description: string;
  detailedDescription: string;
  technicalDetails?: string;
  testInstructions?: string;
  knownIssues?: string;
  dependencies?: string;
  breakingChanges?: string;
  screenshots?: string;
  category: string;
  priority: string;
  isEnabled: boolean;
  implementedDate: string;
  enabledDate?: string;
  enabledBy?: string;
  testComment?: string; // Test result comment from tester - visible to all users
  testCommentBy?: string; // Username of the tester who wrote the comment
  testCommentDate?: string; // Date when the test comment was last updated
  canToggle: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class FeatureService {
  private readonly apiUrl = this.getApiUrl();

  constructor(
    private http: HttpClient,
    private authService: AuthService
  ) {}

  getFeatures(): Observable<Feature[]> {
    const headers = this.authService.getAuthHeaders();
    return this.http.get<Feature[]>(`${this.apiUrl}/GetFeatures`, { headers });
  }

  toggleFeature(featureId: number): Observable<{ success: boolean }> {
    const headers = this.authService.getAuthHeaders();
    return this.http.post<{ success: boolean }>(
      `${this.apiUrl}/ToggleFeature`,
      { featureId },
      { headers }
    );
  }

  updateTestComment(featureId: number, testComment: string): Observable<{ success: boolean }> {
    const headers = this.authService.getAuthHeaders();
    return this.http.post<{ success: boolean }>(
      `${this.apiUrl}/UpdateFeatureTestComment`,
      { featureId, testComment },
      { headers }
    );
  }

  private getApiUrl(): string {
    const configured = this.normalizeBaseUrl(this.readGlobalApiBaseUrl());
    if (configured) {
      return configured;
    }

    if (typeof window !== 'undefined') {
      const localOverride = this.normalizeBaseUrl(
        window.localStorage?.getItem('interfaceConfigurator.apiBaseUrl') ?? undefined
      );
      if (localOverride) {
        return localOverride;
      }

      const hostname = window.location.hostname.toLowerCase();
      if (hostname === 'localhost' || hostname === '127.0.0.1') {
        return 'http://localhost:7071/api';
      }

      return `${window.location.origin.replace(/\/$/, '')}/api`;
    }

    return 'https://func-integration-main.azurewebsites.net/api';
  }

  private readGlobalApiBaseUrl(): string | undefined {
    if (typeof window === 'undefined') {
      return undefined;
    }

    const globalWindow = window as any;
    return (
      globalWindow.INTERFACE_CONFIGURATOR_API_BASE_URL ??
      globalWindow.__interfaceConfiguratorApiBaseUrl ??
      globalWindow.__interfaceConfigurator?.apiBaseUrl ??
      globalWindow.__env?.API_BASE_URL ??
      globalWindow.__env?.apiBaseUrl
    );
  }

  private normalizeBaseUrl(url?: string): string | undefined {
    if (!url) {
      return undefined;
    }

    const trimmed = url.trim();
    if (!trimmed) {
      return undefined;
    }

    return trimmed.replace(/\/+$/, '');
  }
}

