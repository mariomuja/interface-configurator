# Automatisches Fehlerbehebungs-System - Integration Guide

## Übersicht

Dieses System ermöglicht:
1. **Automatisches Tracking** aller Funktionsaufrufe
2. **Detaillierte Fehlerberichte** mit Stack Traces und Kontext
3. **AI-Integration** zur automatischen Fehlerbehebung
4. **Benutzerfreundlicher Fehler-Dialog** mit "Fehler an AI übergeben"-Button

## Komponenten

### 1. ErrorTrackingService (`error-tracking.service.ts`)
- Trackt alle Funktionsaufrufe
- Erstellt detaillierte Fehlerberichte
- Speichert Historie in localStorage
- Übermittelt Fehler an Backend

### 2. ErrorDialogComponent (`error-dialog.component.ts`)
- Zeigt Fehlerdetails an
- Zeigt Funktionsaufruf-Historie
- Button "Fehler an AI zur Korrektur übergeben"

### 3. GlobalErrorHandlerService (`global-error-handler.service.ts`)
- Fängt alle unhandled Errors ab
- Zeigt automatisch Error-Dialog

### 4. Backend: SubmitErrorToAI Function
- Empfängt Fehlerberichte
- Loggt sie für AI-Verarbeitung

## Integration Steps

### Schritt 1: app.config.ts aktualisieren

```typescript
import { ApplicationConfig, ErrorHandler } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { GlobalErrorHandlerService } from './services/global-error-handler.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideHttpClient(),
    provideAnimations(),
    {
      provide: ErrorHandler,
      useClass: GlobalErrorHandlerService
    }
  ]
};
```

### Schritt 2: TransportComponent aktualisieren

**Imports hinzufügen:**
```typescript
import { ErrorTrackingService } from '../../services/error-tracking.service';
import { MatDialog } from '@angular/material/dialog';
import { ErrorDialogComponent } from '../error-dialog/error-dialog.component';
```

**Im Constructor injizieren:**
```typescript
constructor(
  // ... existing services
  private errorTrackingService: ErrorTrackingService,
  private dialog: MatDialog
) {}
```

**Fehlerbehandlung aktualisieren - Beispiel:**

```typescript
onFileSelected(event: Event): void {
  // ... existing code ...
  
  reader.onerror = () => {
    const error = new Error('Fehler beim Lesen der Datei');
    this.showErrorDialog(error, 'onFileSelected', 'TransportComponent', {
      fileName: file.name
    });
  };
}

// Neue Hilfsmethode
private showErrorDialog(
  error: Error | any,
  functionName: string,
  component: string,
  context?: any
): void {
  // Track error
  this.errorTrackingService.trackError(functionName, error, component, context);
  
  // Add application state
  this.errorTrackingService.addApplicationState('currentInterface', this.currentInterfaceName);
  this.errorTrackingService.addApplicationState('sourceEnabled', this.sourceIsEnabled);
  this.errorTrackingService.addApplicationState('destinationEnabled', this.destinationIsEnabled);
  
  // Show dialog
  this.dialog.open(ErrorDialogComponent, {
    width: '800px',
    maxWidth: '90vw',
    data: {
      error: error,
      functionName: functionName,
      component: component,
      context: context
    }
  });
}
```

**Alle catch-Blöcke aktualisieren:**

```typescript
// Vorher:
catch (error) {
  console.error('Error:', error);
  this.snackBar.open('Fehler', 'OK');
}

// Nachher:
catch (error) {
  console.error('Error:', error);
  this.showErrorDialog(error, 'functionName', 'TransportComponent', {
    // context data
  });
}
```

### Schritt 3: Service-Methoden tracken

**Option A: Manuell tracken**
```typescript
loadSqlData(): void {
  const startTime = performance.now();
  
  this.transportService.getSqlData().subscribe({
    next: (data) => {
      const duration = performance.now() - startTime;
      this.errorTrackingService.trackFunctionCall(
        'loadSqlData',
        'TransportComponent',
        {},
        data,
        duration
      );
      // ... rest of logic
    },
    error: (error) => {
      this.errorTrackingService.trackError(
        'loadSqlData',
        error,
        'TransportComponent',
        {}
      );
      this.showErrorDialog(error, 'loadSqlData', 'TransportComponent');
    }
  });
}
```

