# Deployment Update Instructions

## Overview
This guide will help you update GitHub, Vercel, and Azure Functions.

## Prerequisites
- Git installed and configured
- Azure CLI installed (for manual Azure Functions deployment)
- Vercel CLI installed (optional, for manual Vercel deployment)

## Step 1: Update GitHub Repository

### For interface-configurator (main project):

```powershell
# Navigate to project directory
cd C:\Users\mario\infrastructure-as-code  # or interface-configurator if renamed

# Check git status
git status

# Add all changes
git add .

# Commit changes
git commit -m "Add SFTP support to CsvAdapter and update repository references"

# Push to GitHub
git push origin main
```

### For mariomuja profile README:

```powershell
# Navigate to profile repository
cd C:\Users\mario\mariomuja

# Check git status
git status

# Add README changes
git add README.md

# Commit changes
git commit -m "Add Interface Configurator to profile README"

# Push to GitHub
git push origin main
```

## Step 2: Deploy Azure Functions

Azure Functions will be automatically deployed via GitHub Actions when you push changes to the `main` branch that affect files in `azure-functions/**`.

### Automatic Deployment (Recommended):
1. Push changes to GitHub (Step 1)
2. GitHub Actions workflow will automatically:
   - Build the .NET Functions
   - Create deployment package
   - Deploy to Azure Function App
   - Verify deployment

### Manual Deployment (If needed):

```powershell
# Navigate to Azure Functions directory
cd C:\Users\mario\infrastructure-as-code\azure-functions\main

# Build and publish
dotnet publish --configuration Release --output ./publish

# Create ZIP package
cd publish
Compress-Archive -Path * -DestinationPath ..\..\function-app.zip -Force
cd ..\..

# Deploy using Azure CLI (requires Azure CLI login)
az functionapp deployment source config-zip `
  --resource-group rg-interface-configuration `
  --name func-integration-main `
  --src function-app.zip
```

## Step 3: Deploy to Vercel

### Automatic Deployment (Recommended):
Vercel automatically deploys when you push to GitHub if connected.

### Manual Deployment:

```powershell
# Navigate to project root
cd C:\Users\mario\infrastructure-as-code

# Install Vercel CLI (if not installed)
npm install -g vercel

# Login to Vercel (if not logged in)
vercel login

# Deploy
vercel --prod
```

### Or use Vercel Dashboard:
1. Go to https://vercel.com/dashboard
2. Select your project
3. Click "Redeploy" or wait for automatic deployment

## Step 4: Verify Deployments

### Verify GitHub:
- Check repository: https://github.com/mariomuja/interface-configurator
- Check Actions: https://github.com/mariomuja/interface-configurator/actions

### Verify Azure Functions:
```powershell
# Check Function App status
az functionapp show `
  --resource-group rg-interface-configuration `
  --name func-integration-main `
  --query "{name: name, state: state, defaultHostName: defaultHostName}" `
  --output table

# List functions
az functionapp function list `
  --resource-group rg-interface-configuration `
  --name func-integration-main `
  --output table
```

### Verify Vercel:
- Check deployment: https://interface-configurator.vercel.app
- Check dashboard: https://vercel.com/dashboard

## Troubleshooting

### GitHub Actions Not Triggering:
- Ensure workflow file exists: `.github/workflows/deploy-functions.yml`
- Check that changes are in `azure-functions/**` path
- Verify GitHub secrets are set: `AZURE_CREDENTIALS`, `AZURE_RESOURCE_GROUP`, `AZURE_FUNCTIONAPP_NAME`

### Azure Functions Not Deploying:
- Check GitHub Actions logs
- Verify Function App exists and is running
- Check `WEBSITE_RUN_FROM_PACKAGE` setting

### Vercel Not Deploying:
- Check Vercel project is connected to GitHub repository
- Verify build settings in `vercel.json`
- Check Vercel deployment logs

## Quick Commands Summary

```powershell
# 1. Update GitHub (main project)
cd C:\Users\mario\infrastructure-as-code
git add .
git commit -m "Update: Add SFTP support and rename references"
git push origin main

# 2. Update GitHub (profile)
cd C:\Users\mario\mariomuja
git add README.md
git commit -m "Add Interface Configurator"
git push origin main

# 3. Azure Functions (auto-deploys via GitHub Actions)
# No action needed - will deploy automatically after Step 1

# 4. Vercel (auto-deploys if connected)
# No action needed - will deploy automatically after Step 1
```



