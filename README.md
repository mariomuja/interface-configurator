# 📊 CSV to SQL Server Transport - Integration Demo

<div align="center">

[![Live Preview](https://img.shields.io/badge/🌐_Live_Preview-000000?style=for-the-badge&logo=vercel&logoColor=white)](https://infrastructure-as-code.vercel.app)
[![Azure](https://img.shields.io/badge/Azure-0078D4?style=for-the-badge&logo=microsoft-azure&logoColor=white)](https://azure.microsoft.com)
[![Terraform](https://img.shields.io/badge/Terraform-7B42BC?style=for-the-badge&logo=terraform&logoColor=white)](https://www.terraform.io)
[![Angular](https://img.shields.io/badge/Angular-DD0031?style=for-the-badge&logo=angular&logoColor=white)](https://angular.io)
[![Vercel](https://img.shields.io/badge/Vercel-000000?style=for-the-badge&logo=vercel&logoColor=white)](https://vercel.com)

**A complete data integration workflow demonstrating modern cloud-native integration patterns**

[Features](#-integration-concepts-implemented) • [Architecture](#-architecture-overview) • [Deployment](#-terraform-azure-infrastructure) • [Contact](#-contact)

</div>

---

## 🎯 What This Application Demonstrates

This application demonstrates a complete **data integration workflow** from CSV files to SQL Server database, showcasing modern cloud-native integration patterns and Infrastructure as Code (IaC) principles. It features a **pluggable adapter architecture** that allows easy swapping of data sources and destinations (CSV, SQL Server, and future adapters like JSON, SAP, REST APIs). It serves as a reference implementation for building scalable, maintainable data integration solutions on Microsoft Azure.

## 🚀 Integration Concepts Implemented

### 1. **Event-Driven Architecture**
- **Blob Storage Trigger**: Azure Function automatically triggered when CSV files are uploaded to Blob Storage
- **Asynchronous Processing**: Non-blocking data processing pipeline
- **Event Logging**: Comprehensive process logging for monitoring and debugging

### 2. **Dynamic Schema Management**
- **Schema-on-Write**: SQL table structure automatically adapts to CSV column structure
- **Dynamic Column Creation**: New CSV columns automatically create corresponding SQL columns
- **Type Inference**: Automatic data type detection and conversion (string, integer, decimal, date)
- **Schema Evolution**: Handles CSV schema changes without manual database migrations

### 3. **Row-Level Error Handling**
- **Type Validation**: Validates data types before insertion
- **Failed Row Isolation**: Individual failed rows saved as separate CSV files in error folder
- **Success/Failure Tracking**: Only successfully processed rows are inserted; failed rows are preserved for reprocessing
- **Error Details Logging**: Comprehensive error logging with exception details for troubleshooting

### 4. **Infrastructure as Code (IaC)**
- **Terraform**: Complete Azure infrastructure defined as code
- **Reproducible Deployments**: Infrastructure can be recreated identically across environments
- **Version Control**: All infrastructure changes tracked in Git
- **Automated Provisioning**: Single command deploys entire infrastructure stack

### 5. **Multi-Platform Architecture**
- **Frontend**: Angular application deployed on Vercel
- **Backend API**: Serverless functions on Vercel
- **Data Processing**: Azure Functions (C# .NET isolated runtime)
- **Storage**: Azure Blob Storage for CSV files
- **Database**: Azure SQL Database with dynamic schema

### 6. **Internationalization (i18n)**
- **5 Languages**: German, English, French, Spanish, Italian
- **Runtime Language Switching**: Users can change language without page reload
- **Persistent Language Preference**: Language selection saved in browser localStorage

### 7. **Data Quality & Validation**
- **Type Safety**: Automatic type detection and conversion
- **Data Integrity**: GUID primary keys (no IDENTITY columns)
- **Audit Trail**: `datetime_created` column with automatic timestamp on all tables
- **Error Recovery**: Failed rows preserved for manual review and reprocessing

### 8. **Adapter Pattern Architecture**
- **Pluggable Adapters**: CSV and SQL Server adapters implementing a common `IAdapter` interface
- **Source/Destination Flexibility**: Each adapter can be used as both source and destination
- **Interchangeable Components**: Easy to swap adapters (e.g., CSV → JSON, SQL Server → SAP)
- **Separation of Concerns**: CSV-specific logic isolated in `CsvAdapter`, SQL Server logic in `SqlServerAdapter`
- **Unified Interface**: All adapters implement `ReadAsync()`, `WriteAsync()`, `GetSchemaAsync()`, and `EnsureDestinationStructureAsync()`
- **Future Extensibility**: New adapters (JSON, SAP, REST APIs) can be added without changing core processing logic

### 9. **Modern Development Practices**
- **Clean Architecture**: Separation of concerns (Services, Models, Data Access, Adapters)
- **Dependency Injection**: Loose coupling and testability
- **Error Handling**: Comprehensive exception handling with detailed logging
- **Code Standards**: Consistent coding patterns and documentation
- **Design Patterns**: Adapter Pattern for data source/destination abstraction

## 🏗️ Architecture Overview

The application uses a multi-platform infrastructure with a pluggable adapter architecture:

- **Frontend**: Deployed on Vercel (Angular application with serverless functions)
- **Backend**: Deployed on Vercel serverless functions
- **Database**: Azure SQL Database
- **Storage**: Azure Storage Accounts
- **Processing**: Azure Function App for serverless functions

### Adapter Architecture

The data processing layer uses an **Adapter Pattern** to abstract data sources and destinations:

```
┌─────────────────┐
│  CsvProcessor   │
│  (Orchestrator) │
└────────┬────────┘
         │
    ┌────┴────┐
    │         │
┌───▼───┐ ┌──▼──────┐
│ Source │ │Destination│
│Adapter │ │  Adapter   │
└───┬───┘ └──┬───────┘
    │        │
┌───▼───┐ ┌──▼──────┐
│  CSV  │ │SQL Server│
│Adapter│ │ Adapter  │
└───────┘ └──────────┘
```

**Key Components:**

- **`IAdapter` Interface**: Common contract for all adapters
  - `ReadAsync()`: Read data from source
  - `WriteAsync()`: Write data to destination
  - `GetSchemaAsync()`: Retrieve schema information
  - `EnsureDestinationStructureAsync()`: Ensure destination structure matches schema

- **`CsvAdapter`**: Handles CSV file operations
  - Reads CSV files from Azure Blob Storage
  - Writes CSV files to Azure Blob Storage
  - Configurable field separator (UTF-8 character support)
  - Column count validation

- **`SqlServerAdapter`**: Handles SQL Server operations
  - Reads data from SQL Server tables
  - Writes data to SQL Server tables
  - Dynamic table structure management
  - Type conversion and validation

**Benefits:**

- **Flexibility**: Easy to add new adapters (JSON, SAP, REST APIs, etc.)
- **Testability**: Each adapter can be tested independently
- **Maintainability**: Source/destination logic is isolated and reusable
- **Extensibility**: New data sources/destinations don't require changes to core processing logic

## 🔧 Terraform (Azure Infrastructure)

### Prerequisites

1. **Terraform** >= 1.0 installed
2. **Azure Subscription** with appropriate permissions
3. **Service Principal** with Contributor role on the subscription

### Setup

1. **Create terraform.tfvars file**:
   ```bash
   cd terraform
   cp terraform.tfvars.example terraform.tfvars
   # Edit terraform.tfvars with your Service Principal credentials
   ```

   **Alternative: Use Environment Variables** (recommended for CI/CD):
   ```bash
   # Windows PowerShell
   $env:ARM_SUBSCRIPTION_ID="your-subscription-id"
   $env:ARM_CLIENT_ID="your-client-id"
   $env:ARM_CLIENT_SECRET="your-client-secret"
   $env:ARM_TENANT_ID="your-tenant-id"

   # Linux/Mac
   export ARM_SUBSCRIPTION_ID="your-subscription-id"
   export ARM_CLIENT_ID="your-client-id"
   export ARM_CLIENT_SECRET="your-client-secret"
   export ARM_TENANT_ID="your-tenant-id"
   ```

   If using environment variables, set the authentication variables to `null` in `terraform.tfvars`:
   ```hcl
   subscription_id = null
   client_id       = null
   client_secret   = null
   tenant_id       = null
   ```

2. **Initialize Terraform**:
   ```bash
   cd terraform
   terraform init
   ```

3. **Review the plan**:
   ```bash
   terraform plan
   ```

4. **Apply the configuration**:
   ```bash
   terraform apply
   ```

### Variables

Key variables to configure in `terraform.tfvars`:

- `subscription_id`: Azure subscription ID
- `client_id`: Service Principal Client ID
- `client_secret`: Service Principal Client Secret
- `tenant_id`: Azure Tenant ID
- `sql_admin_login`: SQL Server administrator username
- `sql_admin_password`: SQL Server administrator password
- `jwt_secret`: JWT secret for authentication
- `environment`: Environment name (dev, staging, prod)
- `location`: Azure region (default: West Europe)

### Resources Created

- **Resource Group**: Container for all resources
- **Azure SQL Server**: Logical SQL Server container
- **Azure SQL Database**: Application database
- **Storage Account**: General purpose storage
- **Function App** (optional): Serverless functions
- **Functions Storage Account**: Storage for Azure Functions

### Outputs

After applying, Terraform outputs:
- SQL Server connection details
- Function App URL (if enabled)
- Storage account information
- Resource group name

## 📦 Vercel Configuration

The frontend is deployed to Vercel. Configuration is in `vercel/vercel.json`.

### Deployment

#### Azure Functions

Azure Functions are automatically deployed via GitHub Actions using the **"Run from Package"** method (Microsoft's recommended approach).

**Quick Setup:**
```powershell
# Windows
.\setup-github-secrets.ps1
```

```bash
# Linux/Mac
./setup-github-secrets.sh
```

**Documentation:**
- [GitHub Actions Deployment](./GITHUB_ACTIONS_DEPLOYMENT.md) - Complete guide
- [Setup GitHub Secrets](./SETUP_GITHUB_SECRETS.md) - Automated setup
- [Deployment Checklist](./DEPLOYMENT_CHECKLIST.md) - Step-by-step checklist
- [Documentation Index](./DOCUMENTATION_INDEX.md) - Complete documentation overview

#### Vercel

Vercel deployments are automatically triggered on git push to the main branch.

## 🔐 Environment Variables

### Frontend (Vercel)

Set in Vercel dashboard or via CLI:

- `DATABASE_URL`: SQL Server connection string
- `JWT_SECRET`: JWT secret for authentication
- `NODE_ENV`: Environment (production)

### Azure Functions

Configured via Terraform in Function App settings:

- `DATABASE_URL`: SQL Server connection string
- `NODE_ENV`: Environment
- `FUNCTIONS_WORKER_RUNTIME`: Node.js runtime

## 🔒 Security Considerations

- **Secrets**: Never commit `terraform.tfvars` with real values
- **Firewall Rules**: Configure SQL Server firewall to allow only necessary IPs
- **SSL/TLS**: All connections use SSL/TLS encryption
- **CORS**: Configure CORS origins appropriately
- **JWT Secrets**: Use strong, randomly generated secrets

## 💰 Cost Optimization

- Use appropriate SKU sizes for your workload
- Consider using Azure SQL Database Basic tier for development
- Use consumption plan for Function Apps when possible

## 🐛 Troubleshooting

### Terraform Issues

- **Authentication**: Verify Service Principal credentials are correct
- **Permissions**: Ensure Service Principal has Contributor role on subscription
- **Resource Names**: Some names must be globally unique
- **Subscription**: Verify subscription_id matches your Azure subscription

### Database Connection Issues

- **Firewall**: Check SQL Server firewall rules
- **SSL**: Ensure SSL mode is set correctly
- **Credentials**: Verify username and password

### Deployment Issues

- **Build Errors**: Check Node.js version compatibility
- **Environment Variables**: Verify all required variables are set
- **CORS**: Check CORS configuration matches frontend URL
- **Function App Deployment**: See [GITHUB_ACTIONS_DEPLOYMENT.md](./GITHUB_ACTIONS_DEPLOYMENT.md)
- **GitHub Secrets**: See [SETUP_GITHUB_SECRETS.md](./SETUP_GITHUB_SECRETS.md)

## 🔧 Maintenance

### Updates

1. Modify Terraform files as needed
2. Run `terraform plan` to review changes
3. Apply with `terraform apply`
4. Update documentation

### Backups

- SQL Database backups are configured automatically
- Consider additional backup strategies for production

## 📚 Support

For issues or questions:
- Check Terraform documentation: https://registry.terraform.io/providers/hashicorp/azurerm
- Azure documentation: https://docs.microsoft.com/azure
- Vercel documentation: https://vercel.com/docs

---

## 👤 Contact

<div align="center">

**Mario Muja**

**Call me:** +49 1520 464 14 73 / +39 345 345 00 98

[![Email](https://img.shields.io/badge/Email-D14836?style=for-the-badge&logo=gmail&logoColor=white)](mailto:mario.muja@gmail.com)
[![GitHub](https://img.shields.io/badge/GitHub-181717?style=for-the-badge&logo=github&logoColor=white)](https://github.com/mariomuja)
[![LinkedIn](https://img.shields.io/badge/LinkedIn-0077B5?style=for-the-badge&logo=linkedin&logoColor=white)](https://www.linkedin.com/in/mario-muja-016782347)

</div>

---

<div align="center">

*This project demonstrates modern cloud-native integration patterns and Infrastructure as Code practices for data integration workflows.*

Made with ❤️ using Azure, Terraform, Angular, and Vercel

</div>
