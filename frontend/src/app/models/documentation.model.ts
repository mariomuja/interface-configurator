export interface DocumentationChapter {
  id: string;
  title: string;
  content: string;
}

export const DOCUMENTATION_CHAPTERS: DocumentationChapter[] = [
  {
    id: 'overview',
    title: 'Übersicht & Alle Features',
    content: `
      <h2>📊 Interface Configuration System</h2>
      <p>Diese Anwendung demonstriert einen <strong>revolutionären Ansatz zur Datenintegration</strong>: <strong>Konfiguration statt Implementierung</strong>. Statt für jede neue Schnittstelle zwischen Systemen Code zu schreiben, konfigurieren Sie einfach, was Sie verbinden möchten – und es funktioniert.</p>
      
      <h3>🎯 Kernkonzept: Konfigurieren statt Implementieren</h3>
      <p><strong>Traditioneller Ansatz:</strong> Jede neue Schnittstelle erfordert eigenen Code, hohen Wartungsaufwand, schwierige Skalierung.</p>
      <p><strong>Dieser Ansatz:</strong> Sagen Sie dem System, was verbunden werden soll (z.B. "CSV → SQL Server"), verwenden Sie denselben Code für alle Schnittstellen, null Implementierungsaufwand für neue Schnittstellen.</p>
      
      <h3>✨ Hauptfunktionen</h3>
      <ul>
        <li>✅ <strong>Konfigurationsbasierte Integration</strong> - Neue Schnittstellen ohne Code</li>
        <li>✅ <strong>Universal Adapter</strong> - Jeder Adapter kann Quelle und Ziel sein</li>
        <li>✅ <strong>MessageBox Pattern</strong> - Garantierte Zustellung mit Event-Driven Processing</li>
        <li>✅ <strong>Automatisches Error Tracking</strong> - Detaillierte Fehlerberichte mit AI-Integration</li>
        <li>✅ <strong>Robuste Fehlerbehandlung</strong> - Retry-Logik, Timeouts, Graceful Degradation</li>
        <li>✅ <strong>Umfassendes Logging</strong> - Alle Ereignisse werden protokolliert</li>
        <li>✅ <strong>Health Checks</strong> - Service-Status-Monitoring</li>
        <li>✅ <strong>Input Validation</strong> - Validierung auf Frontend und Backend</li>
        <li>✅ <strong>HTTP Error Interceptor</strong> - Einheitliche Fehlerbehandlung</li>
        <li>✅ <strong>Global Error Handler</strong> - Fängt alle unhandled Errors ab</li>
      </ul>
      
      <h3>🚀 Alle Features & Verbesserungen</h3>
      
      <h4>🤖 Automatisches AI-Fehlerbehebungs-System</h4>
      <ul>
        <li><strong>Error Tracking Service</strong>: Trackt automatisch alle Funktionsaufrufe (letzte 100), wird bei jedem Fehler überschrieben</li>
        <li><strong>Detaillierte Fehlerberichte</strong>: Mit Stack Traces, Funktionshistorie, Anwendungszustand und Umgebungsinformationen</li>
        <li><strong>AI-Integration</strong>: "Fehler an AI zur Korrektur übergeben" Button im Error-Dialog</li>
        <li><strong>Code-Analyse Service</strong>: Analysiert Fehlerberichte, identifiziert betroffene Code-Stellen, kategorisiert Fehler (NullReference, TypeError, NetworkError, ValidationError)</li>
        <li><strong>Automatische Fix-Generierung</strong>: AI erstellt Fixes basierend auf Fehlertyp, wendet sie automatisch an, erstellt Backups</li>
        <li><strong>Automatisches Testing</strong>: Führt automatisch Tests nach Fixes aus (Frontend, Backend, Integration)</li>
        <li><strong>Git-Integration</strong>: Committet Fixes automatisch zu Git</li>
        <li><strong>ProcessErrorForAI Endpoint</strong>: Vollständiger AI-Pipeline (Analyze → Fix → Test → Commit)</li>
        <li><strong>Global Error Handler</strong>: Fängt alle unhandled Errors automatisch ab und zeigt Error-Dialog</li>
        <li><strong>Error-Dialog</strong>: Zeigt Fehlerdetails, Historie, ermöglicht Download und AI-Übermittlung</li>
        <li><strong>Backend-Logging</strong>: Fehlerberichte werden für AI-Verarbeitung geloggt</li>
      </ul>
      
      <h4>🛡️ Robuste Fehlerbehandlung & Resilience</h4>
      <ul>
        <li><strong>HTTP Error Interceptor</strong>: Einheitliche Fehlerbehandlung für alle HTTP-Requests mit automatischen Retries</li>
        <li><strong>Automatische Retries</strong>: Exponential Backoff für transient Errors (bis zu 2 Retries mit 1-10 Sekunden Delay)</li>
        <li><strong>Request Timeouts</strong>: 30 Sekunden Timeout für alle Requests, verhindert hängende Anfragen</li>
        <li><strong>Graceful Degradation</strong>: Ein Fehler blockiert nicht die gesamte Anwendung, andere Funktionen bleiben verfügbar</li>
        <li><strong>Konsistente Error Responses</strong>: Einheitliches Format für alle Backend-Fehler (ErrorResponseHelper)</li>
        <li><strong>Error Response Helper</strong>: Standardisierte Fehlerantworten im Backend mit CORS-Headers</li>
        <li><strong>Benutzerfreundliche Fehlermeldungen</strong>: Klare, verständliche Fehlermeldungen für alle HTTP-Status-Codes (0, 400, 401, 403, 404, 500, 503)</li>
        <li><strong>Error-Dialog mit Historie</strong>: Zeigt vollständige Funktionsaufruf-Historie vor dem Fehler</li>
        <li><strong>Fehler-Deduplizierung</strong>: Verhindert mehrfache Anzeige desselben Fehlers</li>
      </ul>
      
      <h4>✅ Input Validation & Sicherheit</h4>
      <ul>
        <li><strong>Frontend Validation Service</strong>: Validierung vor dem Senden an Backend (Sanitization, Type-Checking)</li>
        <li><strong>Backend Validation Helper</strong>: Doppelte Validierung für Sicherheit (nie Frontend-Input vertrauen)</li>
        <li><strong>Input Sanitization</strong>: Automatische Bereinigung von Benutzereingaben (Interface-Namen, Field Separators)</li>
        <li><strong>Null Safety</strong>: Umfassende Null-Checks im gesamten Code (null-conditional Operators, null-coalescing)</li>
        <li><strong>Validierungsregeln</strong>:
          <ul>
            <li>Interface-Namen: 3-100 Zeichen, nur alphanumerisch, Bindestrich, Unterstrich</li>
            <li>Field Separators: Maximal 10 Zeichen</li>
            <li>Batch Sizes: 1-10000</li>
            <li>Polling Intervals: 1-3600 Sekunden</li>
            <li>File Masks: Maximal 100 Zeichen</li>
          </ul>
        </li>
        <li><strong>Case-Insensitive JSON</strong>: PropertyNameCaseInsensitive = true für camelCase/PascalCase-Kompatibilität</li>
        <li><strong>CORS Handling</strong>: Korrekte CORS-Header für alle Requests</li>
      </ul>
      
      <h4>⚡ Performance & Zuverlässigkeit</h4>
      <ul>
        <li><strong>Loading State Management</strong>: Zentrale Verwaltung von Loading-States, verhindert Konflikte</li>
        <li><strong>Optimistic Updates</strong>: UI-Updates vor Server-Bestätigung mit automatischem Rollback bei Fehlern</li>
        <li><strong>Database Connection Resilience</strong>: Retry-Logik für Datenbankverbindungen mit exponential backoff (3 Retries)</li>
        <li><strong>Health Checks</strong>: Automatische Service-Status-Prüfung (Database, Blob Storage, MessageBox)</li>
        <li><strong>Caching</strong>: Lokale Speicherung von Konfigurationen in localStorage</li>
        <li><strong>Function Call History</strong>: Letzte 100 Funktionsaufrufe werden gespeichert (wird bei Fehler überschrieben)</li>
        <li><strong>Strukturiertes Logging</strong>: Mit Correlation IDs für besseres Tracking</li>
        <li><strong>Error Report Persistence</strong>: Fehlerberichte werden in localStorage gespeichert</li>
      </ul>
    `
  },
  {
    id: 'ai-features',
    title: 'AI-Funktionen & Automatisierung',
    content: `
      <h2>🤖 AI-Funktionen & Automatische Fehlerbehebung</h2>
      
      <h3>Automatisches Error Tracking</h3>
      <ul>
        <li><strong>Funktionsaufruf-Historie</strong>: Letzte 100 Funktionsaufrufe werden automatisch getrackt</li>
        <li><strong>Dauer-Tracking</strong>: Performance-Messung für jeden Funktionsaufruf</li>
        <li><strong>Parameter-Logging</strong>: Alle Funktionsparameter werden gespeichert (sanitized)</li>
        <li><strong>Return-Value-Tracking</strong>: Rückgabewerte werden gespeichert</li>
        <li><strong>Automatische Überschreibung</strong>: Historie wird bei jedem Fehler mit vollständigem Kontext überschrieben</li>
      </ul>
      
      <h3>Detaillierte Fehlerberichte</h3>
      <p>Jeder Fehler erstellt einen umfassenden Bericht mit:</p>
      <ul>
        <li><strong>Error ID</strong>: Eindeutige Identifikation (ERR-Timestamp-Random)</li>
        <li><strong>Stack Trace</strong>: Vollständiger Stack Trace mit Dateipfaden und Zeilennummern</li>
        <li><strong>Funktionshistorie</strong>: Alle vorherigen Funktionsaufrufe vor dem Fehler</li>
        <li><strong>Anwendungszustand</strong>: Aktueller Interface-Name, Enabled-Status, Adapter-Namen</li>
        <li><strong>Umgebungsinformationen</strong>: API-URL, Browser, Plattform, User-Agent</li>
        <li><strong>Kontext</strong>: Zusätzliche Kontextinformationen (z.B. Dateiname bei File-Errors)</li>
      </ul>
      
      <h3>AI Code-Analyse</h3>
      <p>Der <strong>ErrorAnalysisService</strong> analysiert Fehler automatisch:</p>
      <ul>
        <li><strong>Stack-Trace-Parsing</strong>: Extrahiert Dateipfade und Zeilennummern automatisch</li>
        <li><strong>Fehlerkategorisierung</strong>:
          <ul>
            <li>NullReference: Null/undefined-Zugriffe</li>
            <li>TypeError: Typ-Fehler</li>
            <li>NetworkError: Netzwerk-/HTTP-Fehler</li>
            <li>ValidationError: Validierungsfehler</li>
            <li>GenericError: Andere Fehler</li>
          </ul>
        </li>
        <li><strong>Root-Cause-Analyse</strong>: Identifiziert wahrscheinliche Ursachen</li>
        <li><strong>Confidence-Score</strong>: Berechnet Vertrauenswert (0.0-1.0) basierend auf verfügbaren Informationen</li>
        <li><strong>Betroffene Dateien</strong>: Liste aller betroffenen Dateien mit Zeilennummern</li>
      </ul>
      
      <h3>Automatische Fix-Generierung</h3>
      <p>Der <strong>AutoFixService</strong> erstellt und wendet Fixes automatisch an:</p>
      <ul>
        <li><strong>Fix-Typen</strong>:
          <ul>
            <li><strong>AddNullCheck</strong>: Fügt Null-Checks vor Property-Zugriffen hinzu</li>
            <li><strong>AddTypeCheck</strong>: Fügt Typ-Überprüfungen hinzu</li>
            <li><strong>AddRetryLogic</strong>: Fügt Retry-Logik für Netzwerk-Requests hinzu</li>
            <li><strong>AddValidation</strong>: Fügt Input-Validierung hinzu</li>
          </ul>
        </li>
        <li><strong>Backup-Erstellung</strong>: Erstellt automatisch Backups vor Änderungen (.backup.Timestamp)</li>
        <li><strong>Code-Modifikation</strong>: Wendet Fixes direkt auf Code-Dateien an</li>
        <li><strong>Git-Integration</strong>: Committet Fixes automatisch zu Git mit beschreibenden Commit-Messages</li>
        <li><strong>Fehlerbehandlung</strong>: Trackt erfolgreiche und fehlgeschlagene Fixes</li>
      </ul>
      
      <h3>Automatisches Testing</h3>
      <p>Der <strong>AutoTestService</strong> testet Fixes automatisch:</p>
      <ul>
        <li><strong>Frontend-Tests</strong>: Führt <code>npm test</code> aus (Angular/Karma)</li>
        <li><strong>Backend-Tests</strong>: Führt <code>dotnet test</code> aus (.NET/xUnit)</li>
        <li><strong>Integration-Tests</strong>: Führt Integration-Tests aus (wenn verfügbar)</li>
        <li><strong>Test-Ergebnisse</strong>: Sammelt Output, Exit-Codes, Fehlermeldungen</li>
        <li><strong>Zusammenfassung</strong>: Erstellt Gesamt-Ergebnis (Success/Failed)</li>
      </ul>
      
      <h3>Vollständiger AI-Pipeline</h3>
      <p>Der <strong>ProcessErrorForAI</strong> Endpoint führt automatisch aus:</p>
      <ol>
        <li><strong>Code-Analyse</strong>: Analysiert Fehlerbericht, identifiziert betroffene Stellen</li>
        <li><strong>Fix-Generierung</strong>: Erstellt Fix-Vorschläge basierend auf Fehlertyp</li>
        <li><strong>Fix-Anwendung</strong>: Wendet Fixes an, erstellt Backups</li>
        <li><strong>Git-Commit</strong>: Committet Änderungen zu Git</li>
        <li><strong>Test-Ausführung</strong>: Führt Tests aus, verifiziert Fixes</li>
        <li><strong>Ergebnis-Rückgabe</strong>: Gibt detailliertes Ergebnis zurück</li>
      </ol>
      
      <h3>Verwendung</h3>
      <p><strong>Im Error-Dialog:</strong></p>
      <ol>
        <li>Fehler tritt auf → Error-Dialog wird automatisch angezeigt</li>
        <li>Klicken Sie "Fehler an AI zur Korrektur übergeben"</li>
        <li>Fehlerbericht wird an Backend gesendet</li>
        <li>AI-Pipeline wird automatisch ausgeführt</li>
        <li>Ergebnis wird angezeigt (Analyse, Fixes, Tests)</li>
      </ol>
      
      <p><strong>API-Endpoint:</strong></p>
      <pre><code>POST /api/ProcessErrorForAI
Body: ErrorReport (JSON)

Response: {
  success: true,
  analysis: {
    affectedFiles: number,
    suggestedFixes: number,
    confidenceScore: number,
    rootCause: string
  },
  fixes: {
    applied: number,
    failed: number,
    success: boolean
  },
  tests: {
    success: boolean,
    summary: string,
    testSuites: number
  }
}</code></pre>
    `
  },
  {
    id: 'architecture',
    title: 'Architektur & Technologie',
    content: `
      <h2>Systemarchitektur</h2>
      <p>Die Anwendung folgt einer modernen, cloud-nativen Architektur mit klarer Trennung der Verantwortlichkeiten.</p>
      
      <h3>Frontend (Angular + Material Design)</h3>
      <ul>
        <li><strong>Framework:</strong> Angular 17 mit Standalone Components</li>
        <li><strong>UI Library:</strong> Angular Material Design</li>
        <li><strong>Hosting:</strong> Vercel (Serverless Hosting)</li>
        <li><strong>Features:</strong> UI für Datenanzeige, Transport-Start, Log-Visualisierung, Error-Dialog</li>
        <li><strong>Error Handling:</strong> Global Error Handler, HTTP Error Interceptor, Error Tracking Service</li>
        <li><strong>Validation:</strong> Validation Service für Frontend-Validierung</li>
      </ul>
      
      <h3>Backend (Azure Functions)</h3>
      <ul>
        <li><strong>Runtime:</strong> .NET 8.0 (Isolated Worker Process)</li>
        <li><strong>Hosting:</strong> Azure Functions (Consumption Plan)</li>
        <li><strong>Triggers:</strong> HTTP, Timer, Blob Storage</li>
        <li><strong>Features:</strong> REST API, Timer-basierte Verarbeitung, Blob-basierte Trigger</li>
        <li><strong>Error Handling:</strong> Error Response Helper, Validation Helper, konsistente Fehlerantworten</li>
        <li><strong>AI Integration:</strong> SubmitErrorToAI Function für automatische Fehlerbehebung</li>
      </ul>
      
      <h3>Azure Cloud Services</h3>
      <ul>
        <li><strong>Azure Blob Storage:</strong> Speicherung von CSV-Dateien und Konfigurationen</li>
        <li><strong>Azure Functions:</strong> Serverless Verarbeitung</li>
        <li><strong>Azure SQL Server:</strong> Zieldatenbank für Transport-Daten, Logs und MessageBox</li>
        <li><strong>Infrastructure as Code:</strong> Terraform für vollständige Infrastruktur-Verwaltung</li>
      </ul>
      
      <h3>Architektur-Patterns</h3>
      
      <h4>1. Universal Adapters</h4>
      <p>Jeder Adapter kann sowohl als <strong>Quelle</strong> als auch als <strong>Ziel</strong> verwendet werden:</p>
      <ul>
        <li><strong>CsvAdapter</strong>: Liest CSV-Dateien (Quelle) oder schreibt CSV-Dateien (Ziel)</li>
        <li><strong>SqlServerAdapter</strong>: Liest aus SQL-Tabellen (Quelle) oder schreibt in SQL-Tabellen (Ziel)</li>
        <li>Zukünftige Adapter (JSON, SAP, REST APIs) folgen demselben Muster</li>
      </ul>
      
      <h4>2. MessageBox Pattern</h4>
      <p>Der <strong>MessageBox</strong> fungiert als Staging-Bereich für <strong>garantierte Zustellung</strong>:</p>
      <ul>
        <li><strong>Debatching</strong>: Jeder Datensatz wird als separate Nachricht gespeichert</li>
        <li><strong>Event-Driven</strong>: Triggert Ziel-Adapter, wenn Nachrichten hinzugefügt werden</li>
        <li><strong>Garantierte Zustellung</strong>: Nachrichten bleiben bestehen, bis alle Ziele die Verarbeitung bestätigen</li>
        <li><strong>Mehrere Ziele</strong>: Eine Quelle kann mehrere Ziele versorgen</li>
      </ul>
      
      <h4>3. Konfigurationsbasierte Integration</h4>
      <p>Schnittstellen werden durch <strong>Konfiguration, nicht Code</strong> definiert:</p>
      <ul>
        <li>Null Implementierungsaufwand für neue Schnittstellen</li>
        <li>Runtime-Konfigurations-Updates ohne Neudeployment</li>
        <li>Unabhängige Enable/Disable-Steuerung für jeden Adapter</li>
        <li>Benutzerbearbeitbare Instanznamen und Einstellungen</li>
      </ul>
      
      <h4>4. Error Handling & Resilience</h4>
      <ul>
        <li><strong>Global Error Handler</strong>: Fängt alle unhandled Errors ab</li>
        <li><strong>HTTP Error Interceptor</strong>: Einheitliche Behandlung aller HTTP-Fehler</li>
        <li><strong>Retry Logic</strong>: Automatische Wiederholung bei transient Errors</li>
        <li><strong>Error Tracking</strong>: Vollständige Historie aller Funktionsaufrufe</li>
        <li><strong>Graceful Degradation</strong>: Ein Fehler blockiert nicht die gesamte Anwendung</li>
      </ul>
      
      <h3>Datenfluss</h3>
      <ol>
        <li><strong>Quell-Adapter</strong> liest Daten und debatched sie in einzelne Nachrichten</li>
        <li><strong>MessageBox</strong> speichert jede Nachricht und triggert Events</li>
        <li><strong>Ziel-Adapter</strong> abonnieren Nachrichten und verarbeiten sie</li>
        <li><strong>Garantierte Zustellung</strong>: Nachrichten werden nur entfernt, nachdem alle Ziele bestätigt haben</li>
      </ol>
    `
  },
  {
    id: 'features',
    title: 'Alle Features & Fähigkeiten',
    content: `
      <h2>Funktionen & Fähigkeiten</h2>
      
      <h3>🔧 Adapter-Management</h3>
      <ul>
        <li><strong>CSV Adapter</strong>: Liest/schreibt CSV-Dateien aus/in Azure Blob Storage</li>
        <li><strong>SQL Server Adapter</strong>: Liest/schreibt Daten aus/in SQL Server Tabellen</li>
        <li><strong>Dynamische Schema-Erstellung</strong>: Tabellen werden automatisch erstellt/angepasst</li>
        <li><strong>Mehrere Ziel-Instanzen</strong>: Ein Interface kann mehrere Ziel-Adapter haben</li>
        <li><strong>Instanz-Management</strong>: Enable/Disable, Umbenennen, Konfigurieren</li>
      </ul>
      
      <h3>📊 Interface-Konfiguration</h3>
      <ul>
        <li><strong>Erstellung</strong>: Neue Interfaces durch Konfiguration erstellen</li>
        <li><strong>Bearbeitung</strong>: Alle Eigenschaften zur Laufzeit ändern</li>
        <li><strong>Löschung</strong>: Interfaces entfernen</li>
        <li><strong>Enable/Disable</strong>: Unabhängige Steuerung von Quelle und Ziel</li>
        <li><strong>Konfiguration-Speicherung</strong>: In Azure Blob Storage als JSON</li>
      </ul>
      
      <h3>🔄 Datenverarbeitung</h3>
      <ul>
        <li><strong>Chunk-basierte Verarbeitung</strong>: Große Dateien werden in Chunks verarbeitet</li>
        <li><strong>Transaktionale Inserts</strong>: Jeder Chunk wird in einer Transaktion verarbeitet</li>
        <li><strong>Fehlerbehandlung</strong>: Rollback bei Fehlern, Retry-Logik</li>
        <li><strong>Debatching</strong>: Jeder Datensatz wird als separate Nachricht behandelt</li>
        <li><strong>Guaranteed Delivery</strong>: Nachrichten bleiben bis zur Bestätigung</li>
      </ul>
      
      <h3>📝 Logging & Monitoring</h3>
      <ul>
        <li><strong>Process Logs</strong>: Alle Verarbeitungsereignisse werden protokolliert</li>
        <li><strong>Error Tracking</strong>: Automatisches Tracking aller Fehler</li>
        <li><strong>Funktionshistorie</strong>: Letzte 100 Funktionsaufrufe werden gespeichert</li>
        <li><strong>Detaillierte Fehlerberichte</strong>: Mit Stack Traces, Kontext und Anwendungszustand</li>
        <li><strong>Health Checks</strong>: Service-Status-Monitoring</li>
        <li><strong>Strukturiertes Logging</strong>: Mit Correlation IDs für besseres Tracking</li>
      </ul>
      
      <h3>🛡️ Fehlerbehandlung & Robustheit</h3>
      <ul>
        <li><strong>Global Error Handler</strong>: Fängt alle unhandled Errors ab und zeigt Error-Dialog</li>
        <li><strong>HTTP Error Interceptor</strong>: Einheitliche Fehlerbehandlung für alle HTTP-Requests</li>
        <li><strong>Automatische Retries</strong>: Exponential Backoff für transient Errors (bis zu 2 Retries)</li>
        <li><strong>Request Timeouts</strong>: 30 Sekunden Timeout für alle Requests</li>
        <li><strong>Graceful Degradation</strong>: Ein Fehler blockiert nicht die gesamte Anwendung</li>
        <li><strong>Error-Dialog</strong>: Benutzerfreundliche Fehleranzeige mit AI-Integration</li>
        <li><strong>Konsistente Error Responses</strong>: Einheitliches Format für alle Backend-Fehler</li>
        <li><strong>Error Response Helper</strong>: Standardisierte Fehlerantworten im Backend</li>
        <li><strong>Error Tracking Service</strong>: Vollständige Historie aller Funktionsaufrufe</li>
      </ul>
      
      <h3>✅ Validierung & Sicherheit</h3>
      <ul>
        <li><strong>Frontend Validation Service</strong>: Validierung vor dem Senden</li>
        <li><strong>Backend Validation Helper</strong>: Doppelte Validierung für Sicherheit</li>
        <li><strong>Input Sanitization</strong>: Automatische Bereinigung von Eingaben</li>
        <li><strong>Null Safety</strong>: Umfassende Null-Checks</li>
        <li><strong>CORS Handling</strong>: Korrekte CORS-Header für alle Requests</li>
        <li><strong>Validierungsregeln</strong>: Interface-Namen (3-100 Zeichen, alphanumerisch), Field Separators (max 10 Zeichen), Batch Sizes (1-10000), Polling Intervals (1-3600 Sekunden)</li>
      </ul>
      
      <h3>🤖 AI-Integration & Automatisierung</h3>
      <ul>
        <li><strong>Automatisches Error Tracking</strong>: Alle Fehler werden automatisch getrackt mit vollständiger Historie</li>
        <li><strong>Fehlerberichte</strong>: Detaillierte JSON-Berichte mit allen Informationen (ErrorReport-Model)</li>
        <li><strong>AI-Übermittlung</strong>: "Fehler an AI zur Korrektur übergeben" Button im Error-Dialog</li>
        <li><strong>Backend-Logging</strong>: Fehlerberichte werden für AI-Verarbeitung geloggt (Application Insights)</li>
        <li><strong>SubmitErrorToAI Function</strong>: Backend-Endpoint für Fehlerübermittlung</li>
        <li><strong>ProcessErrorForAI Function</strong>: Vollständiger AI-Pipeline (Analyze → Fix → Test → Commit)</li>
        <li><strong>ErrorAnalysisService</strong>: Analysiert Fehler, identifiziert Code-Stellen, kategorisiert Fehler</li>
        <li><strong>AutoFixService</strong>: Erstellt und wendet Fixes automatisch an, committet zu Git</li>
        <li><strong>AutoTestService</strong>: Führt automatisch Tests nach Fixes aus</li>
        <li><strong>Unit Tests</strong>: Vollständige Test-Suite für alle AI-Services</li>
      </ul>
      
      <h3>⚡ Performance</h3>
      <ul>
        <li><strong>Loading State Management</strong>: Zentrale Verwaltung von Loading-States</li>
        <li><strong>Optimistic Updates</strong>: UI-Updates vor Server-Bestätigung (vorbereitet)</li>
        <li><strong>Database Connection Resilience</strong>: Retry-Logik für Datenbankverbindungen (vorbereitet)</li>
        <li><strong>Caching</strong>: Lokale Speicherung von Konfigurationen</li>
        <li><strong>Efficient Error Handling</strong>: Fehler werden nicht mehrfach angezeigt</li>
      </ul>
    `
  },
  {
    id: 'usage',
    title: 'Anleitung & Best Practices',
    content: `
      <h2>Verwendung der Anwendung</h2>
      
      <h3>1. Interface erstellen</h3>
      <ol>
        <li>Klicken Sie auf "Create Interface"</li>
        <li>Geben Sie einen Interface-Namen ein (3-100 Zeichen, nur Buchstaben, Zahlen, Bindestrich, Unterstrich)</li>
        <li>Wählen Sie Quell-Adapter (z.B. CSV)</li>
        <li>Wählen Sie Ziel-Adapter (z.B. SQL Server)</li>
        <li>Konfigurieren Sie die Adapter-Eigenschaften</li>
        <li>Speichern Sie die Konfiguration</li>
      </ol>
      
      <h3>2. Adapter konfigurieren</h3>
      <ul>
        <li><strong>CSV Adapter (Quelle):</strong>
          <ul>
            <li>Receive Folder: Ordner im Blob Storage</li>
            <li>File Mask: Dateimuster (z.B. *.csv) - max 100 Zeichen</li>
            <li>Field Separator: Feldtrennzeichen (z.B. ║) - max 10 Zeichen</li>
            <li>Polling Interval: Abfrageintervall in Sekunden (1-3600)</li>
            <li>Batch Size: Anzahl Datensätze pro Batch (1-10000)</li>
            <li>CSV Data: Beispiel-Daten für Tests</li>
          </ul>
        </li>
        <li><strong>SQL Server Adapter (Ziel):</strong>
          <ul>
            <li>Server Name: SQL Server Name</li>
            <li>Database Name: Datenbankname</li>
            <li>Credentials: Benutzername/Passwort oder Integrated Security</li>
            <li>Table Name: Zieltabelle</li>
            <li>Batch Size: Anzahl Datensätze pro Batch (1-10000)</li>
            <li>Use Transaction: Transaktionale Verarbeitung</li>
          </ul>
        </li>
      </ul>
      
      <h3>3. Transport starten</h3>
      <ol>
        <li>Wählen Sie ein Interface aus</li>
        <li>Überprüfen Sie die Konfiguration</li>
        <li>Stellen Sie sicher, dass Quelle und Ziel aktiviert sind</li>
        <li>Klicken Sie auf "Transport starten"</li>
        <li>Überwachen Sie die Process Logs</li>
        <li>Prüfen Sie die SQL-Daten</li>
      </ol>
      
      <h3>4. Fehlerbehandlung</h3>
      <ul>
        <li><strong>Automatisch:</strong> Alle Fehler werden automatisch getrackt</li>
        <li><strong>Error-Dialog:</strong> Zeigt automatisch Fehlerdetails, Stack Trace und Historie</li>
        <li><strong>AI-Integration:</strong> Klicken Sie "Fehler an AI zur Korrektur übergeben"</li>
        <li><strong>Retry:</strong> Automatische Retries bei transient Errors (bis zu 2 Retries)</li>
        <li><strong>Logs:</strong> Alle Fehler werden in Process Logs protokolliert</li>
        <li><strong>Download:</strong> Fehlerbericht als JSON herunterladen</li>
        <li><strong>Copy:</strong> Fehlerdetails in Zwischenablage kopieren</li>
      </ul>
      
      <h3>5. Monitoring</h3>
      <ul>
        <li><strong>Process Logs:</strong> Zeigt alle Verarbeitungsereignisse</li>
        <li><strong>MessageBox:</strong> Zeigt Nachrichten im MessageBox</li>
        <li><strong>SQL Data:</strong> Zeigt transportierte Daten</li>
        <li><strong>Health Check:</strong> Prüft Service-Status</li>
        <li><strong>Error Tracking:</strong> Zeigt Historie der letzten 100 Funktionsaufrufe</li>
      </ul>
      
      <h3>6. Validierung</h3>
      <p>Alle Eingaben werden automatisch validiert:</p>
      <ul>
        <li><strong>Interface-Namen:</strong> 3-100 Zeichen, nur alphanumerisch, Bindestrich, Unterstrich</li>
        <li><strong>Field Separators:</strong> Maximal 10 Zeichen</li>
        <li><strong>Batch Sizes:</strong> 1-10000</li>
        <li><strong>Polling Intervals:</strong> 1-3600 Sekunden</li>
        <li><strong>File Masks:</strong> Maximal 100 Zeichen</li>
      </ul>
      <p>Bei ungültigen Eingaben werden klare Fehlermeldungen angezeigt.</p>
    `
  }
];
