using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service for providing available target systems and their endpoints/modules
/// Simplifies configuration for SAP, Dynamics365, and CRM adapters
/// </summary>
public class TargetSystemService
{
    /// <summary>
    /// Gets available target systems
    /// </summary>
    public static List<TargetSystem> GetAvailableTargetSystems()
    {
        return new List<TargetSystem>
        {
            new TargetSystem
            {
                Id = "Dynamics365",
                Name = "Microsoft Dynamics 365",
                Description = "Microsoft Dynamics 365 Business Applications",
                Endpoints = GetDynamics365Endpoints()
            },
            new TargetSystem
            {
                Id = "SAP",
                Name = "SAP",
                Description = "SAP ERP / S/4HANA",
                Endpoints = GetSapEndpoints()
            },
            new TargetSystem
            {
                Id = "CRM",
                Name = "Microsoft CRM",
                Description = "Microsoft Dynamics 365 Customer Engagement (CRM)",
                Endpoints = GetCrmEndpoints()
            }
        };
    }

    /// <summary>
    /// Gets available Dynamics 365 endpoints/modules
    /// </summary>
    private static List<TargetSystemEndpoint> GetDynamics365Endpoints()
    {
        return new List<TargetSystemEndpoint>
        {
            new TargetSystemEndpoint
            {
                Id = "finance",
                Name = "Finance",
                Description = "Dynamics 365 Finance - Financial management and accounting",
                ApiVersion = "v9.2",
                BasePath = "/api/data/v9.2",
                CommonEntities = new List<string> { "accounts", "contacts", "invoices", "customers", "vendors", "generalLedgerEntries", "journalEntries" },
                ModuleType = "Finance"
            },
            new TargetSystemEndpoint
            {
                Id = "supplychain",
                Name = "Supply Chain Management",
                Description = "Dynamics 365 Supply Chain Management - Operations and logistics",
                ApiVersion = "v9.2",
                BasePath = "/api/data/v9.2",
                CommonEntities = new List<string> { "products", "inventory", "purchaseOrders", "salesOrders", "warehouses", "shipments" },
                ModuleType = "SupplyChain"
            },
            new TargetSystemEndpoint
            {
                Id = "sales",
                Name = "Sales",
                Description = "Dynamics 365 Sales - Sales and customer management",
                ApiVersion = "v9.2",
                BasePath = "/api/data/v9.2",
                CommonEntities = new List<string> { "accounts", "contacts", "leads", "opportunities", "quotes", "orders", "invoices" },
                ModuleType = "Sales"
            },
            new TargetSystemEndpoint
            {
                Id = "marketing",
                Name = "Marketing",
                Description = "Dynamics 365 Marketing - Marketing automation and campaigns",
                ApiVersion = "v9.2",
                BasePath = "/api/data/v9.2",
                CommonEntities = new List<string> { "contacts", "leads", "marketingLists", "campaigns", "events", "forms" },
                ModuleType = "Marketing"
            },
            new TargetSystemEndpoint
            {
                Id = "customerservice",
                Name = "Customer Service",
                Description = "Dynamics 365 Customer Service - Customer support and service",
                ApiVersion = "v9.2",
                BasePath = "/api/data/v9.2",
                CommonEntities = new List<string> { "accounts", "contacts", "cases", "knowledgeArticles", "queues", "serviceActivities" },
                ModuleType = "CustomerService"
            },
            new TargetSystemEndpoint
            {
                Id = "fieldservice",
                Name = "Field Service",
                Description = "Dynamics 365 Field Service - Field service management",
                ApiVersion = "v9.2",
                BasePath = "/api/data/v9.2",
                CommonEntities = new List<string> { "workOrders", "bookings", "resources", "equipment", "incidents", "timeEntries" },
                ModuleType = "FieldService"
            },
            new TargetSystemEndpoint
            {
                Id = "projectoperations",
                Name = "Project Operations",
                Description = "Dynamics 365 Project Operations - Project management and operations",
                ApiVersion = "v9.2",
                BasePath = "/api/data/v9.2",
                CommonEntities = new List<string> { "projects", "projectTasks", "projectTeams", "timeEntries", "expenses", "contracts" },
                ModuleType = "ProjectOperations"
            },
            new TargetSystemEndpoint
            {
                Id = "custom",
                Name = "Custom Entity",
                Description = "Custom entity or custom endpoint",
                ApiVersion = "v9.2",
                BasePath = "/api/data/v9.2",
                CommonEntities = new List<string>(),
                ModuleType = "Custom",
                IsCustom = true
            }
        };
    }

