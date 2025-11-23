-- Initialize Default Features for Feature Management System
-- This script creates example features for testing the feature management system
-- Run this script after the Features and Users tables have been created

-- Note: Features are inserted with IsEnabled = 0 (disabled by default)
-- Only admins can enable features via the UI

-- Feature 1: Automatische Versionierung
IF NOT EXISTS (SELECT * FROM Features WHERE FeatureNumber = 1)
BEGIN
    INSERT INTO Features (
        FeatureNumber, Title, Description, DetailedDescription, 
        TechnicalDetails, TestInstructions, KnownIssues, Dependencies, 
        Category, Priority, IsEnabled, ImplementedDate
    )
    VALUES (
        1,
        'Automatische Versionierung',
        'Automatische Versionsverwaltung mit GitHub Actions - Version wird bei jedem Push zu main erhöht',
        '**Was wurde implementiert:**
Diese Funktion implementiert ein vollständiges Versionsverwaltungssystem für die Anwendung.

**Hauptfunktionen:**
- Automatische Versionserhöhung bei jedem Push zu GitHub main Branch
- Version wird in version.json, package.json und .csproj Dateien aktualisiert
- Build-Nummer wird inkrementiert
- Version wird im App-Header angezeigt (Format: v1.0.0 (build 5))

**Technische Details:**
- GitHub Action Workflow (.github/workflows/version-bump.yml) überwacht Pushes zu main
- Version wird in frontend/src/assets/version.json gespeichert
- Angular VersionService liest die Version zur Laufzeit
- Pre-Build-Skript kopiert version.json automatisch in Assets

**Vorteile:**
- Immer aktuelle Versionsnummer sichtbar
- Nachvollziehbarkeit welche Version deployed ist
- Automatische Versionsverwaltung ohne manuellen Aufwand',
        '**Implementierung:**
- Root: version.json mit version, buildNumber, lastUpdated
- GitHub Action: Automatisches Bumping bei Push zu main
- Frontend: VersionService liest /assets/version.json
- Build: scripts/copy-version.js kopiert version.json vor jedem Build

**Dateien:**
- version.json (Root)
- .github/workflows/version-bump.yml
- frontend/src/app/services/version.service.ts
- scripts/copy-version.js
- frontend/src/app/app.component.ts (Version-Anzeige)',
        '**Testanweisungen:**

1. **Version-Anzeige prüfen:**
   - App öffnen und im Header die Versionsnummer prüfen
   - Format sollte sein: v1.0.0 (build 0) oder höher

2. **Automatisches Bumping testen:**
   - Änderung zu main pushen
   - GitHub Action sollte automatisch Version erhöhen
   - Nach Deployment sollte neue Version im Header erscheinen

3. **Build-Integration testen:**
   - Lokal `npm run build` ausführen
   - Prüfen ob version.json in dist/assets/ vorhanden ist

**Erwartete Ergebnisse:**
- Version wird korrekt angezeigt
- Bei jedem Push wird Version automatisch erhöht
- Build-Nummer wird inkrementiert',
        '**Bekannte Probleme:**
- Keine bekannten Probleme

**Hinweise:**
- GitHub Action benötigt GITHUB_TOKEN Berechtigung
- Version-Bump wird nur bei Push zu main ausgeführt (nicht bei anderen Branches)',
        NULL,
        'DevOps',
        'Medium',
        0, -- IsEnabled: disabled by default
        GETUTCDATE()
    );
    PRINT 'Feature #1: Automatische Versionierung erstellt';
END
ELSE
BEGIN
    PRINT 'Feature #1: Automatische Versionierung bereits vorhanden';
END
GO

