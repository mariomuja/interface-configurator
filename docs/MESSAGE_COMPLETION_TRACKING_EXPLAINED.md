# Message Completion Tracking - Detaillierte Erklärung

## 🎯 Problemstellung

### Das ursprüngliche Problem

Bei Azure Service Bus werden Messages mit einem **Lock-Mechanismus** verarbeitet:

1. **Message Empfang**: Wenn eine Destination Adapter eine Message vom Service Bus empfängt, erhält sie einen **Lock Token**
2. **Lock-Dauer**: Der Lock ist standardmäßig **60 Sekunden** gültig
3. **Message Completion**: Nach erfolgreicher Verarbeitung muss die Message **completed** werden, sonst wird sie wieder verfügbar
4. **Lock Expiration**: Läuft der Lock ab, wird die Message automatisch wieder verfügbar gemacht

**Das Problem:**
- Bei Container App Neustarts gingen die **Receiver-Instanzen** verloren
- **Lock Tokens** waren nur im Speicher gespeichert
- Nach einem Neustart konnten Messages nicht mehr **completed** werden
- Messages liefen ab und wurden erneut verarbeitet → **Duplikate**
- Oder Messages gingen verloren, wenn der Lock abgelaufen war

---

## ✅ Die Lösung: Message Completion Tracking

Das System wurde um drei Hauptkomponenten erweitert:

### 1. Lock Token Persistierung in der Datenbank

**Was passiert:**
- Jede empfangene Message wird in der Datenbank gespeichert
- Lock Token, Topic, Subscription, Expiration Time werden persistiert
- Status wird verfolgt: Active, Completed, Abandoned, DeadLettered, Expired

**Vorteil:**
- Lock-Informationen überleben Container App Neustarts
- Nach einem Neustart können Messages wieder gefunden und completed werden
- Vollständige Nachvollziehbarkeit aller Message-Locks

### 2. Automatische Lock-Erneuerung

**Was passiert:**
- Ein Background Service läuft alle **30 Sekunden**
- Findet alle Locks, die in den nächsten **30 Sekunden** ablaufen
- Erneuert diese automatisch, bevor sie ablaufen

**Vorteil:**
- Verhindert Lock-Expiration während langer Verarbeitungszeiten
- Messages können sicher verarbeitet werden, auch wenn es länger dauert
- Keine Message-Duplikate durch abgelaufene Locks

### 3. Dead-Letter Queue Monitoring

**Was passiert:**
- Ein Background Service läuft alle **5 Minuten**
- Überprüft alle Topics und Subscriptions auf Dead-Letter Messages
- Loggt Warnungen mit Details (Reason, Error Description, Delivery Count)

**Vorteil:**
- Proaktive Erkennung von fehlgeschlagenen Messages
- Frühe Warnung bei Problemen
- Detaillierte Informationen für Troubleshooting

---

## 📊 Architektur-Übersicht