**Option B: Decorator verwenden (experimentell)**
```typescript
import { TrackFunction } from '../../decorators/track-function.decorator';

@TrackFunction('TransportComponent')
loadSqlData(): void {
  // ... code
  // Automatically tracked
}
```

### Schritt 4: HTTP Error Interceptor aktualisieren

Falls du einen HTTP Error Interceptor hast, aktualisiere ihn:

```typescript
import { ErrorTrackingService } from '../services/error-tracking.service';

intercept(req: HttpRequest<any>, next: HttpHandler): Observable<any> {
  return next.handle(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // Track error
      this.errorTrackingService.trackError(
        `${req.method} ${req.url}`,
        error,
        'HttpInterceptor',
        {
          url: req.url,
          method: req.method,
          status: error.status
        }
      );
      
      // ... rest of error handling
    })
  );
}
```

## Verwendung

### Automatisch (Global Error Handler)
Alle unhandled Errors werden automatisch gefangen und im Dialog angezeigt.

### Manuell
```typescript
try {
  // code
} catch (error) {
  this.showErrorDialog(error, 'functionName', 'ComponentName', {
    // optional context
  });
}
```

### Fehler an AI übergeben
1. Fehler tritt auf
2. Error-Dialog wird angezeigt
3. Benutzer klickt "Fehler an AI zur Korrektur übergeben"
4. Fehlerbericht wird an Backend gesendet
5. Backend loggt detaillierte Informationen
6. **Nächster Schritt**: AI kann die Logs abrufen und automatisch beheben

## Backend Integration

Die `SubmitErrorToAI` Function:
- Empfängt Fehlerberichte vom Frontend
- Loggt sie mit `_logger.LogInformation` (für AI-Zugriff)
- Kann erweitert werden für:
  - Blob Storage Speicherung
  - GitHub Issue Erstellung
  - Direkte AI API Calls

## Nächste Schritte für vollständige AI-Integration

1. **Log-Abruf-System**: AI kann Application Insights Logs abrufen
2. **Code-Analyse**: AI analysiert Fehlerbericht und Code
3. **Automatische Fixes**: AI erstellt Fixes und committet sie
4. **Testing**: AI testet Fixes automatisch
5. **Deployment**: Automatisches Deployment nach erfolgreichem Test

## Beispiel-Fehlerbericht JSON

```json
{
  "errorId": "ERR-1234567890-abc123",
  "timestamp": 1234567890,
  "userAgent": "Mozilla/5.0...",
  "url": "https://...",
  "functionCallHistory": [
    {
      "functionName": "loadSqlData",
      "component": "TransportComponent",
      "timestamp": 1234567880,
      "success": true,
      "duration": 150.5
    },
    {
      "functionName": "updateFieldSeparator",
      "component": "TransportComponent",
      "timestamp": 1234567890,
      "success": false,
      "error": {
        "message": "Cannot read property 'x' of undefined",
        "stack": "Error: ...",
        "name": "TypeError"
      }
    }
  ],
  "currentError": {
    "functionName": "updateFieldSeparator",
    "component": "TransportComponent",
    "error": {...},
    "stack": "...",
    "context": {...}
  },
  "applicationState": {
    "currentInterface": "TestInterface",
    "sourceEnabled": true,
    "destinationEnabled": false
  },
  "environment": {
    "apiUrl": "https://...",
    "browser": "Chrome",
    "platform": "Win32"
  }
}
```

## Troubleshooting

### Error-Dialog wird nicht angezeigt
- Prüfe, ob `GlobalErrorHandlerService` in `app.config.ts` registriert ist
- Prüfe Browser-Konsole für Fehler

### Fehlerberichte werden nicht gespeichert
- Prüfe localStorage (Browser DevTools)
- Prüfe, ob `ErrorTrackingService` korrekt injiziert ist

### Backend erhält keine Fehlerberichte
- Prüfe CORS-Einstellungen
- Prüfe Backend-Logs
- Prüfe Network-Tab im Browser


