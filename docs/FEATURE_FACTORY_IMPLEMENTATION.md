# Feature Factory Pattern Implementation

## Übersicht

Das Feature Factory Pattern wurde implementiert, um Feature Toggles zentral zu verwalten. Statt an mehreren Stellen im Code zu prüfen, ob ein Feature aktiviert ist, wird dies nur **ein einziges Mal** in der Factory gemacht.

## Architektur

### 1. Feature Registry (`IFeatureRegistry` / `FeatureRegistry`)
- **Zentrale Stelle** für Feature-Status-Prüfung
- Verwendet Caching (5 Minuten) um Datenbankabfragen zu minimieren
- Methoden:
  - `IsFeatureEnabledAsync(int featureNumber)` - Prüft Feature-Status
  - `IsFeatureEnabledByNameAsync(string featureName)` - Prüft Feature-Status nach Name
  - `GetEnabledFeatureNumbersAsync()` - Gibt alle aktivierten Features zurück
  - `RefreshCacheAsync()` - Aktualisiert den Cache

### 2. Feature Factory (`IFeatureFactory<T>` / `FeatureFactory<T>`)
- **Einzige Stelle** wo Feature Toggles geprüft werden
- Generische Factory für jeden Service-Typ
- Wenn Feature aktiviert: Gibt neue Implementierung zurück
- Wenn Feature deaktiviert: Gibt alte/legacy Implementierung zurück
- Methoden:
  - `CreateAsync()` - Asynchrone Erstellung mit aktueller Feature-Prüfung
  - `Create()` - Synchrone Erstellung mit gecachtem Feature-Status

### 3. Extension Methods (`FeatureFactoryExtensions`)
- Vereinfacht die Registrierung von Feature Factories in Dependency Injection
- `AddFeatureFactory<TInterface, TOld, TNew>(featureNumber)` - Registriert Factory für einen Service

## Verwendung

### Backend (Azure Functions)

#### 1. Feature Factory registrieren

```csharp
// In Program.cs
services.AddMemoryCache(); // Für FeatureRegistry Caching
services.AddScoped<IFeatureRegistry, FeatureRegistry>();

// Feature Factory für einen Service registrieren
services.AddFeatureFactory<IDataService, DataServiceAdapter, DataServiceAdapterV2>(featureNumber: 5);
```

#### 2. Service verwenden

```csharp
// Service wird automatisch über Factory geliefert
public class MyFunction
{
    private readonly IDataService _dataService; // Wird automatisch über Factory erstellt
    
    public MyFunction(IDataService dataService)
    {
        _dataService = dataService; // Factory prüft Feature-Status und gibt richtige Implementierung zurück
    }
}
```

### Frontend (Angular)

Die Frontend-Implementierung folgt demselben Pattern (noch zu implementieren).

## Implementierte Services

### 1. IDataService
- **Alte Implementierung**: `DataServiceAdapter`
- **Neue Implementierung**: `DataServiceAdapterV2`
  - Verbesserte Fehlerbehandlung mit Retry-Logic
  - Größere Batch-Größen für bessere Performance
  - Parallele Verarbeitung mit Concurrency-Limits
- **Feature Number**: 5

### 2. ILoggingService
- **Alte Implementierung**: `SqlServerLoggingService`
- **Neue Implementierung**: `SqlServerLoggingServiceV2`
  - Batch-Processing für Logs (50 Logs pro Batch)
  - Automatisches Flushing alle 5 Sekunden
  - Verbesserte Performance durch Bulk-Inserts
- **Feature Number**: 6

## Vorteile

1. **Zentrale Feature-Prüfung**: Feature Toggles werden nur an einer Stelle geprüft (in der Factory)
2. **Einfache Erweiterung**: Neue Features können einfach hinzugefügt werden
3. **Type-Safety**: Compile-time Type-Checking durch Generics
4. **Performance**: Caching reduziert Datenbankabfragen
5. **Testbarkeit**: Factories können einfach gemockt werden
6. **Wartbarkeit**: Klare Trennung zwischen alter und neuer Implementierung

## Nächste Schritte

1. ✅ Feature Registry erstellt
2. ✅ Feature Factory erstellt
3. ✅ Extension Methods erstellt
4. ✅ Beispiel-Implementierungen (DataService, LoggingService)
5. ⏳ Weitere Services auf Factory Pattern umstellen
6. ⏳ Frontend Services auf Factory Pattern umstellen
7. ⏳ Tests für Factory Pattern erstellen
8. ⏳ Dokumentation aktualisieren

## Feature Numbers

- **Feature #5**: Enhanced DataService (DataServiceAdapterV2)
- **Feature #6**: Enhanced LoggingService (SqlServerLoggingServiceV2)

## Beispiel: Neues Feature hinzufügen

```csharp
// 1. Neue Implementierung erstellen
public class MyServiceV2 : IMyService
{
    // Neue Implementierung
}

// 2. Factory registrieren
services.AddFeatureFactory<IMyService, MyService, MyServiceV2>(featureNumber: 7);

// 3. Feature in Datenbank erstellen (via InitializeFeatures oder manuell)
// Feature #7: Enhanced MyService
```

Die Factory prüft automatisch, ob Feature #7 aktiviert ist, und gibt die entsprechende Implementierung zurück.