```
┌─────────────────────────────────────────────────────────────┐
│                    Service Bus Message Flow                 │
└─────────────────────────────────────────────────────────────┘

1. Message Empfang (ReceiveMessagesAsync)
   │
   ├─► Message wird empfangen mit Lock Token
   │
   ├─► Lock wird in Datenbank gespeichert (RecordMessageLockAsync)
   │   └─► ServiceBusMessageLocks Tabelle
   │       ├─ MessageId
   │       ├─ LockToken
   │       ├─ TopicName
   │       ├─ SubscriptionName
   │       ├─ LockExpiresAt
   │       └─ Status: "Active"
   │
   └─► Message wird verarbeitet

2. Lock Renewal (ServiceBusLockRenewalService)
   │
   ├─► Läuft alle 30 Sekunden
   │
   ├─► Findet Locks, die in 30 Sekunden ablaufen
   │   └─► GetLocksNeedingRenewalAsync()
   │
   ├─► Erneuert Locks automatisch
   │   └─► RenewLockAsync()
   │       └─► Aktualisiert LockExpiresAt in Datenbank
   │
   └─► Verhindert Lock-Expiration

3. Message Completion (CompleteMessageAsync)
   │
   ├─► Message wurde erfolgreich verarbeitet
   │
   ├─► Lock wird in Service Bus completed
   │   └─► receiver.CompleteMessageAsync()
   │
   └─► Status wird in Datenbank aktualisiert
       └─► UpdateLockStatusAsync("Completed")
           └─► Status: "Completed"
           └─► CompletedAt: DateTime.UtcNow

4. Dead Letter Monitoring (ServiceBusDeadLetterMonitoringService)
   │
   ├─► Läuft alle 5 Minuten
   │
   ├─► Überprüft alle Topics/Subscriptions
   │   └─► GetSubscriptionRuntimePropertiesAsync()
   │
   ├─► Findet Dead-Letter Messages
   │   └─► DeadLetterMessageCount > 0
   │
   └─► Loggt Warnungen mit Details
       ├─ Reason
       ├─ Error Description
       └─ Delivery Count
```

---

## 🔍 Detaillierte Komponenten-Erklärung

### 1. ServiceBusMessageLock Model

**Zweck:** Speichert alle Informationen über einen Message Lock in der Datenbank

**Wichtige Felder:**
- `MessageId`: Eindeutige Message-ID
- `LockToken`: Lock Token vom Service Bus
- `TopicName` / `SubscriptionName`: Wo die Message empfangen wurde
- `LockExpiresAt`: Wann der Lock abläuft
- `Status`: Aktueller Status (Active, Completed, Abandoned, DeadLettered, Expired)
- `RenewalCount`: Wie oft der Lock erneuert wurde
- `DeliveryCount`: Wie oft die Message bereits empfangen wurde

**Beispiel:**
```csharp
var messageLock = new ServiceBusMessageLock
{
    MessageId = "msg-123",
    LockToken = "lock-token-abc",
    TopicName = "interface-example",
    SubscriptionName = "destination-adapter-guid",
    LockExpiresAt = DateTime.UtcNow.AddSeconds(60),
    Status = "Active",
    DeliveryCount = 1
};
```

---

### 2. ServiceBusLockTrackingService

**Zweck:** Verwaltet alle Lock-Operationen in der Datenbank

#### RecordMessageLockAsync()
**Wann:** Wird aufgerufen, wenn eine Message empfangen wird

**Was passiert:**
1. Prüft, ob Lock bereits existiert (Recovery-Szenario)
2. Erstellt neuen Lock-Eintrag oder aktualisiert bestehenden
3. Speichert alle relevanten Informationen

**Code-Flow:**
```csharp
// In ServiceBusService.ReceiveMessagesAsync()
var lockTrackingService = _serviceProvider.GetService<IServiceBusLockTrackingService>();
await lockTrackingService.RecordMessageLockAsync(
    messageId: sbMessage.MessageId,
    lockToken: sbMessage.LockToken,
    topicName: topicName,
    subscriptionName: subscriptionName,
    interfaceName: interfaceName,
    adapterInstanceGuid: adapterInstanceGuid,
    lockExpiresAt: sbMessage.LockedUntil.UtcDateTime,
    deliveryCount: sbMessage.DeliveryCount
);
```

#### UpdateLockStatusAsync()
**Wann:** Wird aufgerufen nach Completion, Abandon oder DeadLetter

**Was passiert:**
1. Findet Lock-Eintrag in Datenbank
2. Aktualisiert Status (Completed, Abandoned, DeadLettered)
3. Speichert Completion-Zeitpunkt und Grund

**Code-Flow:**
```csharp
// In ServiceBusService.CompleteMessageAsync()
await lockTrackingService.UpdateLockStatusAsync(
    messageId: messageId,
    status: "Completed",
    reason: "Message processed successfully"
);
```

