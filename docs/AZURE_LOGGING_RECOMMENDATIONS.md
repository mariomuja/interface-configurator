# Azure Logging-Empfehlungen

## Zusammenfassung

Dieses Dokument beschreibt Empfehlungen für eine flexible Protokollierungsarchitektur, die verschiedene Azure-Logging-Optionen unterstützt und konfigurierbar ist.

## Aktuelle Situation

### Bestehende Logging-Implementierungen

1. **ILogger (Microsoft.Extensions.Logging)**
   - Standard-Logging über Application Insights
   - Konfiguriert in `host.json` mit Sampling
   - Automatisch für alle Azure Functions verfügbar

2. **SqlServerLoggingService**
   - Speichert Logs in `ProcessLogs` Tabelle (InterfaceConfigDb)
   - Synchrones Schreiben bei jedem Log-Eintrag
   - Kann Performance-Probleme bei hohem Log-Volumen verursachen

3. **SqlServerLoggingServiceV2**
   - Verbesserte Version mit Batch-Processing
   - Queue-basiert mit periodischem Flush
   - Reduziert SQL-Last

4. **InMemoryLoggingService**
   - In-Memory-Logging für schnelle Tests
   - Logs gehen verloren beim Neustart

## Azure-Logging-Optionen

### 1. Application Insights (Empfohlen für Production)

**Vorteile:**
- ✅ Vollständig integriert mit Azure Functions
- ✅ Automatische Telemetrie (Requests, Dependencies, Exceptions)
- ✅ Leistungsüberwachung (Performance Counters, Metrics)
- ✅ Log Analytics Integration
- ✅ Kusto Query Language (KQL) für erweiterte Abfragen
- ✅ Alerting und Dashboards
- ✅ Distributed Tracing
- ✅ Live Metrics Stream

**Nachteile:**
- ⚠️ Kosten bei hohem Log-Volumen
- ⚠️ Sampling kann wichtige Logs filtern

**Empfehlung:**
- Primäre Logging-Lösung für Production
- Strukturierte Logs mit Properties verwenden
- Sampling für hohe Volumen konfigurieren
- Custom Metrics für Business-Metriken

### 2. Azure Monitor Logs / Log Analytics Workspace

**Vorteile:**
- ✅ Zentrale Sammlung aller Logs
- ✅ Langzeit-Speicherung (bis zu 2 Jahre)
- ✅ Kusto Query Language (KQL)
- ✅ Integration mit anderen Azure Services
- ✅ Cost-effective für große Datenmengen
- ✅ Log Retention Policies

**Nachteile:**
- ⚠️ Latenz bei der Log-Verfügbarkeit (1-2 Minuten)
- ⚠️ Kosten bei hohem Volumen

**Empfehlung:**
- Für Compliance und Audit-Logs
- Langzeit-Analyse und Reporting
- Integration mit Azure Sentinel für Security

### 3. Azure Blob Storage

**Vorteile:**
- ✅ Sehr kostengünstig für große Datenmengen
- ✅ Unbegrenzte Speicherung
- ✅ Archivierung von Logs
- ✅ Einfache Integration

**Nachteile:**
- ⚠️ Keine direkte Abfrage-Möglichkeit
- ⚠️ Manuelle Verarbeitung erforderlich
- ⚠️ Latenz bei der Verfügbarkeit

**Empfehlung:**
- Für Archivierung von Logs
- Compliance-Anforderungen (Langzeit-Speicherung)
- Batch-Processing von Logs

### 4. Azure Event Hubs

**Vorteile:**
- ✅ Streaming-Logs in Echtzeit
- ✅ Sehr hoher Durchsatz
- ✅ Integration mit Stream Analytics, Power BI
- ✅ Event-Driven Architecture

**Nachteile:**
- ⚠️ Zusätzliche Kosten
- ⚠️ Komplexere Architektur
- ⚠️ Benötigt Consumer für Verarbeitung

**Empfehlung:**
- Für Echtzeit-Log-Analyse
- Integration mit anderen Systemen
- High-Volume Streaming-Szenarien

### 5. Azure Storage Tables

**Vorteile:**
- ✅ Kostengünstig
- ✅ Schnelle Abfragen nach Partition/Row Key
- ✅ Einfache Integration

