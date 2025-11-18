export interface DocumentationChapter {
  id: string;
  title: string;
  content: string;
}

export const DOCUMENTATION_CHAPTERS: DocumentationChapter[] = [
  {
    id: 'readme',
    title: 'README - Architecture Overview',
    content: `
      <h2>📊 Integration Configuration - Interface Configuration Demo</h2>
      <p>This application demonstrates a revolutionary approach to <strong>data integration</strong>: <strong>Configuration over Implementation</strong>. Instead of writing custom code for each new interface between systems, you simply <strong>configure</strong> what you want to connect—and it just works. No new implementation artifacts required.</p>
      
      <h3>The Vision: Configure, Don't Implement</h3>
      <p><strong>Traditional Approach (Implementation-Based):</strong></p>
      <ul>
        <li>Each new interface requires custom code</li>
        <li>Business logic mixed with integration logic</li>
        <li>High maintenance overhead</li>
        <li>Difficult to scale</li>
      </ul>
      
      <p><strong>This Approach (Configuration-Based):</strong></p>
      <ul>
        <li><strong>Tell the system what to connect</strong> (e.g., "CSV → SQL Server" or "SQL Server → SAP")</li>
        <li><strong>Use the same code</strong> for all interfaces</li>
        <li><strong>Zero implementation effort</strong> for new interfaces</li>
        <li><strong>Pluggable adapters</strong> handle the complexity</li>
      </ul>
      
      <h3>Key Architectural Concepts</h3>
      
      <h4>1. Universal Adapters</h4>
      <p>Each adapter can be used as <strong>both source and destination</strong>:</p>
      <ul>
        <li><strong>CsvAdapter</strong>: Can read CSV files (source) or write CSV files (destination)</li>
        <li><strong>SqlServerAdapter</strong>: Can read from SQL tables (source) or write to SQL tables (destination)</li>
        <li>Future adapters (JSON, SAP, REST APIs) follow the same pattern</li>
      </ul>
      
      <h4>2. MessageBox Pattern</h4>
      <p>The <strong>MessageBox</strong> acts as a staging area ensuring <strong>guaranteed delivery</strong>:</p>
      <ul>
        <li><strong>Debatching</strong>: Each record is stored as a separate message</li>
        <li><strong>Event-Driven</strong>: Triggers destination adapters when messages are added</li>
        <li><strong>Guaranteed Delivery</strong>: Messages remain until all destinations confirm processing</li>
        <li><strong>Multiple Destinations</strong>: One source can feed multiple destinations</li>
      </ul>
      
      <h4>3. Configuration-Based Integration</h4>
      <p>Interfaces are defined by <strong>configuration, not code</strong>:</p>
      <ul>
        <li>Zero implementation overhead for new interfaces</li>
        <li>Runtime configuration updates without redeployment</li>
        <li>Independent enable/disable control for each adapter</li>
        <li>User-editable instance names and settings</li>
      </ul>
      
      <h3>Architecture Flow</h3>
      <ol>
        <li><strong>Source Adapter</strong> reads data and debatches into individual messages</li>
        <li><strong>MessageBox</strong> stores each message and triggers events</li>
        <li><strong>Destination Adapters</strong> subscribe to messages and process them</li>
        <li><strong>Guaranteed Delivery</strong>: Messages removed only after all destinations confirm</li>
      </ol>
      
      <h3>Benefits</h3>
      <ul>
        <li>🚀 <strong>Zero Implementation</strong>: New interfaces = configuration only</li>
        <li>🔄 <strong>Reusability</strong>: Same adapters work for all interfaces</li>
        <li>✅ <strong>Guaranteed Delivery</strong>: Data never lost</li>
        <li>📈 <strong>Scalability</strong>: Add new adapters without touching existing code</li>
        <li>⚡ <strong>Speed</strong>: Deploy new interfaces in minutes, not weeks</li>
      </ul>
      
      <p><em>For detailed architecture documentation, see the other chapters in this documentation.</em></p>
    `
  },
  {
    id: 'overview',
    title: 'Übersicht',
    content: `
      <h2>Integration Configuration</h2>
      <p>Diese Anwendung demonstriert einen vollständigen Daten-Transport-Workflow von CSV-Dateien in eine SQL Server Datenbank unter Verwendung von Infrastructure as Code (IaC) Prinzipien.</p>
      <h3>Funktionsweise:</h3>
      <ol>
        <li>Die Anwendung generiert 50 Beispiel-Datensätze im CSV-Format</li>
        <li>Beim Klick auf "Transport starten" wird die CSV-Datei in Azure Blob Storage hochgeladen</li>
        <li>Eine Azure Function wird automatisch durch den Blob Trigger aktiviert</li>
        <li>Die Function verarbeitet die CSV-Datei in Chunks (100 Datensätze pro Chunk)</li>
        <li>Die Daten werden sequenziell in die SQL Server Datenbank eingefügt</li>
        <li>Alle Ereignisse werden in einer separaten Log-Tabelle protokolliert</li>
      </ol>
    `
  },
  {
    id: 'terraform-iac',
    title: 'Terraform und Infrastructure as Code',
    content: `
      <h2>Infrastructure as Code mit Terraform - Revolutionierung der Azure-Arbeit</h2>
      <p><strong>Infrastructure as Code (IaC)</strong> ist ein Ansatz zur Verwaltung und Bereitstellung von IT-Infrastruktur durch maschinenlesbare Konfigurationsdateien anstelle von manuellen Prozessen. Terraform von HashiCorp ist das führende Tool für IaC und hat die Art und Weise, wie Entwickler und DevOps-Teams mit Cloud-Infrastruktur arbeiten, fundamental verändert.</p>
      
      <h3>Der Paradigmenwechsel: Von manuellen Browser-Operationen zu Code-basierter Infrastruktur</h3>
      <p><strong>Vor Terraform:</strong> Azure-Administratoren mussten jeden Schritt manuell im Azure Portal durchführen - Resource Groups erstellen, Storage Accounts konfigurieren, SQL Server einrichten, Firewall Rules setzen. Jede Änderung erforderte Klicks, Formulare ausfüllen, Warten auf Provisioning. Fehler waren schwer rückgängig zu machen, Wiederholungen mühsam, und Dokumentation war oft veraltet.</p>
      
      <p><strong>Mit Terraform:</strong> Alle Infrastruktur-Operationen werden als Code definiert. Ein einziger Befehl <code>terraform apply</code> erstellt, ändert oder löscht die gesamte Infrastruktur. Keine Browser-Operationen mehr nötig - alles läuft über die Kommandozeile oder CI/CD-Pipelines.</p>
      
      <h3>Kernvorteile von Terraform (nach HashiCorp Dokumentation):</h3>
      
      <h4>1. Automatisierung und Effizienz</h4>
      <ul>
        <li><strong>Keine manuellen Browser-Operationen:</strong> Alle Azure-Ressourcen werden über Terraform-Code erstellt und verwaltet. Das Azure Portal wird nur noch zur Visualisierung genutzt, nicht mehr für Konfiguration.</li>
        <li><strong>Reproduzierbarkeit:</strong> Identische Umgebungen (Dev, Test, Prod) können jederzeit neu erstellt werden - keine "Works on my machine" Probleme mehr.</li>
        <li><strong>Geschwindigkeit:</strong> Komplexe Infrastruktur mit Dutzenden von Ressourcen wird in Minuten statt Stunden bereitgestellt.</li>
      </ul>
      
      <h4>2. Versionierung und Kontrolle</h4>
      <ul>
        <li><strong>Git-basierte Versionierung:</strong> Infrastruktur-Änderungen werden wie Code in Git getrackt. Jede Änderung ist nachvollziehbar, reviewbar und revertierbar.</li>
        <li><strong>State Management:</strong> Terraform verwaltet den aktuellen Zustand der Infrastruktur in einem State-File. Änderungen werden automatisch erkannt und nur das Nötige wird aktualisiert.</li>
        <li><strong>Change Tracking:</strong> <code>terraform plan</code> zeigt vor der Ausführung genau, was geändert wird - keine Überraschungen mehr.</li>
      </ul>
      
      <h4>3. Kollaboration und Best Practices</h4>
      <ul>
        <li><strong>Code Reviews:</strong> Infrastruktur-Änderungen durchlaufen Pull Requests wie jeder andere Code. Teams können gemeinsam arbeiten und Wissen teilen.</li>
        <li><strong>Lebendige Dokumentation:</strong> Die Terraform-Konfiguration ist die einzige Quelle der Wahrheit - sie dokumentiert die Infrastruktur automatisch und bleibt immer aktuell.</li>
        <li><strong>Standardisierung:</strong> Best Practices werden im Code festgeschrieben und automatisch angewendet - keine individuellen Abweichungen mehr.</li>
      </ul>
      
      <h4>4. Sicherheit und Compliance</h4>
      <ul>
        <li><strong>Policy as Code:</strong> Sicherheitsrichtlinien können in Terraform integriert werden (z.B. mit Sentinel). Unsichere Konfigurationen werden automatisch verhindert.</li>
        <li><strong>Audit Trail:</strong> Jede Infrastruktur-Änderung ist in Git dokumentiert - wer hat was wann geändert und warum.</li>
        <li><strong>Rollback-Fähigkeit:</strong> Fehlerhafte Änderungen können durch Git Revert sofort rückgängig gemacht werden.</li>
      </ul>
      
      <h4>5. Multi-Cloud und Portabilität</h4>
      <ul>
        <li><strong>Einheitliche Syntax:</strong> Terraform unterstützt Azure, AWS, GCP und viele andere Provider mit derselben Syntax. Teams müssen nicht für jeden Cloud-Provider neue Tools lernen.</li>
        <li><strong>Cloud-Agnostik:</strong> Infrastruktur-Code kann bei Bedarf zwischen Cloud-Providern migriert werden.</li>
        <li><strong>Hybrid-Cloud:</strong> Ressourcen in verschiedenen Clouds können mit einem Tool verwaltet werden.</li>
      </ul>
      
      <h4>6. Testing und Qualitätssicherung</h4>
      <ul>
        <li><strong>Plan vor Apply:</strong> <code>terraform plan</code> zeigt alle geplanten Änderungen vor der Ausführung - wie ein "Dry Run".</li>
        <li><strong>Validation:</strong> <code>terraform validate</code> prüft die Syntax und Konfiguration vor der Ausführung.</li>
        <li><strong>Automated Testing:</strong> Infrastruktur kann in Test-Umgebungen validiert werden, bevor sie produktiv geht.</li>
      </ul>
      
      <h4>7. Kostenkontrolle und Optimierung</h4>
      <ul>
        <li><strong>Ressourcen-Übersicht:</strong> Terraform zeigt alle verwalteten Ressourcen auf einen Blick.</li>
        <li><strong>Tagging:</strong> Konsistente Tags können automatisch auf alle Ressourcen angewendet werden für bessere Kostenanalyse.</li>
        <li><strong>Lifecycle Management:</strong> Ressourcen können automatisch erstellt, geändert und gelöscht werden - keine "Zombie-Ressourcen" mehr.</li>
      </ul>
      
      <h3>Praktischer Einfluss auf die Azure-Arbeit in diesem Projekt:</h3>
      <p><strong>Ohne Terraform:</strong> Um diese Anwendung zu deployen, müssten Sie im Azure Portal:</p>
      <ol>
        <li>Resource Group manuell erstellen</li>
        <li>SQL Server konfigurieren (Name, Region, Credentials, Firewall Rules)</li>
        <li>SQL Database erstellen (SKU, Größe, Backup-Einstellungen)</li>
        <li>Storage Account für Blob Storage erstellen (Name, Region, Replikation, Zugriff)</li>
        <li>Storage Container erstellen und Zugriffsrechte setzen</li>
        <li>Storage Account für Functions erstellen</li>
        <li>App Service Plan erstellen (SKU, Region, OS Type)</li>
        <li>Function App erstellen und konfigurieren (Runtime, App Settings, Connection Strings)</li>
        <li>Function Code manuell deployen</li>
        <li>Alle Connection Strings und Secrets manuell kopieren und konfigurieren</li>
      </ol>
      <p><strong>Mit Terraform:</strong> Ein einziger Befehl <code>terraform apply</code> erledigt alles automatisch. Die gesamte Infrastruktur ist in Code definiert, versioniert und reproduzierbar.</p>
      
      <h3>Terraform-Ressourcen in diesem Projekt:</h3>
      <ul>
        <li><strong>Resource Groups:</strong> Zentrale Verwaltung aller Ressourcen</li>
        <li><strong>SQL Server und Datenbank:</strong> Vollständig konfiguriert mit Firewall Rules</li>
        <li><strong>Storage Accounts:</strong> Blob Storage für CSV-Dateien und Functions Storage</li>
        <li><strong>Storage Container:</strong> Mit korrekten Zugriffsrechten</li>
        <li><strong>Azure Functions App Service Plan:</strong> Consumption Plan für Serverless Computing</li>
        <li><strong>Azure Function App:</strong> Mit allen App Settings und Connection Strings</li>
        <li><strong>Function Code Deployment:</strong> Über <code>null_resource</code> mit <code>local-exec</code> Provisioner</li>
      </ul>
      
      <p><strong>Wichtig:</strong> Das Deployment der Azure Functions erfolgt ebenfalls über Terraform mit einem <code>null_resource</code> und <code>local-exec</code> Provisioner. Dies stellt sicher, dass auch der Function Code Teil der Infrastructure as Code ist und nicht manuell deployiert werden muss. Alle Änderungen am Function Code werden durch Terraform State getrackt und können bei Bedarf neu deployed werden.</p>
      
      <h3>Zusammenfassung: Der Terraform-Effekt</h3>
      <p>Terraform transformiert die Azure-Administration von einer manuellen, fehleranfälligen Tätigkeit zu einer automatisierten, versionierten, kollaborativen Disziplin. Entwickler können Infrastruktur wie Code behandeln - mit allen Vorteilen: Versionierung, Reviews, Testing, Automatisierung. Das Ergebnis: Schnellere Deployments, weniger Fehler, bessere Zusammenarbeit und vollständige Nachvollziehbarkeit aller Infrastruktur-Änderungen.</p>
    `
  },
  {
    id: 'architecture',
    title: 'Architektur',
    content: `
      <h2>Systemarchitektur</h2>
      <p>Die Anwendung folgt einer modernen, cloud-nativen Architektur mit klarer Trennung der Verantwortlichkeiten.</p>
      
      <h3>Frontend (Vercel + Angular)</h3>
      <ul>
        <li><strong>Framework:</strong> Angular 17 mit Material Design</li>
        <li><strong>Hosting:</strong> Vercel (Serverless Hosting)</li>
        <li><strong>Funktionen:</strong> UI für Datenanzeige, Transport-Start, Log-Visualisierung</li>
      </ul>
      
      <h3>Backend API (Vercel Serverless Functions)</h3>
      <p><strong>Ja, diese Lösung verwendet Vercel Serverless Functions</strong> als Backend-API zwischen dem Frontend und den Azure-Services. Die Functions werden automatisch von Vercel bereitgestellt und skaliert.</p>
      <ul>
        <li><strong>Hosting:</strong> Vercel Edge Functions (Serverless, automatisches Scaling)</li>
        <li><strong>Funktionen:</strong>
          <ul>
            <li><code>/api/sample-csv</code>: Generiert 50 Beispiel-Datensätze im CSV-Format für die Anzeige im Frontend</li>
            <li><code>/api/start-transport</code>: Startet den Transport-Prozess - generiert CSV-Daten, lädt sie in Azure Blob Storage hoch und triggert damit die Azure Function</li>
            <li><code>/api/sql-data</code>: Fragt die transportierten Daten aus der Azure SQL Server Datenbank ab</li>
            <li><code>/api/process-logs</code>: Ruft alle Log-Einträge aus der ProcessLogs-Tabelle ab</li>
            <li><code>/api/clear-table</code>: Leert die TransportData-Tabelle für wiederholte Demonstrationen</li>
          </ul>
        </li>
        <li><strong>Kommunikation:</strong> Direkte Verbindung zu Azure SQL Server und Azure Blob Storage über Azure SDKs</li>
        <li><strong>Vorteile:</strong> Keine Server-Verwaltung, automatisches Scaling, globale Edge-Verteilung, Pay-per-Use</li>
      </ul>
      
      <h3>Azure Cloud Services</h3>
      <ul>
        <li><strong>Azure Blob Storage:</strong> Speicherung der CSV-Dateien</li>
        <li><strong>Azure Functions:</strong> Serverless Verarbeitung der CSV-Dateien</li>
        <li><strong>Azure SQL Server:</strong> Zieldatenbank für Transport-Daten und Logs</li>
        <li><strong>Blob Trigger:</strong> Automatische Aktivierung der Function bei neuen CSV-Dateien</li>
      </ul>
      
      <h3>Datenfluss:</h3>
      <ol>
        <li>Frontend → Vercel API: Start Transport Request</li>
        <li>Vercel API → Azure Blob Storage: CSV-Datei hochladen</li>
        <li>Azure Blob Storage → Azure Function: Blob Trigger aktiviert Function</li>
        <li>Azure Function → Azure SQL Server: Daten in Chunks einfügen</li>
        <li>Azure Function → Azure SQL Server: Log-Einträge schreiben</li>
        <li>Frontend ← Vercel API: Daten und Logs abfragen</li>
      </ol>
    `
  },
  {
    id: 'azure-components',
    title: 'Azure Komponenten',
    content: `
      <h2>Azure Cloud Komponenten</h2>
      
      <h3>1. Azure SQL Server</h3>
      <ul>
        <li><strong>Typ:</strong> Managed SQL Server (PaaS)</li>
        <li><strong>Version:</strong> SQL Server 12.0</li>
        <li><strong>Datenbank:</strong> csvtransportdb (Basic SKU)</li>
        <li><strong>Tabellen:</strong>
          <ul>
            <li><code>TransportData</code>: Enthält die transportierten CSV-Daten</li>
            <li><code>ProcessLogs</code>: Protokolliert alle Verarbeitungsereignisse</li>
          </ul>
        </li>
        <li><strong>Firewall:</strong> Öffentlicher Zugriff für Azure Services aktiviert</li>
      </ul>
      
      <h3>2. Azure Blob Storage</h3>
      <ul>
        <li><strong>Account:</strong> stcsvtransportud3e1cem</li>
        <li><strong>Container:</strong> csv-uploads (öffentlicher Blob-Zugriff)</li>
        <li><strong>Funktion:</strong> Speicherung der CSV-Dateien vor Verarbeitung</li>
        <li><strong>Trigger:</strong> Löst Azure Function bei neuen Dateien aus</li>
      </ul>
      
      <h3>3. Azure Functions</h3>
      <ul>
        <li><strong>Plan:</strong> Consumption Plan (Y1) - Pay-per-Use</li>
        <li><strong>Runtime:</strong> Node.js 20</li>
        <li><strong>Trigger:</strong> Blob Trigger auf csv-uploads Container</li>
        <li><strong>Verarbeitung:</strong> Chunk-basierte Verarbeitung (100 Datensätze pro Chunk)</li>
        <li><strong>Features:</strong>
          <ul>
            <li>Automatische Tabellenerstellung bei erstem Lauf</li>
            <li>Umfassendes Logging aller Ereignisse</li>
            <li>Fehlerbehandlung mit Rollback bei Chunk-Fehlern</li>
            <li>Transaktionale Datenbank-Inserts</li>
          </ul>
        </li>
      </ul>
      
      <h3>4. Azure Function App Storage</h3>
      <ul>
        <li><strong>Account:</strong> stfuncscsvud3e1cem</li>
        <li><strong>Funktion:</strong> Speicherung des Function App Codes und Runtime-Daten</li>
      </ul>
      
      <h3>5. Resource Group</h3>
      <ul>
        <li><strong>Name:</strong> rg-interface-configuration</li>
        <li><strong>Region:</strong> Central US</li>
        <li><strong>Verwaltung:</strong> Alle Ressourcen werden zentral in einer Resource Group verwaltet</li>
      </ul>
    `
  },
  {
    id: 'logging',
    title: 'Logging und Monitoring',
    content: `
      <h2>Umfassendes Event-Logging</h2>
      <p>Alle Ereignisse während des Transport-Prozesses werden in der <code>ProcessLogs</code> Tabelle protokolliert.</p>
      
      <h3>Protokollierte Ereignisse:</h3>
      <ul>
        <li><strong>CSV-Datei erkannt:</strong> Wenn eine neue CSV-Datei im Blob Storage erkannt wird</li>
        <li><strong>CSV-Parsing:</strong> Anzahl der geparsten Datensätze</li>
        <li><strong>Chunk-Erstellung:</strong> Anzahl der erstellten Chunks und Chunk-Größe</li>
        <li><strong>Chunk-Verarbeitung:</strong> Erfolgreiche Verarbeitung jedes Chunks mit Anzahl der eingefügten Datensätze</li>
        <li><strong>Chunk-Fehler:</strong> Detaillierte Fehlerinformationen bei fehlgeschlagenen Chunks</li>
        <li><strong>Transport-Abschluss:</strong> Gesamtzahl der verarbeiteten Datensätze</li>
        <li><strong>Transport-Fehler:</strong> Fehler auf Transport-Ebene</li>
        <li><strong>Datenbank-Initialisierung:</strong> Erstellung der Tabellen bei erstem Lauf</li>
      </ul>
      
      <h3>Log-Level:</h3>
      <ul>
        <li><strong>info:</strong> Normale Verarbeitungsereignisse</li>
        <li><strong>warning:</strong> Warnungen (z.B. leere CSV-Dateien)</li>
        <li><strong>error:</strong> Fehler während der Verarbeitung</li>
      </ul>
      
      <h3>Log-Struktur:</h3>
      <ul>
        <li><strong>timestamp:</strong> UTC-Zeitstempel des Ereignisses</li>
        <li><strong>level:</strong> Log-Level (info, warning, error)</li>
        <li><strong>message:</strong> Kurzbeschreibung des Ereignisses</li>
        <li><strong>details:</strong> Zusätzliche Details (Dateiname, Anzahl Datensätze, etc.)</li>
      </ul>
    `
  },
  {
    id: 'chunk-processing',
    title: 'Chunk-Verarbeitung',
    content: `
      <h2>Chunk-basierte Verarbeitung</h2>
      <p>Große CSV-Dateien werden in kleinere Chunks aufgeteilt, um die Verarbeitung zu optimieren und Ressourcen effizient zu nutzen.</p>
      
      <h3>Vorteile:</h3>
      <ul>
        <li><strong>Skalierbarkeit:</strong> Kann auch sehr große Dateien verarbeiten</li>
        <li><strong>Fehlerbehandlung:</strong> Fehler in einem Chunk beeinträchtigen nicht die gesamte Verarbeitung</li>
        <li><strong>Performance:</strong> Transaktionale Inserts pro Chunk für bessere Datenbank-Performance</li>
        <li><strong>Monitoring:</strong> Fortschritt kann Chunk-für-Chunk verfolgt werden</li>
        <li><strong>Ressourcen:</strong> Geringerer Speicherbedarf durch sequenzielle Verarbeitung</li>
      </ul>
      
      <h3>Konfiguration:</h3>
      <ul>
        <li><strong>Chunk-Größe:</strong> 100 Datensätze pro Chunk</li>
        <li><strong>Verarbeitung:</strong> Sequenziell (ein Chunk nach dem anderen)</li>
        <li><strong>Transaktionen:</strong> Jeder Chunk wird in einer eigenen Datenbank-Transaktion verarbeitet</li>
        <li><strong>Rollback:</strong> Bei Fehlern wird der gesamte Chunk zurückgerollt</li>
      </ul>
    `
  },
  {
    id: 'architecture-adapters',
    title: 'Adapter Pattern Architecture',
    content: `
      <h2>Adapter Pattern Architecture</h2>
      <p>The adapter pattern is the core architectural concept that enables <strong>configuration-based integration</strong>. Instead of writing custom code for each interface, adapters provide a unified interface for reading from and writing to different data sources.</p>
      
      <h3>Universal Adapters</h3>
      <p>Each adapter can be used as <strong>both source and destination</strong>:</p>
      
      <h4>CsvAdapter</h4>
      <p><strong>As Source:</strong></p>
      <ul>
        <li>Reads CSV files from Azure Blob Storage</li>
        <li>Supports folder monitoring via <code>ReceiveFolder</code> property</li>
        <li>Debatches data into individual records</li>
        <li>Writes each record to MessageBox as a separate message</li>
      </ul>
      
      <p><strong>As Destination:</strong></p>
      <ul>
        <li>Reads messages from MessageBox</li>
        <li>Transforms message data back to CSV format</li>
        <li>Writes CSV files to Azure Blob Storage</li>
      </ul>
      
      <h4>SqlServerAdapter</h4>
      <p><strong>As Source:</strong></p>
      <ul>
        <li>Reads data from SQL Server tables</li>
        <li>Debatches into individual records</li>
        <li>Writes each record to MessageBox</li>
      </ul>
      
      <p><strong>As Destination:</strong></p>
      <ul>
        <li>Reads messages from MessageBox</li>
        <li>Ensures destination table structure matches schema</li>
        <li>Writes records to SQL Server tables</li>
        <li>Dynamic schema management (creates/modifies tables automatically)</li>
      </ul>
      
      <h3>IAdapter Interface</h3>
      <p>All adapters implement the <code>IAdapter</code> interface with methods for:</p>
      <ul>
        <li><code>ReadAsync()</code>: Reads data from source</li>
        <li><code>WriteAsync()</code>: Writes data to destination</li>
        <li><code>GetSchemaAsync()</code>: Gets schema information</li>
        <li><code>EnsureDestinationStructureAsync()</code>: Ensures destination structure exists</li>
      </ul>
      
      <h3>Adding New Adapters</h3>
      <p>To add a new adapter (e.g., JSON, SAP, REST API):</p>
      <ol>
        <li>Create a new class implementing <code>IAdapter</code></li>
        <li>Implement all required methods</li>
        <li>Register the adapter in dependency injection</li>
        <li>No changes needed to existing code!</li>
      </ol>
    `
  },
  {
    id: 'architecture-messagebox',
    title: 'MessageBox Pattern Architecture',
    content: `
      <h2>MessageBox Pattern Architecture</h2>
      <p>The MessageBox is a central staging area (similar to Microsoft BizTalk Server) that ensures <strong>guaranteed delivery</strong> of data. All data flows through the MessageBox, enabling event-driven processing and reliable message routing.</p>
      
      <h3>Core Concepts</h3>
      
      <h4>Debatching</h4>
      <p>Source adapters <strong>debatch</strong> data into individual records:</p>
      <ul>
        <li>Each record becomes a separate message in MessageBox</li>
        <li>Messages are independent and can be processed separately</li>
        <li>Enables parallel processing and error isolation</li>
      </ul>
      
      <h4>Event-Driven Processing</h4>
      <p>When a message is added to MessageBox:</p>
      <ol>
        <li>An event is triggered in the Event Queue</li>
        <li>Destination adapters subscribe to messages</li>
        <li>Each adapter processes messages independently</li>
        <li>Subscriptions track processing status</li>
      </ol>
      
      <h4>Guaranteed Delivery</h4>
      <p>Messages remain in MessageBox until <strong>all</strong> subscribing destination adapters have successfully processed them:</p>
      <ul>
        <li>If one destination fails, others can still process</li>
        <li>Failed messages remain for retry</li>
        <li>No data loss until all destinations confirm</li>
      </ul>
      
      <h3>Database Schema</h3>
      <p>The MessageBox uses three main tables:</p>
      <ul>
        <li><strong>Messages</strong>: Stores individual messages (debatched records)</li>
        <li><strong>MessageSubscriptions</strong>: Tracks which adapters have processed which messages</li>
        <li><strong>AdapterInstances</strong>: Maintains metadata about adapter instances</li>
      </ul>
      
      <h3>Message Flow</h3>
      <ol>
        <li><strong>Source Adapter</strong> writes debatched records to MessageBox</li>
        <li><strong>Event Queue</strong> triggers destination adapters</li>
        <li><strong>Destination Adapters</strong> subscribe and process messages</li>
        <li><strong>System checks</strong> if all subscriptions are processed</li>
        <li><strong>Message removed</strong> only after all destinations confirm</li>
      </ol>
      
      <h3>Multiple Destinations</h3>
      <p>The MessageBox pattern supports <strong>one source → multiple destinations</strong>:</p>
      <ul>
        <li>One source creates messages in MessageBox</li>
        <li>Multiple destination adapters can subscribe</li>
        <li>Each adapter processes independently</li>
        <li>Message removed only when ALL destinations confirm</li>
      </ul>
    `
  },
  {
    id: 'architecture-interface-config',
    title: 'Interface Configuration System',
    content: `
      <h2>Interface Configuration System</h2>
      <p>The Interface Configuration system enables <strong>configuration-based integration</strong> by allowing users to define interfaces without writing code. Each interface configuration specifies:</p>
      <ul>
        <li>Source adapter and its configuration</li>
        <li>Destination adapter and its configuration</li>
        <li>Enable/disable flags for independent control</li>
        <li>Instance names for UI display</li>
        <li>Adapter instance GUIDs for tracking</li>
      </ul>
      
      <h3>Storage</h3>
      <p>Configurations are stored in Azure Blob Storage as JSON:</p>
      <ul>
        <li><strong>Container</strong>: <code>function-config</code></li>
        <li><strong>File</strong>: <code>interface-configurations.json</code></li>
        <li><strong>Format</strong>: JSON array of <code>InterfaceConfiguration</code> objects</li>
      </ul>
      
      <p>Configurations are also loaded into memory on Function App startup for fast access.</p>
      
      <h3>Configuration Lifecycle</h3>
      <ol>
        <li><strong>Creation</strong>: User defines source and destination adapters</li>
        <li><strong>Updates</strong>: Configuration changes are atomic (in-memory + JSON file)</li>
        <li><strong>Deletion</strong>: Interface removed and adapter processes stop</li>
      </ol>
      
      <h3>Adapter Instance Management</h3>
      <p>Each adapter instance has:</p>
      <ul>
        <li><strong>AdapterInstanceGuid</strong>: Unique identifier for tracking</li>
        <li><strong>InstanceName</strong>: User-editable name for UI display</li>
        <li><strong>IsEnabled</strong>: Enable/disable flag for process control</li>
        <li><strong>InterfaceName</strong>: Links adapter to interface configuration</li>
      </ul>
      
      <h3>Process Execution</h3>
      <p>Source and destination adapters run as separate Azure Functions (Timer Triggers):</p>
      <ul>
        <li><strong>Source Process</strong>: Loads enabled source configurations and processes them</li>
        <li><strong>Destination Process</strong>: Loads enabled destination configurations and processes messages from MessageBox</li>
      </ul>
      
      <h3>Benefits</h3>
      <ul>
        <li>✅ <strong>Zero Code Changes</strong>: New interfaces = configuration only</li>
        <li>✅ <strong>Runtime Updates</strong>: Change configurations without redeployment</li>
        <li>✅ <strong>Independent Control</strong>: Enable/disable adapters independently</li>
        <li>✅ <strong>User-Friendly</strong>: Editable instance names and settings</li>
        <li>✅ <strong>Scalability</strong>: Add unlimited interfaces with same codebase</li>
      </ul>
    `
  }
];