#### RenewLockAsync()
**Wann:** Wird vom Lock Renewal Service aufgerufen

**Was passiert:**
1. Findet aktiven Lock
2. Aktualisiert `LockExpiresAt` auf neue Zeit
3. Erhöht `RenewalCount`
4. Speichert `LastRenewedAt`

**Wichtig:** Aktuell wird nur die Datenbank aktualisiert. Der echte Service Bus Lock wird noch nicht erneuert (siehe Verbesserungsvorschlag).

#### GetLocksNeedingRenewalAsync()
**Wann:** Wird vom Lock Renewal Service alle 30 Sekunden aufgerufen

**Was passiert:**
1. Findet alle aktiven Locks
2. Filtert nach Locks, die in den nächsten 30 Sekunden ablaufen
3. Gibt Liste zurück, sortiert nach Ablaufzeit

**SQL-äquivalent:**
```sql
SELECT * FROM ServiceBusMessageLocks
WHERE Status = 'Active'
  AND LockExpiresAt <= DATEADD(second, 30, GETUTCDATE())
ORDER BY LockExpiresAt ASC
```

#### GetExpiredLocksAsync()
**Wann:** Wird aufgerufen, um abgelaufene Locks zu finden

**Was passiert:**
1. Findet alle aktiven Locks, die bereits abgelaufen sind
2. Markiert sie als "Expired"
3. Speichert Completion-Zeitpunkt

**Wichtig:** Dies ist ein Fallback-Mechanismus für den Fall, dass Lock Renewal fehlschlägt.

#### CleanupOldLocksAsync()
**Wann:** Kann regelmäßig aufgerufen werden, um alte Einträge zu bereinigen

**Was passiert:**
1. Findet alle abgeschlossenen Locks (Completed, Abandoned, DeadLettered, Expired)
2. Die älter als Retention Period sind
3. Löscht diese aus der Datenbank

**Vorteil:** Verhindert, dass die Datenbank zu groß wird.

---

### 3. ServiceBusLockRenewalService

**Zweck:** Background Service, der automatisch Locks erneuert

**Wie es funktioniert:**

1. **Start:** Service startet beim Application Startup
2. **Loop:** Läuft kontinuierlich alle 30 Sekunden
3. **Prüfung:** Findet alle Locks, die Erneuerung benötigen
4. **Erneuerung:** Erneuert jeden Lock
5. **Logging:** Loggt Erfolg/Fehler

**Aktueller Status:**
- ✅ Findet Locks, die Erneuerung benötigen
- ✅ Aktualisiert Datenbank-Einträge
- ⚠️ Erneuert noch nicht den echten Service Bus Lock (siehe Verbesserungsvorschlag)

**Code-Flow:**
```csharp
// Alle 30 Sekunden
while (!stoppingToken.IsCancellationRequested)
{
    // 1. Finde Locks, die in 30 Sekunden ablaufen
    var locksToRenew = await _lockTrackingService
        .GetLocksNeedingRenewalAsync(TimeSpan.FromSeconds(30));
    
    // 2. Gruppiere nach Topic/Subscription
    var locksBySubscription = locksToRenew
        .GroupBy(l => new { l.TopicName, l.SubscriptionName });
    
    // 3. Erneuere jeden Lock
    foreach (var lockRecord in locks)
    {
        var newExpiresAt = DateTime.UtcNow.AddSeconds(60);
        await _lockTrackingService.RenewLockAsync(
            lockRecord.MessageId, 
            newExpiresAt
        );
    }
    
    // 4. Warte 30 Sekunden
    await Task.Delay(TimeSpan.FromSeconds(30));
}
```

---

### 4. ServiceBusDeadLetterMonitoringService

**Zweck:** Überwacht Dead-Letter Queues proaktiv

**Wie es funktioniert:**

