import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

export interface VersionInfo {
  version: string;
  buildNumber: number;
  lastUpdated: string;
}

@Injectable({
  providedIn: 'root'
})
export class VersionService {
  private versionCache: VersionInfo | null = null;

  constructor(private http: HttpClient) {}

  getVersion(): Observable<VersionInfo> {
    if (this.versionCache) {
      return of(this.versionCache);
    }

    return this.http.get<VersionInfo>('/assets/version.json').pipe(
      map((version: VersionInfo) => {
        this.versionCache = version;
        return version;
      }),
      catchError(() => {
        // Fallback if version.json is not available
        const fallback: VersionInfo = {
          version: '1.0.0',
          buildNumber: 0,
          lastUpdated: new Date().toISOString()
        };
        this.versionCache = fallback;
        return of(fallback);
      })
    );
  }

  getVersionString(): Observable<string> {
    return this.getVersion().pipe(
      map(v => `v${v.version} (build ${v.buildNumber})`)
    );
  }
}