**Nachteile:**
- ⚠️ Begrenzte Abfrage-Möglichkeiten
- ⚠️ Nicht für komplexe Analysen geeignet

**Empfehlung:**
- Für strukturierte Logs mit bekannten Abfrage-Mustern
- Alternative zu SQL Server für Logs

### 6. Container App Logs

**Vorteile:**
- ✅ Integriert mit Azure Container Apps
- ✅ Automatische Log-Sammlung
- ✅ Integration mit Log Analytics

**Nachteile:**
- ⚠️ Nur für Container Apps verfügbar

**Empfehlung:**
- Für Container App-basierte Adapter-Instanzen
- Automatische Log-Sammlung ohne zusätzliche Konfiguration

## Empfohlene Architektur

### Multi-Channel Logging Strategy

```
┌─────────────────┐
│  Application    │
│     Code        │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  ILoggingService │  (Abstraction Layer)
│   Interface      │
└────────┬────────┘
         │
         ├─────────────────────────────────────┐
         │                                     │
         ▼                                     ▼
┌─────────────────┐                  ┌─────────────────┐
│  Logging        │                  │  Logging        │
│  Provider 1     │                  │  Provider 2     │
│  (Primary)      │                  │  (Secondary)    │
└─────────────────┘                  └─────────────────┘
```

### Implementierungsvorschlag

#### 1. Erweiterte ILoggingService Interface

```csharp
public interface ILoggingService
{
    Task LogAsync(string level, string message, string? details = null, 
                  CancellationToken cancellationToken = default);
    
    // Neue Methoden für strukturierte Logs
    Task LogAsync(LogLevel level, string message, 
                  IDictionary<string, object>? properties = null,
                  Exception? exception = null,
                  CancellationToken cancellationToken = default);
    
    // Batch-Logging für Performance
    Task LogBatchAsync(IEnumerable<LogEntry> entries, 
                      CancellationToken cancellationToken = default);
}
```

#### 2. Logging Provider Pattern

```csharp
public interface ILoggingProvider
{
    string Name { get; }
    bool IsEnabled { get; }
    Task LogAsync(LogEntry entry, CancellationToken cancellationToken = default);
    Task LogBatchAsync(IEnumerable<LogEntry> entries, 
                      CancellationToken cancellationToken = default);
}
```

#### 3. Konkrete Provider-Implementierungen

**ApplicationInsightsLoggingProvider**
- Verwendet `ILogger` mit Application Insights
- Strukturierte Logs mit Properties
- Custom Metrics und Events

**LogAnalyticsLoggingProvider**
- Direkte Integration mit Log Analytics Workspace
- Verwendet Data Collector API oder Azure Monitor Ingestion API
- Strukturierte Logs im JSON-Format

**BlobStorageLoggingProvider**
- Batch-Schreiben zu Blob Storage
- Partitionierung nach Datum/Stunde
- Komprimierung für Kosteneinsparung

**EventHubsLoggingProvider**
- Streaming-Logs zu Event Hubs
- Partitionierung nach Adapter-Instanz
- Hoher Durchsatz

**SqlServerLoggingProvider** (bestehend)
- Behalten für Backward Compatibility
- Optional: Nur für kritische Logs

**InMemoryLoggingProvider** (bestehend)
- Für Development/Testing

#### 4. Composite Logging Service

```csharp
public class CompositeLoggingService : ILoggingService
{
    private readonly IEnumerable<ILoggingProvider> _providers;
    private readonly ILogger<CompositeLoggingService> _logger;
    
    public CompositeLoggingService(
        IEnumerable<ILoggingProvider> providers,
        ILogger<CompositeLoggingService> logger)
    {
        _providers = providers.Where(p => p.IsEnabled);
        _logger = logger;
    }
    
    public async Task LogAsync(LogLevel level, string message, 
                               IDictionary<string, object>? properties = null,
                               Exception? exception = null,
                               CancellationToken cancellationToken = default)
    {
        var entry = new LogEntry
        {
            Level = level,
            Message = message,
            Properties = properties ?? new Dictionary<string, object>(),
            Exception = exception,
            Timestamp = DateTime.UtcNow
        };
        
        // Parallel zu allen Providern loggen
        var tasks = _providers.Select(p => 
            p.LogAsync(entry, cancellationToken).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _logger.LogWarning(t.Exception, 
                        "Failed to log to provider {Provider}", p.Name);
                }
            }));
        
        await Task.WhenAll(tasks);
    }
}
```