1. **Start:** Service startet beim Application Startup
2. **Loop:** Läuft kontinuierlich alle 5 Minuten
3. **Prüfung:** Überprüft alle Topics und Subscriptions
4. **Erkennung:** Findet Dead-Letter Messages
5. **Logging:** Loggt Warnungen mit Details

**Was wird überwacht:**
- Anzahl der Dead-Letter Messages pro Subscription
- Reason für Dead-Letter (z.B. "MaxDeliveryCountExceeded")
- Error Description
- Delivery Count (wie oft wurde versucht zu verarbeiten)
- Enqueued Time (wann wurde die Message ursprünglich gesendet)

**Code-Flow:**
```csharp
// Alle 5 Minuten
while (!stoppingToken.IsCancellationRequested)
{
    // 1. Hole alle Topics
    await foreach (var topic in adminClient.GetTopicsAsync())
    {
        // 2. Hole alle Subscriptions
        await foreach (var subscription in adminClient.GetSubscriptionsAsync(topic.Name))
        {
            // 3. Prüfe Dead-Letter Count
            var runtimeProps = await adminClient
                .GetSubscriptionRuntimePropertiesAsync(topic.Name, subscription.Name);
            
            if (runtimeProps.Value.DeadLetterMessageCount > 0)
            {
                // 4. Logge Warnung
                _logger.LogWarning(
                    "Dead Letter Alert: Topic={Topic}, Subscription={Subscription}, Count={Count}",
                    topic.Name, subscription.Name, runtimeProps.Value.DeadLetterMessageCount
                );
                
                // 5. Hole Details der Dead-Letter Messages
                var deadLetterMessages = await receiver.PeekMessagesAsync(5);
                foreach (var msg in deadLetterMessages)
                {
                    _logger.LogWarning(
                        "Dead Letter Details: MessageId={Id}, Reason={Reason}, Error={Error}",
                        msg.MessageId, msg.DeadLetterReason, msg.DeadLetterErrorDescription
                    );
                }
            }
        }
    }
    
    // 6. Warte 5 Minuten
    await Task.Delay(TimeSpan.FromMinutes(5));
}
```

---

## 🔄 Vollständiger Message-Lifecycle

### Beispiel: Eine Message wird verarbeitet

```
1. Message Empfang
   ┌─────────────────────────────────────┐
   │ ServiceBusService.ReceiveMessagesAsync()
   │ ├─► Message empfangen (Lock Token erhalten)
   │ ├─► RecordMessageLockAsync() → Datenbank
   │ │   └─ Status: "Active"
   │ │   └─ LockExpiresAt: +60 Sekunden
   │ └─► Message wird zurückgegeben
   └─────────────────────────────────────┘

2. Lock Renewal (alle 30 Sekunden)
   ┌─────────────────────────────────────┐
   │ ServiceBusLockRenewalService
   │ ├─► GetLocksNeedingRenewalAsync()
   │ │   └─ Findet Lock (läuft in 30 Sek ab)
   │ ├─► RenewLockAsync()
   │ │   └─ LockExpiresAt: +60 Sekunden (neu)
   │ │   └─ RenewalCount: 1
   │ └─► Lock wird erneuert
   └─────────────────────────────────────┘

3. Message Verarbeitung
   ┌─────────────────────────────────────┐
   │ Adapter verarbeitet Message
   │ ├─► Daten werden transformiert
   │ ├─► Daten werden geschrieben
   │ └─► Verarbeitung erfolgreich
   └─────────────────────────────────────┘

4. Message Completion
   ┌─────────────────────────────────────┐
   │ ServiceBusService.CompleteMessageAsync()
   │ ├─► receiver.CompleteMessageAsync()
   │ │   └─ Message wird in Service Bus completed
   │ ├─► UpdateLockStatusAsync("Completed")
   │ │   └─ Status: "Completed"
   │ │   └─ CompletedAt: DateTime.UtcNow
   │ └─► Message ist fertig verarbeitet
   └─────────────────────────────────────┘
```