-- Feature 2: AI Auto-Fix System
IF NOT EXISTS (SELECT * FROM Features WHERE FeatureNumber = 2)
BEGIN
    INSERT INTO Features (
        FeatureNumber, Title, Description, DetailedDescription,
        TechnicalDetails, TestInstructions, KnownIssues, Dependencies,
        Category, Priority, IsEnabled, ImplementedDate
    )
    VALUES (
        2,
        'AI Auto-Fix System',
        'Automatisches Fehlerbehebungs-System mit AI-gestützter Analyse, Fix-Generierung und automatischem Testing',
        '**Was wurde implementiert:**
Ein vollständiges System zur automatischen Fehlerbehebung durch AI-gestützte Analyse und Fix-Generierung.

**Hauptfunktionen:**
1. **Error Tracking Service (Frontend):**
   - Trackt alle Funktionsaufrufe automatisch
   - Speichert Funktionshistorie in localStorage
   - Erstellt detaillierte Error-Reports mit Stack-Traces

2. **Error Dialog Component:**
   - Zeigt detaillierte Fehlerinformationen
   - Button "Fehler an AI zur Korrektur übergeben"
   - Download/Copy-Funktion für Error-Reports

3. **Backend AI Services:**
   - ErrorAnalysisService: Analysiert Fehler und identifiziert betroffene Code-Stellen
   - AutoFixService: Wendet vorgeschlagene Fixes automatisch an
   - AutoTestService: Führt Tests aus um Fixes zu verifizieren

4. **API Endpoints:**
   - SubmitErrorToAI: Empfängt Error-Reports vom Frontend
   - ProcessErrorForAI: Orchestriert Analyse -> Fix -> Test Pipeline',
        '**Frontend:**
- frontend/src/app/services/error-tracking.service.ts
- frontend/src/app/components/error-dialog/error-dialog.component.ts
- frontend/src/app/decorators/track-function.decorator.ts
- frontend/src/app/services/global-error-handler.service.ts
- frontend/src/app/interceptors/http-error.interceptor.ts

**Backend:**
- azure-functions/main/SubmitErrorToAI.cs
- azure-functions/main/ProcessErrorForAI.cs
- azure-functions/main/Services/ErrorAnalysisService.cs
- azure-functions/main/Services/AutoFixService.cs
- azure-functions/main/Services/AutoTestService.cs',
        '**Testanweisungen:**

1. **Error Tracking testen:**
   - App verwenden und absichtlich einen Fehler provozieren
   - Error Dialog sollte automatisch erscheinen
   - Prüfen ob Funktionshistorie angezeigt wird

2. **AI Error Submission testen:**
   - Im Error Dialog auf "Fehler an AI übergeben" klicken
   - Prüfen ob Request erfolgreich an Backend gesendet wird

**Erwartete Ergebnisse:**
- Fehler werden automatisch erfasst
- AI-Analyse identifiziert betroffene Code-Stellen',
        '**Bekannte Probleme:**
- Auto-Fix Service simuliert derzeit File-Operationen (nicht produktiv)
- Auto-Test Service führt derzeit simulierte Tests aus

**Hinweise:**
- System ist für Demo/Prototyp implementiert',
        'ErrorTrackingService muss in Komponenten injiziert sein',
        'AI/Error Handling',
        'High',
        0, -- IsEnabled: disabled by default
        GETUTCDATE()
    );
    PRINT 'Feature #2: AI Auto-Fix System erstellt';
END
ELSE
BEGIN
    PRINT 'Feature #2: AI Auto-Fix System bereits vorhanden';
END
GO

-- Feature 3: Feature-Management-System
IF NOT EXISTS (SELECT * FROM Features WHERE FeatureNumber = 3)
BEGIN
    INSERT INTO Features (
        FeatureNumber, Title, Description, DetailedDescription,
        TechnicalDetails, TestInstructions, KnownIssues, Dependencies,
        Category, Priority, IsEnabled, ImplementedDate
    )
    VALUES (
        3,
        'Feature-Management-System',
        'Vollständiges Feature-Flag-System mit Benutzerverwaltung, Rollen und detaillierten Feature-Beschreibungen',
        '**Was wurde implementiert:**
Ein umfassendes Feature-Management-System das es ermöglicht, Features schrittweise freizugeben und zu testen.

**Hauptfunktionen:**
1. **Feature-Verwaltung:**
   - Jedes Feature erhält eine Nummer und detaillierte Beschreibung
   - Features werden nach Implementierungsdatum sortiert (neueste zuerst)
   - Toggle zum Aktivieren/Deaktivieren für alle Benutzer
   - Standardmäßig sind alle Features deaktiviert

2. **Benutzerverwaltung:**
   - Zwei Rollen: "admin" (kann Features freigeben) und "user" (kann nur ansehen)
   - Standard-Benutzer: admin/admin123 und test/test123
   - Login-System mit Token-basierter Authentifizierung

3. **Persistente Speicherung:**
   - Alle Feature-States werden in SQL Server gespeichert
   - Wer hat wann welches Feature freigegeben wird festgehalten
   - EnabledDate und EnabledBy werden gespeichert',
        '**Backend:**
- azure-functions/main/Models/Feature.cs
- azure-functions/main/Services/FeatureService.cs
- azure-functions/main/Services/AuthService.cs
- azure-functions/main/Login.cs
- azure-functions/main/GetFeatures.cs
- azure-functions/main/ToggleFeature.cs

**Frontend:**
- frontend/src/app/services/auth.service.ts
- frontend/src/app/services/feature.service.ts
- frontend/src/app/components/features/features-dialog.component.ts',
        '**Testanweisungen:**

1. **Login testen:**
   - Mit admin/admin123 anmelden
   - Prüfen ob Features-Button im Header erscheint

2. **Feature freigeben (Admin):**
   - Als Admin einloggen
   - Feature-Toggle aktivieren
   - Prüfen ob Status auf "Aktiviert" wechselt
   - Prüfen ob EnabledDate und EnabledBy gespeichert werden

3. **Persistenz testen:**
   - Feature aktivieren
   - App neu starten
   - Prüfen ob Feature-Status erhalten bleibt',
        '**Bekannte Probleme:**
- Keine bekannten Probleme

**Hinweise:**
- Passwörter werden derzeit mit SHA256 gehasht (in Produktion BCrypt verwenden)',
        'ApplicationDbContext muss konfiguriert sein',
        'Feature Management',
        'High',
        0, -- IsEnabled: disabled by default
        GETUTCDATE()
    );
    PRINT 'Feature #3: Feature-Management-System erstellt';