### Konfiguration

#### appsettings.json / Environment Variables

```json
{
  "Logging": {
    "Providers": {
      "ApplicationInsights": {
        "Enabled": true,
        "IsPrimary": true,
        "SamplingPercentage": 100
      },
      "LogAnalytics": {
        "Enabled": true,
        "IsPrimary": false,
        "WorkspaceId": "",
        "SharedKey": "",
        "LogType": "InterfaceConfiguratorLogs"
      },
      "BlobStorage": {
        "Enabled": false,
        "IsPrimary": false,
        "ContainerName": "logs",
        "RetentionDays": 90
      },
      "EventHubs": {
        "Enabled": false,
        "IsPrimary": false,
        "ConnectionString": "",
        "EventHubName": "logs"
      },
      "SqlServer": {
        "Enabled": true,
        "IsPrimary": false,
        "OnlyErrors": true  // Nur Errors in SQL speichern
      }
    },
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    },
    "StructuredLogging": true,
    "IncludeExceptionDetails": true,
    "IncludeStackTraces": false
  }
}
```

### Strukturierte Logs

#### Log Entry Schema

```csharp
public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Message { get; set; }
    public string? Component { get; set; }
    public string? InterfaceName { get; set; }
    public Guid? AdapterInstanceGuid { get; set; }
    public IDictionary<string, object> Properties { get; set; }
    public Exception? Exception { get; set; }
    public string? CorrelationId { get; set; }
    public string? OperationId { get; set; }
}
```

#### Beispiel: Strukturiertes Logging

```csharp
await _loggingService.LogAsync(
    LogLevel.Information,
    "Adapter instance started processing",
    new Dictionary<string, object>
    {
        ["AdapterName"] = "CSV",
        ["AdapterInstanceGuid"] = adapterInstanceGuid.ToString(),
        ["InterfaceName"] = interfaceName,
        ["BatchSize"] = batchSize,
        ["SourcePath"] = sourcePath
    });
```

### Performance-Optimierungen

#### 1. Batch-Processing

- Sammle Logs in einem Buffer
- Schreibe in Batches (z.B. alle 5 Sekunden oder bei 100 Einträgen)
- Reduziert I/O-Operationen

#### 2. Asynchrones Logging

- Fire-and-Forget für nicht-kritische Logs
- Warteschlange für hohe Volumen
- Background Worker für Batch-Verarbeitung

#### 3. Sampling

- Application Insights Sampling für hohe Volumen
- Alle Errors/Warnings, aber nur Sample von Information
- Konfigurierbar pro Log-Level

#### 4. Filterung

- Log-Level-basierte Filterung
- Component-basierte Filterung
- Property-basierte Filterung

### Sicherheit und Compliance

#### 1. PII (Personally Identifiable Information)

- Automatische Maskierung von sensiblen Daten
- Konfigurierbare PII-Felder
- Audit-Logs für Compliance

#### 2. Log Retention

- Konfigurierbare Retention-Policies
- Automatische Archivierung zu Blob Storage
- Löschung nach Retention-Periode

#### 3. Zugriffskontrolle

- RBAC für Log Analytics Workspace
- Storage Account Zugriffskontrolle
- Application Insights Zugriffskontrolle

## Migrationspfad

### Phase 1: Erweiterte Interface (Keine Breaking Changes)

1. Erweitere `ILoggingService` mit neuen Methoden
2. Implementiere Composite Pattern
3. Behalte bestehende Implementierungen
4. Konfiguriere neue Provider parallel

### Phase 2: Provider-Implementierungen

1. Implementiere ApplicationInsightsLoggingProvider
2. Implementiere LogAnalyticsLoggingProvider
3. Implementiere BlobStorageLoggingProvider
4. Optional: EventHubsLoggingProvider

### Phase 3: Migration

1. Aktiviere neue Provider schrittweise
2. Überwache Performance und Kosten
3. Deaktiviere alte Implementierungen nach erfolgreicher Migration

### Phase 4: Optimierung

