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
  private readonly API_URL = 'https://func-integration-main.azurewebsites.net/api';

  constructor(
    private http: HttpClient,
    private authService: AuthService
  ) {}

  getFeatures(): Observable<Feature[]> {
    const headers = this.authService.getAuthHeaders();
    return this.http.get<Feature[]>(`${this.API_URL}/GetFeatures`, { headers });
  }

  toggleFeature(featureId: number): Observable<{ success: boolean }> {
    const headers = this.authService.getAuthHeaders();
    return this.http.post<{ success: boolean }>(
      `${this.API_URL}/ToggleFeature`,
      { featureId },
      { headers }
    );
  }

  updateTestComment(featureId: number, testComment: string): Observable<{ success: boolean }> {
    const headers = this.authService.getAuthHeaders();
    return this.http.post<{ success: boolean }>(
      `${this.API_URL}/UpdateFeatureTestComment`,
      { featureId, testComment },
      { headers }
    );
  }
}