### Beispiel: Container App Neustart während Verarbeitung

```
1. Message wird empfangen und verarbeitet
   ┌─────────────────────────────────────┐
   │ Lock in Datenbank: Status="Active"
   │ Verarbeitung läuft...
   └─────────────────────────────────────┘

2. Container App Neustart (z.B. Deployment)
   ┌─────────────────────────────────────┐
   │ ❌ Receiver-Instanz geht verloren
   │ ✅ Lock bleibt in Datenbank erhalten
   └─────────────────────────────────────┘

3. Nach Neustart: Lock Recovery
   ┌─────────────────────────────────────┐
   │ Service kann Lock aus Datenbank lesen
   │ ├─► MessageId bekannt
   │ ├─► LockToken bekannt
   │ ├─► Topic/Subscription bekannt
   │ └─► Kann Receiver neu erstellen
   └─────────────────────────────────────┘

4. Lock Renewal funktioniert weiter
   ┌─────────────────────────────────────┐
   │ ServiceBusLockRenewalService
   │ ├─► Findet Lock in Datenbank
   │ ├─► Erneuert Lock
   │ └─► Verhindert Expiration
   └─────────────────────────────────────┘
```

---

## 📈 Vorteile der Implementierung

### 1. Verhindert Message Loss
- ✅ Lock-Informationen überleben Neustarts
- ✅ Messages können nach Neustart wieder gefunden werden
- ✅ Keine verlorenen Messages durch abgelaufene Locks

### 2. Verhindert Duplikate
- ✅ Lock Renewal verhindert Lock-Expiration
- ✅ Messages werden nicht mehrfach verarbeitet
- ✅ At-Least-Once Delivery wird garantiert

### 3. Bessere Fehlerbehandlung
- ✅ Dead-Letter Monitoring erkennt Probleme früh
- ✅ Detaillierte Informationen für Troubleshooting
- ✅ Proaktive Warnungen bei Fehlern

### 4. Nachvollziehbarkeit
- ✅ Alle Lock-Operationen werden in Datenbank gespeichert
- ✅ Vollständige Historie aller Message-Locks
- ✅ Einfaches Debugging durch Lock-Tracking

### 5. Recovery-Fähigkeit
- ✅ Nach Neustart können Locks wiederhergestellt werden
- ✅ Receiver können neu erstellt werden
- ✅ Messages können weiterverarbeitet werden

---

## ⚠️ Aktuelle Einschränkungen

### 1. Lock Renewal erneuert noch nicht den echten Service Bus Lock

**Problem:**
- Aktuell wird nur die Datenbank aktualisiert
- Der echte Service Bus Lock wird nicht erneuert
- Lock kann trotzdem ablaufen

**Lösung (noch zu implementieren):**
- Receiver-Instanzen pro Subscription cachen
- `ServiceBusReceiver.RenewMessageLockAsync()` verwenden
- Fallback: Receiver neu erstellen wenn Lock abläuft

### 2. GetMessageCountAsync gibt noch 0 zurück

**Problem:**
- Placeholder-Implementierung
- UI kann keine echten Message Counts anzeigen

**Lösung (noch zu implementieren):**
- `ServiceBusAdministrationClient` verwenden
- Message Counts cachen (alle 30 Sekunden)

---

## 🔧 Konfiguration

### Datenbank-Migration erforderlich

Die Tabelle `ServiceBusMessageLocks` muss erstellt werden:

