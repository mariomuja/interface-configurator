# Terraform Infrastructure as Code

This directory contains Terraform configurations for deploying Azure infrastructure.

## Prerequisites

1. Azure CLI installed and logged in: `az login`
2. Terraform installed (>= 1.0)
3. SQL Server command-line tools (sqlcmd) for database initialization

## Setup

1. Copy `terraform.tfvars.example` to `terraform.tfvars`
2. Fill in your values in `terraform.tfvars`
3. Initialize Terraform: `terraform init`
4. Review the plan: `terraform plan`
5. Apply the configuration: `terraform apply`

## Outputs

After deployment, Terraform will output:
- Storage account connection string (for Vercel environment variables)
- SQL Server FQDN and credentials
- Function App URL
- Event Grid topic name

## Database Initialization

The `init-database.sql` script creates the required tables:
- `TransportData` - Stores the imported CSV data
- `ProcessLogs` - Stores process logs and errors

## Important Notes

- All resources are created in the specified resource group
- SQL Server firewall rules allow Azure services by default
- Storage accounts use LRS (Locally Redundant Storage)
- Function App uses Consumption plan (serverless)
- Event Grid automatically triggers on blob creation in csv-uploads container



