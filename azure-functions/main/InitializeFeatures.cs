using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Services;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

/// <summary>
/// HTTP endpoint to initialize default features (admin only, one-time setup)
/// </summary>
public class InitializeFeaturesFunction
{
    private readonly ILogger<InitializeFeaturesFunction> _logger;
    private readonly FeatureService _featureService;
    private readonly AuthService _authService;

    public InitializeFeaturesFunction(
        ILogger<InitializeFeaturesFunction> logger,
        FeatureService featureService,
        AuthService authService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _featureService = featureService ?? throw new ArgumentNullException(nameof(featureService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    [Function("InitializeFeatures")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "InitializeFeatures")] HttpRequestData req,
        FunctionContext context)
    {
        try
        {
            // Check admin role
            string? userRole = null;
            if (req.Headers.TryGetValues("Authorization", out var authHeaders))
            {
                var token = authHeaders.FirstOrDefault()?.Replace("Bearer ", "");
                if (!string.IsNullOrEmpty(token))
                {
                    try
                    {
                        var tokenData = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(token));
                        var parts = tokenData.Split(':');
                        if (parts.Length >= 2)
                        {
                            userRole = parts[1];
                        }
                    }
                    catch
                    {
                        // Invalid token
                    }
                }
            }

            if (userRole != "admin")
            {
                var forbiddenResponse = req.CreateResponse(HttpStatusCode.Forbidden);
                CorsHelper.AddCorsHeaders(forbiddenResponse);
                await forbiddenResponse.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Only administrators can initialize features"
                }));
                return forbiddenResponse;
            }

            var featuresCreated = new List<string>();

            // Feature 1: Automatische Versionierung
            try
            {
                await _featureService.CreateFeatureAsync(
                    title: "Automatische Versionierung",
                    description: "Automatische Versionsverwaltung mit GitHub Actions - Version wird bei jedem Push zu main erhöht",
                    detailedDescription: @"**Was wurde implementiert:**
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
- Automatische Versionsverwaltung ohne manuellen Aufwand",
                    technicalDetails: @"**Implementierung:**
- Root: version.json mit version, buildNumber, lastUpdated
- GitHub Action: Automatisches Bumping bei Push zu main
- Frontend: VersionService liest /assets/version.json
- Build: scripts/copy-version.js kopiert version.json vor jedem Build

**Dateien:**
- version.json (Root)
- .github/workflows/version-bump.yml
- frontend/src/app/services/version.service.ts
- scripts/copy-version.js
- frontend/src/app/app.component.ts (Version-Anzeige)",
                    testInstructions: @"**Testanweisungen:**

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
- Build-Nummer wird inkrementiert",
                    knownIssues: @"**Bekannte Probleme:**
- Keine bekannten Probleme

**Hinweise:**
- GitHub Action benötigt GITHUB_TOKEN Berechtigung
- Version-Bump wird nur bei Push zu main ausgeführt (nicht bei anderen Branches)",
                    category: "DevOps",
                    priority: "Medium"
                );
                featuresCreated.Add("Automatische Versionierung");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Feature 'Automatische Versionierung' konnte nicht erstellt werden (möglicherweise bereits vorhanden)");
            }

            // Feature 2: AI Auto-Fix System
            try
            {
                await _featureService.CreateFeatureAsync(
                    title: "AI Auto-Fix System",
                    description: "Automatisches Fehlerbehebungs-System mit AI-gestützter Analyse, Fix-Generierung und automatischem Testing",
                    detailedDescription: @"**Was wurde implementiert:**
Ein vollständiges System zur automatischen Fehlerbehebung durch AI-gestützte Analyse und Fix-Generierung.

**Hauptfunktionen:**
1. **Error Tracking Service (Frontend):**
   - Trackt alle Funktionsaufrufe automatisch
   - Speichert Funktionshistorie in localStorage
   - Erstellt detaillierte Error-Reports mit Stack-Traces

2. **Error Dialog Component:**
   - Zeigt detaillierte Fehlerinformationen
   - Button ""Fehler an AI zur Korrektur übergeben""
   - Download/Copy-Funktion für Error-Reports

3. **Backend AI Services:**
   - ErrorAnalysisService: Analysiert Fehler und identifiziert betroffene Code-Stellen
   - AutoFixService: Wendet vorgeschlagene Fixes automatisch an
   - AutoTestService: Führt Tests aus um Fixes zu verifizieren

4. **API Endpoints:**
   - SubmitErrorToAI: Empfängt Error-Reports vom Frontend
   - ProcessErrorForAI: Orchestriert Analyse -> Fix -> Test Pipeline

**Workflow:**
1. Fehler tritt auf → ErrorTrackingService erfasst ihn
2. User klickt ""Fehler an AI übergeben""
3. Backend analysiert Fehler (ErrorAnalysisService)
4. Backend generiert Fixes (AutoFixService)
5. Backend testet Fixes (AutoTestService)
6. Ergebnisse werden zurückgegeben",
                    technicalDetails: @"**Frontend:**
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
- azure-functions/main/Services/AutoTestService.cs
- azure-functions/main/Models/ErrorReportModels.cs
- azure-functions/main/Models/ErrorAnalysisModels.cs

**Datenstrukturen:**
- ErrorReport: Enthält ErrorId, FunctionCallHistory, CurrentError, ApplicationState
- ErrorAnalysisResult: Enthält AffectedFiles, RootCause, SuggestedFixes
- CodeChange: Beschreibt Code-Änderungen (FilePath, LineNumber, OldCode, NewCode)",
                    testInstructions: @"**Testanweisungen:**

1. **Error Tracking testen:**
   - App verwenden und absichtlich einen Fehler provozieren
   - Error Dialog sollte automatisch erscheinen
   - Prüfen ob Funktionshistorie angezeigt wird

2. **AI Error Submission testen:**
   - Im Error Dialog auf ""Fehler an AI übergeben"" klicken
   - Prüfen ob Request erfolgreich an Backend gesendet wird
   - Backend-Logs prüfen ob Error Report empfangen wurde

3. **Error Analysis testen:**
   - POST Request an /api/ProcessErrorForAI mit ErrorReport
   - Prüfen ob AnalysisResult zurückkommt mit:
     - AffectedFiles
     - RootCause
     - SuggestedFixes

4. **Auto-Fix testen:**
   - Prüfen ob AutoFixService Fixes korrekt anwendet
   - Prüfen ob AppliedFixes und FailedFixes korrekt zurückgegeben werden

5. **Auto-Test testen:**
   - Prüfen ob AutoTestService Tests ausführt
   - Prüfen ob TestResults korrekt zurückgegeben werden

**Erwartete Ergebnisse:**
- Fehler werden automatisch erfasst
- AI-Analyse identifiziert betroffene Code-Stellen
- Fixes werden vorgeschlagen und können angewendet werden
- Tests werden automatisch ausgeführt",
                    knownIssues: @"**Bekannte Probleme:**
- Auto-Fix Service simuliert derzeit File-Operationen (nicht produktiv)
- Auto-Test Service führt derzeit simulierte Tests aus
- Git-Commits werden derzeit nicht automatisch erstellt

**Hinweise:**
- System ist für Demo/Prototyp implementiert
- Produktive Nutzung erfordert zusätzliche Sicherheitsmaßnahmen
- File-Operationen sollten in produktiver Umgebung mit Vorsicht verwendet werden",
                    dependencies: @"**Abhängigkeiten:**
- ErrorTrackingService muss in Komponenten injiziert sein
- @trackFunction() Decorator muss auf Methoden angewendet werden
- GlobalErrorHandler muss in app.config.ts registriert sein
- HTTP Error Interceptor muss konfiguriert sein",
                    category: "AI/Error Handling",
                    priority: "High"
                );
                featuresCreated.Add("AI Auto-Fix System");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Feature 'AI Auto-Fix System' konnte nicht erstellt werden (möglicherweise bereits vorhanden)");
            }

            // Feature 3: Feature-Management-System
            try
            {
                await _featureService.CreateFeatureAsync(
                    title: "Feature-Management-System",
                    description: "Vollständiges Feature-Flag-System mit Benutzerverwaltung, Rollen und detaillierten Feature-Beschreibungen",
                    detailedDescription: @"**Was wurde implementiert:**
Ein umfassendes Feature-Management-System das es ermöglicht, Features schrittweise freizugeben und zu testen.

**Hauptfunktionen:**
1. **Feature-Verwaltung:**
   - Jedes Feature erhält eine Nummer und detaillierte Beschreibung
   - Features werden nach Implementierungsdatum sortiert (neueste zuerst)
   - Toggle zum Aktivieren/Deaktivieren für alle Benutzer
   - Standardmäßig sind alle Features deaktiviert

2. **Benutzerverwaltung:**
   - Zwei Rollen: ""admin"" (kann Features freigeben) und ""user"" (kann nur ansehen)
   - Standard-Benutzer: admin/admin123 und test/test123
   - Login-System mit Token-basierter Authentifizierung

3. **Detaillierte Feature-Beschreibungen:**
   - Detaillierte Beschreibung (bis 10.000 Zeichen)
   - Technische Details
   - Testanweisungen
   - Bekannte Probleme
   - Abhängigkeiten
   - Breaking Changes
   - Kategorie und Priorität

4. **Persistente Speicherung:**
   - Alle Feature-States werden in SQL Server gespeichert
   - Wer hat wann welches Feature freigegeben wird festgehalten
   - EnabledDate und EnabledBy werden gespeichert

**Workflow:**
1. Feature wird implementiert und in main Branch committed
2. Feature wird im System registriert (standardmäßig deaktiviert)
3. Tester kann Feature im UI ansehen mit allen Details
4. Admin kann Feature aktivieren/deaktivieren
5. Status wird persistent gespeichert",
                    technicalDetails: @"**Backend:**
- azure-functions/main/Models/Feature.cs
- azure-functions/main/Models/User.cs
- azure-functions/main/Services/FeatureService.cs
- azure-functions/main/Services/AuthService.cs
- azure-functions/main/Login.cs
- azure-functions/main/GetFeatures.cs
- azure-functions/main/ToggleFeature.cs
- azure-functions/main/CreateFeature.cs

**Frontend:**
- frontend/src/app/services/auth.service.ts
- frontend/src/app/services/feature.service.ts
- frontend/src/app/components/features/features-dialog.component.ts
- frontend/src/app/components/login/login-dialog.component.ts

**Datenbank:**
- Features Tabelle: Id, FeatureNumber, Title, Description, DetailedDescription, IsEnabled, ImplementedDate, EnabledDate, EnabledBy, etc.
- Users Tabelle: Id, Username, PasswordHash, Role, CreatedDate, LastLoginDate, IsActive",
                    testInstructions: @"**Testanweisungen:**

1. **Login testen:**
   - Mit admin/admin123 anmelden
   - Prüfen ob Features-Button im Header erscheint
   - Mit test/test123 anmelden
   - Prüfen ob Features-Button erscheint (aber kein Toggle)

2. **Feature-Liste ansehen:**
   - Features-Dialog öffnen
   - Prüfen ob Features nach Datum sortiert sind (neueste zuerst)
   - Prüfen ob alle Details angezeigt werden

3. **Feature freigeben (Admin):**
   - Als Admin einloggen
   - Feature-Toggle aktivieren
   - Prüfen ob Status auf ""Aktiviert"" wechselt
   - Prüfen ob EnabledDate und EnabledBy gespeichert werden
   - Seite neu laden und prüfen ob Status persistent ist

4. **Feature-Freigabe verweigern (User):**
   - Als test-User einloggen
   - Prüfen ob Toggle nicht angezeigt wird oder deaktiviert ist
   - Versuchen Feature zu aktivieren (sollte nicht möglich sein)

5. **Persistenz testen:**
   - Feature aktivieren
   - App neu starten
   - Prüfen ob Feature-Status erhalten bleibt
   - Prüfen ob EnabledDate und EnabledBy korrekt angezeigt werden

**Erwartete Ergebnisse:**
- Features werden korrekt angezeigt
- Admin kann Features aktivieren/deaktivieren
- User kann Features nur ansehen
- Status wird persistent gespeichert
- EnabledDate und EnabledBy werden korrekt erfasst",
                    knownIssues: @"**Bekannte Probleme:**
- Keine bekannten Probleme

**Hinweise:**
- Passwörter werden derzeit mit SHA256 gehasht (in Produktion BCrypt verwenden)
- Token-basierte Authentifizierung ist vereinfacht (in Produktion JWT verwenden)
- Standard-Benutzer werden beim ersten Start automatisch erstellt",
                    dependencies: @"**Abhängigkeiten:**
- ApplicationDbContext muss konfiguriert sein
- SQL Server Datenbank muss verfügbar sein
- Features und Users Tabellen werden automatisch erstellt",
                    category: "Feature Management",
                    priority: "High"
                );
                featuresCreated.Add("Feature-Management-System");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Feature 'Feature-Management-System' konnte nicht erstellt werden (möglicherweise bereits vorhanden)");
            }

            // Feature 4: Robuste Fehlerbehandlung
            try
            {
                await _featureService.CreateFeatureAsync(
                    title: "Robuste Fehlerbehandlung",
                    description: "Umfassende Fehlerbehandlung mit HTTP Error Interceptor, Retry-Logic, Global Error Handler und Input Validation",
                    detailedDescription: @"**Was wurde implementiert:**
Ein vollständiges System zur robusten Fehlerbehandlung auf Frontend- und Backend-Seite.

**Frontend-Features:**
1. **HTTP Error Interceptor:**
   - Fängt alle HTTP-Fehler ab
   - Retry-Logik mit Exponential Backoff (bis zu 3 Versuche)
   - Benutzerfreundliche Fehlermeldungen
   - Spezielle Behandlung für 404, 401, 500 Fehler

2. **Global Error Handler:**
   - Fängt alle unhandled exceptions ab
   - Zeigt Error Dialog mit detaillierten Informationen
   - Loggt Fehler für Debugging

3. **Input Validation:**
   - Formular-Validierung mit Bootstrap-Styles
   - Client-seitige Validierung vor Submit
   - Klare Fehlermeldungen für Benutzer

4. **Loading State Management:**
   - Zentrale Verwaltung von Loading-States
   - Loading-Indikatoren während API-Calls
   - Verhindert mehrfache Submits

**Backend-Features:**
1. **Unified Error Response Format:**
   - Konsistente Error-Responses mit ErrorResponseHelper
   - CORS-Headers werden automatisch hinzugefügt
   - Strukturierte Fehlermeldungen

2. **Input Validation:**
   - Server-seitige Validierung aller Inputs
   - PropertyNameCaseInsensitive für JSON-Deserialisierung
   - Null-Safety Checks

3. **Resilient Database Connections:**
   - Connection Pooling
   - Retry-on-Failure für transient errors
   - Timeout-Handling",
                    technicalDetails: @"**Frontend:**
- frontend/src/app/interceptors/http-error.interceptor.ts
- frontend/src/app/services/global-error-handler.service.ts
- frontend/src/app/helpers/error-response-helper.ts (falls vorhanden)

**Backend:**
- azure-functions/main/Helpers/ErrorResponseHelper.cs
- PropertyNameCaseInsensitive in allen JSON-Deserialisierungen
- Connection Pooling in Program.cs

**Konfiguration:**
- app.config.ts: GlobalErrorHandler und HTTP_INTERCEPTORS registriert",
                    testInstructions: @"**Testanweisungen:**

1. **HTTP Error Interceptor testen:**
   - API-Endpoint mit falscher URL aufrufen (404)
   - Prüfen ob Retry-Logik funktioniert
   - Prüfen ob benutzerfreundliche Fehlermeldung angezeigt wird

2. **Global Error Handler testen:**
   - JavaScript-Fehler provozieren (z.B. undefined property access)
   - Prüfen ob Error Dialog erscheint
   - Prüfen ob Stack-Trace angezeigt wird

3. **Input Validation testen:**
   - Formular mit ungültigen Daten absenden
   - Prüfen ob Validierungsfehler angezeigt werden
   - Prüfen ob Submit verhindert wird

4. **Backend Error Handling testen:**
   - Request mit ungültigen Daten senden
   - Prüfen ob konsistente Error-Response zurückkommt
   - Prüfen ob CORS-Headers gesetzt sind

**Erwartete Ergebnisse:**
- Fehler werden benutzerfreundlich angezeigt
- Retry-Logik funktioniert bei transienten Fehlern
- Input-Validierung verhindert ungültige Daten
- Konsistente Error-Responses vom Backend",
                    knownIssues: @"**Bekannte Probleme:**
- Keine bekannten Probleme

**Hinweise:**
- Retry-Logik sollte für idempotente Operationen verwendet werden
- Error-Logging sollte in Produktion mit Log-Aggregation verbunden werden",
                    category: "Error Handling",
                    priority: "High"
                );
                featuresCreated.Add("Robuste Fehlerbehandlung");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Feature 'Robuste Fehlerbehandlung' konnte nicht erstellt werden (möglicherweise bereits vorhanden)");
            }

            // Feature 5: Enhanced DataService
            try
            {
                await _featureService.CreateFeatureAsync(
                    title: "Enhanced DataService (V2)",
                    description: "Verbesserte DataServiceAdapter-Implementierung mit Retry-Logic, größeren Batch-Größen und besserer Fehlerbehandlung",
                    detailedDescription: @"**Was wurde implementiert:**
Eine verbesserte Version des DataServiceAdapter mit erweiterten Funktionen für bessere Performance und Zuverlässigkeit.

**Hauptfunktionen:**
- Retry-Logic für transient errors (bis zu 3 Versuche)
- Größere Batch-Größen für Bulk-Inserts (10.000 statt 5.000)
- Verbesserte Fehlerbehandlung und Logging
- Optimierte SqlBulkCopy-Konfiguration (BatchSize: 2000, Timeout: 10 Minuten)

**Technische Details:**
- Implementiert als DataServiceAdapterV2
- Verwendet Feature Factory Pattern (Feature #7)
- Aktiviert über Feature #5
- Automatische Fallback auf alte Implementierung wenn Feature deaktiviert",
                    technicalDetails: @"**Implementierung:**
- azure-functions/main/Services/DataServiceAdapterV2.cs
- Registriert via FeatureFactory in Program.cs
- Feature Number: 5

**Verbesserungen gegenüber V1:**
- Batch-Größe: 10.000 statt 5.000
- SqlBulkCopy BatchSize: 2000 statt 1000
- Timeout: 600 Sekunden statt 300
- Retry-Logic mit exponential backoff
- Verbesserte Fehlerbehandlung",
                    testInstructions: @"**Testanweisungen:**

1. **Feature aktivieren:**
   - Als Admin einloggen
   - Feature #5 ""Enhanced DataService (V2)"" aktivieren

2. **Funktionalität testen:**
   - CSV-Daten verarbeiten
   - Prüfen ob V2-Implementierung verwendet wird (Logs zeigen ""V2"")
   - Prüfen ob größere Batch-Größen verwendet werden

3. **Performance testen:**
   - Große CSV-Dateien verarbeiten
   - Prüfen ob Performance verbessert wurde

**Erwartete Ergebnisse:**
- V2-Implementierung wird verwendet wenn Feature aktiviert
- Bessere Performance bei großen Datenmengen
- Retry-Logic funktioniert bei transienten Fehlern",
                    knownIssues: @"**Bekannte Probleme:**
- Keine bekannten Probleme

**Hinweise:**
- Feature muss aktiviert werden um V2 zu verwenden
- Fallback auf V1 wenn Feature deaktiviert",
                    category: "Backend/Performance",
                    priority: "Medium"
                );
                featuresCreated.Add("Enhanced DataService (V2)");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Feature 'Enhanced DataService (V2)' konnte nicht erstellt werden (möglicherweise bereits vorhanden)");
            }

            // Feature 6: Enhanced LoggingService
            try
            {
                await _featureService.CreateFeatureAsync(
                    title: "Enhanced LoggingService (V2)",
                    description: "Verbesserte LoggingService-Implementierung mit Batch-Processing, automatischem Flushing und Bulk-Inserts",
                    detailedDescription: @"**Was wurde implementiert:**
Eine verbesserte Version des SqlServerLoggingService mit Batch-Processing für bessere Performance.

**Hauptfunktionen:**
- Batch-Processing: Logs werden in Batches von 50 gesammelt
- Automatisches Flushing alle 5 Sekunden
- Bulk-Inserts für bessere Datenbank-Performance
- Verbesserte Fehlerbehandlung mit Re-Queueing bei Fehlern

**Technische Details:**
- Implementiert als SqlServerLoggingServiceV2
- Verwendet ConcurrentQueue für Thread-sichere Log-Sammlung
- Background-Timer für automatisches Flushing
- Automatische Fallback auf alte Implementierung wenn Feature deaktiviert",
                    technicalDetails: @"**Implementierung:**
- azure-functions/main/Services/SqlServerLoggingServiceV2.cs
- Registriert via FeatureFactory in Program.cs
- Feature Number: 6

**Verbesserungen gegenüber V1:**
- Batch-Processing: 50 Logs pro Batch
- Automatisches Flushing: Alle 5 Sekunden
- Bulk-Inserts statt einzelne Inserts
- Thread-sichere Queue-Implementierung
- Re-Queueing bei Fehlern (bis zu 1000 Logs)",
                    testInstructions: @"**Testanweisungen:**

1. **Feature aktivieren:**
   - Als Admin einloggen
   - Feature #6 ""Enhanced LoggingService (V2)"" aktivieren

2. **Funktionalität testen:**
   - Logs generieren (verschiedene Levels)
   - Prüfen ob V2-Implementierung verwendet wird
   - Prüfen ob Batch-Processing funktioniert (Logs werden gesammelt)

3. **Performance testen:**
   - Viele Logs schnell hintereinander generieren
   - Prüfen ob Logs in Batches gespeichert werden
   - Prüfen ob automatisches Flushing funktioniert

**Erwartete Ergebnisse:**
- V2-Implementierung wird verwendet wenn Feature aktiviert
- Logs werden in Batches gespeichert
- Automatisches Flushing alle 5 Sekunden
- Bessere Performance bei vielen Logs",
                    knownIssues: @"**Bekannte Probleme:**
- Keine bekannten Probleme

**Hinweise:**
- Feature muss aktiviert werden um V2 zu verwenden
- Fallback auf V1 wenn Feature deaktiviert
- FlushAllAsync() sollte bei Shutdown aufgerufen werden",
                    category: "Backend/Performance",
                    priority: "Medium"
                );
                featuresCreated.Add("Enhanced LoggingService (V2)");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Feature 'Enhanced LoggingService (V2)' konnte nicht erstellt werden (möglicherweise bereits vorhanden)");
            }

            // Feature 7: Feature Factory Pattern
            try
            {
                await _featureService.CreateFeatureAsync(
                    title: "Feature Factory Pattern",
                    description: "Zentrales Feature Toggle Management mit Factory Pattern - Feature-Prüfung nur an einer Stelle im Code",
                    detailedDescription: @"**Was wurde implementiert:**
Ein vollständiges Factory Pattern System für Feature Toggles, das es ermöglicht, Features zentral zu verwalten ohne Feature-Prüfungen im gesamten Code.

**Hauptfunktionen:**
1. **Feature Registry:**
   - Zentrale Stelle für Feature-Status-Prüfung
   - Caching (5 Minuten) für bessere Performance
   - Methoden: IsFeatureEnabledAsync(), GetEnabledFeatureNumbersAsync()

2. **Feature Factory:**
   - Generische Factory für jeden Service-Typ
   - Einzige Stelle wo Feature Toggles geprüft werden
   - Gibt neue Implementierung zurück wenn Feature aktiviert
   - Gibt alte Implementierung zurück wenn Feature deaktiviert

3. **Extension Methods:**
   - Vereinfachte Registrierung: AddFeatureFactory<TInterface, TOld, TNew>()
   - Automatische Dependency Injection Integration

**Vorteile:**
- Feature-Prüfung nur an einer Stelle (in der Factory)
- Einfache Erweiterung neuer Features
- Type-Safety durch Generics
- Performance durch Caching
- Klare Trennung zwischen alter und neuer Implementierung",
                    technicalDetails: @"**Implementierung:**
- azure-functions/main/Core/Factories/IFeatureRegistry.cs
- azure-functions/main/Core/Factories/FeatureRegistry.cs
- azure-functions/main/Core/Factories/IFeatureFactory.cs
- azure-functions/main/Core/Factories/FeatureFactory.cs
- azure-functions/main/Core/Factories/FeatureFactoryExtensions.cs

**Verwendung:**
```csharp
// Factory registrieren
services.AddFeatureFactory<IDataService, DataServiceAdapter, DataServiceAdapterV2>(featureNumber: 5);

// Service verwenden (automatisch über Factory)
public class MyFunction
{
    private readonly IDataService _dataService; // Factory prüft Feature-Status automatisch
}
```

**Feature Numbers:**
- Feature #5: Enhanced DataService
- Feature #6: Enhanced LoggingService",
                    testInstructions: @"**Testanweisungen:**

1. **Factory-Funktionalität testen:**
   - Feature #5 aktivieren → DataServiceAdapterV2 sollte verwendet werden
   - Feature #5 deaktivieren → DataServiceAdapter sollte verwendet werden
   - Gleiches für Feature #6 testen

2. **Cache-Funktionalität testen:**
   - Feature-Status ändern
   - Prüfen ob Cache nach 5 Minuten aktualisiert wird
   - RefreshCacheAsync() manuell aufrufen

3. **Performance testen:**
   - Viele Service-Instanzen erstellen
   - Prüfen ob Caching die Datenbankabfragen reduziert

**Erwartete Ergebnisse:**
- Feature-Prüfung erfolgt nur in der Factory
- Richtige Implementierung wird basierend auf Feature-Status zurückgegeben
- Caching funktioniert korrekt
- Performance ist verbessert durch Caching",
                    knownIssues: @"**Bekannte Probleme:**
- Keine bekannten Probleme

**Hinweise:**
- Cache wird alle 5 Minuten aktualisiert
- RefreshCacheAsync() kann manuell aufgerufen werden
- Alle neuen Features sollten über Factory Pattern verwaltet werden",
                    dependencies: @"**Abhängigkeiten:**
- IMemoryCache muss registriert sein
- IFeatureRegistry muss registriert sein
- ApplicationDbContext muss verfügbar sein",
                    category: "Architecture/Feature Management",
                    priority: "High"
                );
                featuresCreated.Add("Feature Factory Pattern");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Feature 'Feature Factory Pattern' konnte nicht erstellt werden (möglicherweise bereits vorhanden)");
            }

            // Feature 8: Destination Adapter UI
            try
            {
                await _featureService.CreateFeatureAsync(
                    title: "Destination Adapter UI",
                    description: "UI-Komponenten für die Verwaltung von Zieladaptern (Destination Adapters)",
                    detailedDescription: @"**Was wurde implementiert:**
Diese Funktion aktiviert die UI-Komponenten für die Verwaltung von Zieladaptern (Destination Adapters) in der Transport-Komponente.

**Hauptfunktionen:**
- Destination Adapter Cards werden in der UI angezeigt
- Möglichkeit, mehrere Destination Adapter hinzuzufügen
- Verwaltung von Destination Adapter Instanzen
- Einstellungen für Destination Adapter

**UI-Komponenten:**
- Destination Adapter Container Card
- Add CSV / Add SQL Server Buttons
- Destination Adapter Instances Cards
- Settings Dialog für Destination Adapter

**Standardmäßig deaktiviert:**
- Feature ist standardmäßig deaktiviert, um die UI-Komponenten zu verstecken
- Muss von Admin aktiviert werden, nachdem ausreichend getestet wurde",
                    technicalDetails: @"**Frontend:**
- frontend/src/app/components/transport/transport.component.html (Zeile 229-319)
- frontend/src/app/components/transport/transport.component.ts
- frontend/src/app/components/destination-instances-dialog/destination-instances-dialog.component.ts
- frontend/src/app/components/adapter-card/adapter-card.component.ts

**Backend:**
- Destination Adapter Funktionalität ist bereits implementiert
- Feature steuert nur die Sichtbarkeit der UI-Komponenten",
                    testInstructions: @"**Testanweisungen:**

1. **Feature aktivieren:**
   - Als Admin einloggen
   - Features-Dialog öffnen
   - Feature 'Destination Adapter UI' aktivieren

2. **UI-Komponenten prüfen:**
   - Zur Transport-Komponente navigieren
   - Prüfen ob Destination Adapter Container Card sichtbar ist
   - Prüfen ob 'Add CSV' und 'Add SQL Server' Buttons sichtbar sind

3. **Funktionalität testen:**
   - Destination Adapter hinzufügen
   - Einstellungen öffnen
   - Destination Adapter aktivieren/deaktivieren
   - Destination Adapter entfernen

**Erwartete Ergebnisse:**
- UI-Komponenten sind nur sichtbar wenn Feature aktiviert ist
- Alle Destination Adapter Funktionen funktionieren korrekt",
                    knownIssues: @"**Bekannte Probleme:**
- Keine bekannten Probleme

**Hinweise:**
- Feature sollte erst aktiviert werden, nachdem Destination Adapter Funktionalität ausreichend getestet wurde
- UI-Komponenten werden komplett ausgeblendet wenn Feature deaktiviert ist",
                    category: "UI/Feature Management",
                    priority: "Medium"
                );
                featuresCreated.Add("Destination Adapter UI");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Feature 'Destination Adapter UI' konnte nicht erstellt werden (möglicherweise bereits vorhanden)");
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);

            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                success = true,
                message = $"Features initialisiert: {featuresCreated.Count}",
                featuresCreated = featuresCreated
            }, new JsonSerializerOptions { WriteIndented = true }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing features");
            return await ErrorResponseHelper.CreateErrorResponse(
                req, HttpStatusCode.InternalServerError, "Failed to initialize features", ex, _logger);
        }
    }
}

