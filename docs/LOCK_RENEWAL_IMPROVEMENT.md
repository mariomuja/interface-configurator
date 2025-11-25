# Service Bus Lock Renewal Verbesserung

## Problem

Die ursprüngliche Implementierung des `ServiceBusLockRenewalService` aktualisierte nur die Datenbank-Einträge, erneuerte aber nicht die echten Service Bus Locks. Dies führte dazu, dass Locks trotzdem ablaufen konnten.

## Lösung

### 1. Receiver-Instanzen Caching

**Neuer Service:** `IServiceBusReceiverCache` / `ServiceBusReceiverCache`

- Cached ServiceBusReceiver-Instanzen pro Topic/Subscription-Kombination
- Wiederverwendung von Receivern für effiziente Lock-Erneuerung
- Automatische Bereinigung von disposed Receivern
- Thread-safe Implementierung mit `ConcurrentDictionary`

**Vorteile:**
- Keine unnötige Erstellung von Receivern
- Bessere Performance durch Wiederverwendung
- Reduzierte Verbindungs-Overhead

### 2. Echte Lock-Erneuerung

**Aktualisierter Service:** `ServiceBusLockRenewalService`

- Verwendet `ServiceBusReceiver.RenewMessageLockAsync()` für echte Lock-Erneuerung
- Nutzt gecachte Receiver-Instanzen aus `IServiceBusReceiverCache`
- Aktualisiert Datenbank-Einträge mit neuen Expiration-Zeiten
- Behandelt Fehler (Lock verloren, Topic nicht gefunden, etc.)

**Ablauf:**
1. Findet Locks, die Erneuerung benötigen (aus Datenbank)
2. Gruppiert nach Topic/Subscription
3. Verwendet gecachten Receiver für Lock-Erneuerung
4. Aktualisiert Datenbank mit neuer Expiration-Zeit
5. Markiert abgelaufene Locks als "Expired"

## Implementierte Komponenten

### IServiceBusReceiverCache Interface

```csharp
public interface IServiceBusReceiverCache
{
    Task<ServiceBusReceiver> GetOrCreateReceiverAsync(
        string topicName,
        string subscriptionName,
        CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> RenewMessageLockAsync(
        string topicName,
        string subscriptionName,
        string lockToken,
        CancellationToken cancellationToken = default);

    void RemoveReceiver(string topicName, string subscriptionName);
    void Clear();
    int Count { get; }
}
```

### ServiceBusReceiverCache Implementation

**Features:**
- Thread-safe Caching mit `ConcurrentDictionary`
- Automatische Receiver-Erstellung bei Bedarf
- Fehlerbehandlung für disposed Receivers
- Cleanup-Methoden für Receiver-Verwaltung

**Cache-Key Format:** `{topicName}|{subscriptionName}`

### ServiceBusLockRenewalService Updates

**Vorher:**
- Aktualisierte nur Datenbank-Einträge
- Keine echte Lock-Erneuerung

**Jetzt:**
- Erneuert echte Service Bus Locks
- Verwendet gecachte Receiver-Instanzen
- Aktualisiert Datenbank mit korrekten Expiration-Zeiten
- Behandelt Fehler und markiert abgelaufene Locks

## Service-Registrierung

In `Program.cs`:

```csharp
// Register Service Bus Receiver Cache
services.AddSingleton<IServiceBusReceiverCache>(sp =>
{
    var serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString")
        ?? Environment.GetEnvironmentVariable("AZURE_SERVICEBUS_CONNECTION_STRING") ?? string.Empty;
    
    var serviceBusClient = new ServiceBusClient(serviceBusConnectionString);
    var logger = sp.GetService<ILogger<ServiceBusReceiverCache>>();
    return new ServiceBusReceiverCache(serviceBusClient, logger);
});

// Register Service Bus Lock Renewal Background Service
services.AddHostedService<ServiceBusLockRenewalService>();
```

## Vorteile

1. **Echte Lock-Erneuerung:** Locks werden jetzt tatsächlich im Service Bus erneuert
2. **Bessere Performance:** Receiver-Caching reduziert Overhead
3. **Zuverlässigkeit:** Fehlerbehandlung für verschiedene Szenarien
4. **Nachvollziehbarkeit:** Detailliertes Logging für Troubleshooting

## Fehlerbehandlung

Der Service behandelt folgende Fehler-Szenarien:

1. **Lock verloren/abgelaufen:**
   - Markiert Lock als "Expired" in Datenbank
   - Loggt Warnung

2. **Topic/Subscription nicht gefunden:**
   - Entfernt Receiver aus Cache
   - Loggt Warnung

3. **Receiver disposed:**
   - Erstellt neuen Receiver
   - Entfernt alten Receiver aus Cache

4. **Andere Fehler:**
   - Loggt Fehler
   - Setzt mit nächstem Lock fort (kein Abbruch)

## Monitoring

**Log-Einträge:**
- `Successfully renewed lock` - Lock erfolgreich erneuert
- `Failed to renew lock (lock lost or expired)` - Lock konnte nicht erneuert werden
- `Lock renewed in Service Bus but database update failed` - Service Bus OK, DB-Fehler
- `Error renewing lock` - Allgemeiner Fehler

**Metriken:**
- Anzahl gecachter Receiver (`IServiceBusReceiverCache.Count`)
- Anzahl erneuerter Locks pro Zyklus
- Anzahl fehlgeschlagener Erneuerungen

## Nächste Schritte

1. ✅ Receiver-Caching implementiert
2. ✅ Echte Lock-Erneuerung implementiert
3. ⚠️ Optional: Health Checks für Receiver-Cache
4. ⚠️ Optional: Automatische Cleanup von ungenutzten Receivern
5. ⚠️ Optional: Metriken für Lock-Erneuerungs-Erfolgsrate

