# Set AZURE_FUNCTION_APP_URL in Vercel

## Function App URL

**Your Function App URL:**
```
https://func-integration-main.azurewebsites.net
```

## Quick Setup

### Option 1: Automated Script (Easiest)

```powershell
.\set-vercel-function-app-url.ps1
```

This script will:
- Automatically find your Function App URL
- Set it in Vercel via CLI
- Verify the Function App is accessible

### Option 2: Vercel Dashboard (Recommended)

1. Go to: https://vercel.com/dashboard
2. Select your project: `interface-configuration`
3. Go to: **Settings** → **Environment Variables**
4. Click **Add New**
5. Enter:
   - **Name**: `AZURE_FUNCTION_APP_URL`
   - **Value**: `https://func-integration-main.azurewebsites.net`
   - **Environment**: Select **Production** (and optionally Preview/Development)
6. Click **Save**
7. **Redeploy** the project:
   - Go to **Deployments** tab
   - Click the three dots (⋯) on the latest deployment
   - Click **Redeploy**

### Option 2: Vercel CLI

```bash
# Set the environment variable
vercel env add AZURE_FUNCTION_APP_URL production

# When prompted, enter:
https://func-integration-main.azurewebsites.net

# Redeploy
vercel deploy --prod
```

### Option 3: PowerShell Script

```powershell
# Get the URL from Terraform
cd terraform
terraform output -raw function_app_url

# Or use the helper script
cd ..
.\get-function-app-url.ps1 -FromTerraform
```

## Verify

After setting the variable and redeploying:

1. Open your app: https://interface-configurator.vercel.app
2. Click the **"Diagnose"** button
3. Check that "Function App URL" shows **OK**
4. Check that "Function App Connectivity" shows **OK**

## Troubleshooting

### Function App not accessible

If the Function App URL is set but connectivity check fails:

1. **Check Function App is running**:
   ```bash
   az functionapp show --name func-csv-to-sql-processor --resource-group rg-interface-configuration --query "{state:state, enabled:enabled}"
   ```

2. **Check Function App logs**:
   - Azure Portal → Function App → Log stream
   - Look for errors or warnings

3. **Test Function App endpoint manually**:
   ```bash
   curl https://func-integration-main.azurewebsites.net/api/GetProcessLogs
   ```
   Should return JSON (can be empty array `[]`)

4. **Check CORS settings** (if needed):
   - Azure Portal → Function App → CORS
   - Add `https://interface-configurator.vercel.app` if needed

### Get Function App URL from Azure

If you need to find the URL manually:

```bash
# List all Function Apps
az functionapp list --query "[].{Name:name, URL:defaultHostName, ResourceGroup:resourceGroup}" -o table

# Get specific Function App URL
az functionapp show \
  --name func-csv-to-sql-processor \
  --resource-group rg-interface-configuration \
  --query "defaultHostName" \
  -o tsv
```

### Get Function App URL from Terraform

```bash
cd terraform
terraform output function_app_url
```

## After Setting

Once `AZURE_FUNCTION_APP_URL` is set:

1. ✅ Process Logs will be fetched from Azure Function App
2. ✅ Clear Logs will work via Function App API
3. ✅ Diagnostics will show Function App connectivity status