1. Fine-Tuning der Konfiguration
2. Performance-Optimierungen
3. Cost-Optimierung

## Kostenüberlegungen

### Application Insights

- **Free Tier**: 5 GB/Monat
- **Pay-as-you-go**: $2.30/GB nach Free Tier
- **Empfehlung**: Sampling für hohe Volumen

### Log Analytics Workspace

- **Free Tier**: 5 GB/Monat
- **Pay-as-you-go**: $2.76/GB nach Free Tier
- **Retention**: $0.10/GB/Monat (über 31 Tage)

### Blob Storage

- **Hot Tier**: $0.0184/GB/Monat
- **Cool Tier**: $0.01/GB/Monat (für Archivierung)
- **Archive Tier**: $0.00099/GB/Monat (für Langzeit-Archivierung)

### Event Hubs

- **Basic**: $0.05/GB Durchsatz
- **Standard**: $0.05/GB Durchsatz + $0.05/Million Messages
- **Premium**: Ab $0.10/GB Durchsatz

## Best Practices

### 1. Log-Level Strategie

- **Trace**: Sehr detailliert, nur für Development
- **Debug**: Debugging-Informationen, nicht in Production
- **Information**: Allgemeine Informationen, Business-Events
- **Warning**: Warnungen, die Aufmerksamkeit benötigen
- **Error**: Fehler, die behoben werden müssen
- **Critical**: Kritische Fehler, sofortige Aufmerksamkeit

### 2. Strukturierte Logs

- Verwende strukturierte Logs statt String-Formatierung
- Nutze Properties für Filterung und Abfragen
- Konsistente Property-Namen

### 3. Correlation IDs

- Verwende Correlation IDs für Request-Tracing
- Propagiere Correlation IDs durch alle Services
- Nutze für Distributed Tracing

### 4. Exception Logging

- Logge immer vollständige Exception-Informationen
- Inkludiere Context-Informationen
- Nutze Exception-Properties für strukturierte Logs

### 5. Performance Monitoring

- Logge Performance-Metriken (Dauer, Durchsatz)
- Nutze Custom Metrics für Business-Metriken
- Setze Alerts für Performance-Probleme

## Empfohlene Konfiguration pro Umgebung

### Development

```json
{
  "Logging": {
    "Providers": {
      "ApplicationInsights": { "Enabled": false },
      "LogAnalytics": { "Enabled": false },
      "BlobStorage": { "Enabled": false },
      "SqlServer": { "Enabled": true, "OnlyErrors": false },
      "InMemory": { "Enabled": true }
    },
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

### Staging

```json
{
  "Logging": {
    "Providers": {
      "ApplicationInsights": { "Enabled": true, "SamplingPercentage": 50 },
      "LogAnalytics": { "Enabled": true },
      "BlobStorage": { "Enabled": false },
      "SqlServer": { "Enabled": true, "OnlyErrors": true }
    },
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### Production

```json
{
  "Logging": {
    "Providers": {
      "ApplicationInsights": { 
        "Enabled": true, 
        "IsPrimary": true,
        "SamplingPercentage": 20 
      },
      "LogAnalytics": { 
        "Enabled": true,
        "IsPrimary": false 
      },
      "BlobStorage": { 
        "Enabled": true,
        "RetentionDays": 365 
      },
      "SqlServer": { 
        "Enabled": true, 
        "OnlyErrors": true 
      }
    },
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

## Nächste Schritte

1. **Sofort**: Erweitere `ILoggingService` Interface
2. **Kurzfristig**: Implementiere Composite Pattern
3. **Mittelfristig**: Implementiere Provider für Application Insights, Log Analytics, Blob Storage
4. **Langfristig**: Optimierung und Cost-Management

## Referenzen

- [Azure Application Insights Documentation](https://docs.microsoft.com/azure/azure-monitor/app/app-insights-overview)
- [Azure Monitor Logs](https://docs.microsoft.com/azure/azure-monitor/logs/log-query-overview)
- [Azure Blob Storage](https://docs.microsoft.com/azure/storage/blobs/)
- [Azure Event Hubs](https://docs.microsoft.com/azure/event-hubs/)
- [Structured Logging Best Practices](https://docs.microsoft.com/aspnet/core/fundamentals/logging/)