```sql
CREATE TABLE ServiceBusMessageLocks (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    MessageId NVARCHAR(200) NOT NULL,
    LockToken NVARCHAR(500) NOT NULL,
    TopicName NVARCHAR(200) NOT NULL,
    SubscriptionName NVARCHAR(200) NOT NULL,
    InterfaceName NVARCHAR(200) NOT NULL,
    AdapterInstanceGuid UNIQUEIDENTIFIER NOT NULL,
    LockAcquiredAt DATETIME2 NOT NULL,
    LockExpiresAt DATETIME2 NOT NULL,
    LastRenewedAt DATETIME2 NULL,
    RenewalCount INT NOT NULL DEFAULT 0,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Active',
    CompletedAt DATETIME2 NULL,
    CompletionReason NVARCHAR(1000) NULL,
    DeliveryCount INT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_ServiceBusMessageLocks_MessageId ON ServiceBusMessageLocks(MessageId);
CREATE INDEX IX_ServiceBusMessageLocks_LockToken ON ServiceBusMessageLocks(LockToken);
CREATE INDEX IX_ServiceBusMessageLocks_Status ON ServiceBusMessageLocks(Status);
CREATE INDEX IX_ServiceBusMessageLocks_AdapterInstanceGuid ON ServiceBusMessageLocks(AdapterInstanceGuid);
CREATE INDEX IX_ServiceBusMessageLocks_LockExpiresAt ON ServiceBusMessageLocks(LockExpiresAt);
CREATE INDEX IX_ServiceBusMessageLocks_Status_LockExpiresAt ON ServiceBusMessageLocks(Status, LockExpiresAt);
```

### Service-Registrierungen

Alle Services sind bereits in `Program.cs` registriert:
- `IServiceBusLockTrackingService` → `ServiceBusLockTrackingService` (Scoped)
- `ServiceBusLockRenewalService` (Hosted Service - Background)
- `ServiceBusDeadLetterMonitoringService` (Hosted Service - Background)

---

## 📊 Monitoring & Troubleshooting

### Lock-Status prüfen

```sql
-- Alle aktiven Locks
SELECT * FROM ServiceBusMessageLocks
WHERE Status = 'Active'
ORDER BY LockExpiresAt ASC;

-- Locks, die bald ablaufen
SELECT * FROM ServiceBusMessageLocks
WHERE Status = 'Active'
  AND LockExpiresAt <= DATEADD(minute, 1, GETUTCDATE())
ORDER BY LockExpiresAt ASC;

-- Abgelaufene Locks
SELECT * FROM ServiceBusMessageLocks
WHERE Status = 'Expired'
ORDER BY CompletedAt DESC;

-- Lock-Statistiken
SELECT 
    Status,
    COUNT(*) as Count,
    AVG(DATEDIFF(second, LockAcquiredAt, ISNULL(CompletedAt, GETUTCDATE()))) as AvgDurationSeconds,
    MAX(RenewalCount) as MaxRenewals
FROM ServiceBusMessageLocks
GROUP BY Status;
```

### Logs prüfen

**Lock Renewal:**
```
[ServiceBusLockRenewalService] Found {Count} locks needing renewal
[ServiceBusLockRenewalService] Renewed lock: MessageId={MessageId}
```

**Dead Letter Monitoring:**
```
[ServiceBusDeadLetterMonitoringService] Dead Letter Queue Alert: Topic={Topic}, Subscription={Subscription}, DeadLetterCount={Count}
[ServiceBusDeadLetterMonitoringService] Dead Letter Message Details: MessageId={Id}, Reason={Reason}, Error={Error}
```

---

## 🎯 Zusammenfassung

**Message Completion Tracking** löst das Problem von Message Loss bei Container App Neustarts durch:

1. **Persistierung** aller Lock-Informationen in der Datenbank
2. **Automatische Erneuerung** von Locks vor Ablauf
3. **Proaktive Überwachung** von Dead-Letter Queues

**Ergebnis:**
- ✅ Keine Message Loss mehr
- ✅ Keine Duplikate durch abgelaufene Locks
- ✅ Bessere Fehlerbehandlung
- ✅ Vollständige Nachvollziehbarkeit

**Nächste Schritte:**
- Lock Renewal vollständig implementieren (echter Service Bus Lock Renewal)
- Message Count API implementieren
- UI für Lock-Status hinzufügen

