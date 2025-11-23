# Adapter Settings Dialog - Vereinfachung für SAP, Dynamics365 und CRM

## Übersicht

Der Settings-Dialog für SAP, Dynamics365 und CRM Adapter wird vereinfacht, sodass Benutzer:
1. Das Zielsystem auswählen (SAP, Dynamics365, CRM)
2. Ein Modul/Endpoint aus einer Dropdown-Liste auswählen
3. Eine Entity aus einer Dropdown-Liste auswählen
4. Automatisch konfiguriert werden

## Backend-Änderungen

### 1. TargetSystemService (`azure-functions/main/Services/TargetSystemService.cs`)
- ✅ Erstellt
- Stellt verfügbare Zielsysteme und Endpunkte bereit
- Enthält vordefinierte Module für:
  - **Dynamics365**: Finance, Supply Chain, Sales, Marketing, Customer Service, Field Service, Project Operations
  - **SAP**: OData Service, REST API, RFC Gateway, IDOC
  - **CRM**: Sales, Customer Service, Marketing

### 2. GetTargetSystems API (`azure-functions/main/GetTargetSystems.cs`)
- ✅ Erstellt
- Endpoint: `/api/GetTargetSystems`
- Gibt alle verfügbaren Zielsysteme und Endpunkte zurück

## Frontend-Änderungen

### 1. Interface-Erweiterung
- ✅ `AdapterPropertiesData` erweitert um:
  - `targetSystemId`: 'SAP' | 'Dynamics365' | 'CRM'
  - `endpointId`: ID des ausgewählten Endpoints
  - `entityName`: Name der Entity
  - Weitere spezifische Properties für jeden Adapter

### 2. Komponente erweitern
**TODO**: `adapter-properties-dialog.component.ts` erweitern mit:
- Properties für Target Systems, Endpoints, Entities
- Service zum Abrufen der Target Systems
- Methoden zur automatischen Konfiguration

### 3. HTML-Template erweitern
**TODO**: `adapter-properties-dialog.component.html` erweitern mit:
- Target System Dropdown (nur für SAP/Dynamics365/CRM)
- Endpoint/Module Dropdown (abhängig von Target System)
- Entity Name Dropdown (abhängig von Endpoint)
- Automatische Anzeige/Versteckung von Feldern

## Dynamics 365 Module-Unterstützung

Der Dynamics 365 Adapter unterstützt jetzt verschiedene Module:

1. **Finance** (`/api/data/v9.2`)
   - Entities: accounts, invoices, customers, vendors, generalLedgerEntries
   
2. **Supply Chain Management** (`/api/data/v9.2`)
   - Entities: products, inventory, purchaseOrders, salesOrders, warehouses
   
3. **Sales** (`/api/data/v9.2`)
   - Entities: accounts, contacts, leads, opportunities, quotes, orders
   
4. **Marketing** (`/api/data/v9.2`)
   - Entities: contacts, leads, marketingLists, campaigns, events
   
5. **Customer Service** (`/api/data/v9.2`)
   - Entities: accounts, contacts, cases, knowledgeArticles, queues
   
6. **Field Service** (`/api/data/v9.2`)
   - Entities: workOrders, bookings, resources, equipment, incidents
   
7. **Project Operations** (`/api/data/v9.2`)
   - Entities: projects, projectTasks, projectTeams, timeEntries, expenses

**Hinweis**: Alle Module verwenden die gleiche OData API (`/api/data/v9.2`), aber unterschiedliche Entities. Die Auswahl des Moduls setzt automatisch die entsprechenden Standard-Entities.

## Implementierungsstatus

- ✅ Backend: TargetSystemService erstellt
- ✅ Backend: GetTargetSystems API erstellt
- ✅ Frontend: Interface erweitert
- ⏳ Frontend: Komponente erweitern
- ⏳ Frontend: HTML-Template erweitern
- ⏳ Frontend: Service zum Abrufen der Target Systems

## Nächste Schritte

1. Frontend-Service erstellen, um Target Systems abzurufen
2. Komponente erweitern mit Target System/Endpoint/Entity Auswahl
3. HTML-Template erweitern mit Dropdowns
4. Automatische Konfiguration implementieren
5. Testing

