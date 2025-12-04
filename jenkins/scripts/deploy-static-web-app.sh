#!/bin/bash
set -e

export PATH="/usr/bin:/usr/local/bin:$PATH"

echo "Deploying Frontend to Azure Static Web App..."

if [ -z "$AZURE_STATIC_WEB_APP_TOKEN" ]; then
    echo "ERROR: AZURE_STATIC_WEB_APP_TOKEN not set"
    exit 1
fi

echo "Installing Static Web Apps CLI..."
/usr/bin/docker run --rm --volumes-from interface-configurator-jenkins -w "$PWD" \
  -e AZURE_STATIC_WEB_APP_TOKEN="$AZURE_STATIC_WEB_APP_TOKEN" \
  node:22 bash -c "
  npm install -g @azure/static-web-apps-cli
  echo 'Deploying frontend build to Azure Static Web App...'
  swa deploy ./frontend/dist/interface-configuration/browser \
    --deployment-token \"\$AZURE_STATIC_WEB_APP_TOKEN\" \
    --env production
  echo 'Static Web App deployment completed successfully'
"

echo "Frontend deployed successfully"