END
ELSE
BEGIN
    PRINT 'Feature #3: Feature-Management-System bereits vorhanden';
END
GO

-- Feature 4: Robuste Fehlerbehandlung
IF NOT EXISTS (SELECT * FROM Features WHERE FeatureNumber = 4)
BEGIN
    INSERT INTO Features (
        FeatureNumber, Title, Description, DetailedDescription,
        TechnicalDetails, TestInstructions, KnownIssues,
        Category, Priority, IsEnabled, ImplementedDate
    )
    VALUES (
        4,
        'Robuste Fehlerbehandlung',
        'Umfassende Fehlerbehandlung mit HTTP Error Interceptor, Retry-Logic, Global Error Handler und Input Validation',
        '**Was wurde implementiert:**
Ein vollständiges System zur robusten Fehlerbehandlung auf Frontend- und Backend-Seite.

**Frontend-Features:**
1. **HTTP Error Interceptor:**
   - Fängt alle HTTP-Fehler ab
   - Retry-Logik mit Exponential Backoff (bis zu 3 Versuche)
   - Benutzerfreundliche Fehlermeldungen

2. **Global Error Handler:**
   - Fängt alle unhandled exceptions ab
   - Zeigt Error Dialog mit detaillierten Informationen

3. **Input Validation:**
   - Formular-Validierung
   - Client-seitige Validierung vor Submit

**Backend-Features:**
1. **Unified Error Response Format:**
   - Konsistente Error-Responses mit ErrorResponseHelper
   - CORS-Headers werden automatisch hinzugefügt

2. **Input Validation:**
   - Server-seitige Validierung aller Inputs
   - PropertyNameCaseInsensitive für JSON-Deserialisierung',
        '**Frontend:**
- frontend/src/app/interceptors/http-error.interceptor.ts
- frontend/src/app/services/global-error-handler.service.ts

**Backend:**
- azure-functions/main/Helpers/ErrorResponseHelper.cs',
        '**Testanweisungen:**

1. **HTTP Error Interceptor testen:**
   - API-Endpoint mit falscher URL aufrufen (404)
   - Prüfen ob Retry-Logik funktioniert

2. **Global Error Handler testen:**
   - JavaScript-Fehler provozieren
   - Prüfen ob Error Dialog erscheint

**Erwartete Ergebnisse:**
- Fehler werden benutzerfreundlich angezeigt
- Retry-Logik funktioniert bei transienten Fehlern',
        '**Bekannte Probleme:**
- Keine bekannten Probleme',
        'Error Handling',
        'High',
        0, -- IsEnabled: disabled by default
        GETUTCDATE()
    );
    PRINT 'Feature #4: Robuste Fehlerbehandlung erstellt';
END
ELSE
BEGIN
    PRINT 'Feature #4: Robuste Fehlerbehandlung bereits vorhanden';
END
GO

PRINT 'Feature-Initialisierung abgeschlossen';
GO


