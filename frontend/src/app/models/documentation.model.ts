export interface DocumentationChapter {
  id: string;
  title: string;
  content: string;
}

export const DOCUMENTATION_CHAPTERS: DocumentationChapter[] = [
  {
    id: 'overview',
    title: 'Übersicht',
    content: `
      <h2>CSV zu SQL Server Transport</h2>
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
        <li><strong>Name:</strong> rg-infrastructure-as-code</li>
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
  }
];

