#!/bin/bash
set -e

export PATH="/usr/bin:/usr/local/bin:$PATH"

echo "Setting up deployment environment..."

# Using latest .NET 8 SDK (8.0 tag pulls the latest 8.0.x patch version)
/usr/bin/docker run --rm --volumes-from interface-configurator-jenkins -w "$PWD" \
  -e AZURE_CLIENT_ID="$AZURE_CLIENT_ID" \
  -e AZURE_CLIENT_SECRET="$AZURE_CLIENT_SECRET" \
  -e AZURE_TENANT_ID="$AZURE_TENANT_ID" \
  -e AZURE_SUBSCRIPTION_ID="$AZURE_SUBSCRIPTION_ID" \
  -e AZURE_FUNCTION_APP_NAME="$AZURE_FUNCTION_APP_NAME" \
  -e AZURE_RESOURCE_GROUP="$AZURE_RESOURCE_GROUP" \
  mcr.microsoft.com/dotnet/sdk:8.0 bash -c "
  apt-get update && apt-get install -y curl gnupg lsb-release
  echo 'Installing Azure CLI...'
  curl -sL https://aka.ms/InstallAzureCLIDeb | bash
  echo 'Installing Azure Functions Core Tools...'
  curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > /etc/apt/trusted.gpg.d/microsoft.gpg
  sh -c 'echo \"deb [arch=amd64] https://packages.microsoft.com/repos/microsoft-ubuntu-\$(lsb_release -cs)-prod \$(lsb_release -cs) main\" > /etc/apt/sources.list.d/dotnetdev.list'
  apt-get update && apt-get install -y azure-functions-core-tools-4
  echo 'Logging in to Azure...'
  if [ -n \"\$AZURE_CLIENT_ID\" ] && [ -n \"\$AZURE_CLIENT_SECRET\" ] && [ -n \"\$AZURE_TENANT_ID\" ]; then
    az login --service-principal -u \"\$AZURE_CLIENT_ID\" -p \"\$AZURE_CLIENT_SECRET\" --tenant \"\$AZURE_TENANT_ID\"
    if [ -n \"\$AZURE_SUBSCRIPTION_ID\" ]; then
      az account set --subscription \"\$AZURE_SUBSCRIPTION_ID\"
    fi
    echo 'Azure login successful'
    az account show
  else
    echo 'Azure credentials not set. Please configure Jenkins credentials.'
    exit 1
  fi
  echo 'Deploying Azure Function App to production...'
  echo 'Step 1: Publishing Function App (--self-contained false)...'
  cd azure-functions/main
  dotnet publish main.csproj --self-contained false --configuration Release --output ./publish
  echo 'Publish completed'
  if [ -z \"\$AZURE_FUNCTION_APP_NAME\" ] || [ -z \"\$AZURE_RESOURCE_GROUP\" ]; then
    echo 'Required variables not set: AZURE_FUNCTION_APP_NAME, AZURE_RESOURCE_GROUP'
    exit 1
  fi
  echo 'Step 2: Deploying to Azure Function App...'
  echo \"Function App: \$AZURE_FUNCTION_APP_NAME\"
  echo \"Resource Group: \$AZURE_RESOURCE_GROUP\"
  cd publish
  func azure functionapp publish \"\$AZURE_FUNCTION_APP_NAME\" --dotnet-isolated
  echo 'Deployment completed successfully'
  echo \"Function App URL: https://\${AZURE_FUNCTION_APP_NAME}.azurewebsites.net\"
"


