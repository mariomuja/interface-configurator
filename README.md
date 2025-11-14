# Infrastructure as Code

**Live Demo**: [https://infrastructure-as-code.vercel.app](https://infrastructure-as-code.vercel.app)

This directory contains infrastructure-as-code definitions for the International Bookkeeping application.

## Architecture Overview

The application uses a multi-platform infrastructure:

- **Frontend**: Deployed on Vercel (Angular application with serverless functions)
- **Backend**: Deployed on Vercel serverless functions
- **Database**: Azure SQL Database
- **Storage**: Azure Storage Accounts
- **Optional**: Azure Function App for serverless functions

## Terraform (Azure Infrastructure)

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

## Vercel Configuration

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

## Environment Variables

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

## Security Considerations

- **Secrets**: Never commit `terraform.tfvars` with real values
- **Firewall Rules**: Configure SQL Server firewall to allow only necessary IPs
- **SSL/TLS**: All connections use SSL/TLS encryption
- **CORS**: Configure CORS origins appropriately
- **JWT Secrets**: Use strong, randomly generated secrets

## Cost Optimization

- Use appropriate SKU sizes for your workload
- Consider using Azure SQL Database Basic tier for development
- Use consumption plan for Function Apps when possible

## Troubleshooting

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

## Maintenance

### Updates

1. Modify Terraform files as needed
2. Run `terraform plan` to review changes
3. Apply with `terraform apply`
4. Update documentation

### Backups

- SQL Database backups are configured automatically
- Consider additional backup strategies for production

## Support

For issues or questions:
- Check Terraform documentation: https://registry.terraform.io/providers/hashicorp/azurerm
- Azure documentation: https://docs.microsoft.com/azure
- Vercel documentation: https://vercel.com/docs