    /// <summary>
    /// Gets available SAP endpoints/modules
    /// </summary>
    private static List<TargetSystemEndpoint> GetSapEndpoints()
    {
        return new List<TargetSystemEndpoint>
        {
            new TargetSystemEndpoint
            {
                Id = "odata",
                Name = "OData Service (S/4HANA)",
                Description = "SAP S/4HANA OData Service - Modern RESTful API",
                ApiVersion = "v2",
                BasePath = "/sap/opu/odata/sap",
                CommonEntities = new List<string> { "SalesOrder", "PurchaseOrder", "Material", "Customer", "Vendor", "Invoice" },
                ModuleType = "OData"
            },
            new TargetSystemEndpoint
            {
                Id = "restapi",
                Name = "REST API (S/4HANA)",
                Description = "SAP S/4HANA REST API - RESTful services",
                ApiVersion = "v1",
                BasePath = "/sap/bc/rest",
                CommonEntities = new List<string> { "SalesOrder", "PurchaseOrder", "Material", "Customer", "Vendor" },
                ModuleType = "REST"
            },
            new TargetSystemEndpoint
            {
                Id = "rfc",
                Name = "RFC Gateway",
                Description = "SAP RFC Gateway - Classic SAP systems via HTTP",
                ApiVersion = "1.0",
                BasePath = "/sap/bc/soap/rfc",
                CommonEntities = new List<string> { "IDOC_INBOUND_ASYNCHRONOUS", "BAPI_SALESORDER_CREATEFROMDAT2", "RFC_READ_TABLE" },
                ModuleType = "RFC"
            },
            new TargetSystemEndpoint
            {
                Id = "idoc",
                Name = "IDOC",
                Description = "SAP IDOC - Intermediate Document processing",
                ApiVersion = "1.0",
                BasePath = "/sap/bc/idoc",
                CommonEntities = new List<string> { "ORDERS05", "INVOIC02", "MATMAS05", "CUSTOMER01" },
                ModuleType = "IDOC"
            }
        };
    }

    /// <summary>
    /// Gets available CRM endpoints/modules
    /// </summary>
    private static List<TargetSystemEndpoint> GetCrmEndpoints()
    {
        return new List<TargetSystemEndpoint>
        {
            new TargetSystemEndpoint
            {
                Id = "sales",
                Name = "Sales",
                Description = "CRM Sales entities - Leads, Opportunities, Quotes",
                ApiVersion = "v9.2",
                BasePath = "/api/data/v9.2",
                CommonEntities = new List<string> { "leads", "opportunities", "quotes", "orders", "invoices", "products" },
                ModuleType = "Sales"
            },
            new TargetSystemEndpoint
            {
                Id = "service",
                Name = "Customer Service",
                Description = "CRM Service entities - Cases, Knowledge Articles",
                ApiVersion = "v9.2",
                BasePath = "/api/data/v9.2",
                CommonEntities = new List<string> { "cases", "knowledgearticles", "queues", "serviceactivities", "contracts" },
                ModuleType = "Service"
            },
            new TargetSystemEndpoint
            {
                Id = "marketing",
                Name = "Marketing",
                Description = "CRM Marketing entities - Campaigns, Marketing Lists",
                ApiVersion = "v9.2",
                BasePath = "/api/data/v9.2",
                CommonEntities = new List<string> { "campaigns", "marketinglists", "contacts", "leads", "events" },
                ModuleType = "Marketing"
            },
            new TargetSystemEndpoint
            {
                Id = "custom",
                Name = "Custom Entity",
                Description = "Custom CRM entity",
                ApiVersion = "v9.2",
                BasePath = "/api/data/v9.2",
                CommonEntities = new List<string>(),
                ModuleType = "Custom",
                IsCustom = true
            }
        };
    }

    /// <summary>
    /// Gets endpoint configuration for a specific target system and endpoint
    /// </summary>
    public static TargetSystemEndpoint? GetEndpoint(string targetSystemId, string endpointId)
    {
        var system = GetAvailableTargetSystems().FirstOrDefault(s => s.Id == targetSystemId);
        return system?.Endpoints.FirstOrDefault(e => e.Id == endpointId);
    }
}

/// <summary>
/// Represents a target system (SAP, Dynamics365, CRM)
/// </summary>
public class TargetSystem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<TargetSystemEndpoint> Endpoints { get; set; } = new();
}

/// <summary>
/// Represents an endpoint/module within a target system
/// </summary>
public class TargetSystemEndpoint
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
    public string BasePath { get; set; } = string.Empty;
    public List<string> CommonEntities { get; set; } = new();
    public string ModuleType { get; set; } = string.Empty;
    public bool IsCustom { get; set; } = false;
}

