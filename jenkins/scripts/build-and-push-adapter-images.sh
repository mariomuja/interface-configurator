#!/bin/bash
set -e

export PATH="/usr/bin:/usr/local/bin:$PATH"

echo "Building and pushing adapter container images to ACR..."

if [ -z "$ACR_NAME" ]; then
    echo "ERROR: ACR_NAME not set"
    exit 1
fi

if [ -z "$ACR_USERNAME" ] || [ -z "$ACR_PASSWORD" ]; then
    echo "ERROR: ACR credentials not set"
    exit 1
fi

# Docker registry URL
ACR_URL="${ACR_NAME}.azurecr.io"

echo "Logging in to Azure Container Registry: $ACR_URL"
echo "$ACR_PASSWORD" | docker login "$ACR_URL" -u "$ACR_USERNAME" --password-stdin

# Build and push adapter images
# For now, adapters are part of the Function App, but this script is prepared for future containerization

# Example: CSV Adapter (when containerized)
# if [ -f "adapters/csv/Dockerfile" ]; then
#     echo "Building CSV Adapter image..."
#     docker build -t "${ACR_URL}/csv-adapter:${BUILD_NUMBER}" -t "${ACR_URL}/csv-adapter:latest" adapters/csv/
#     docker push "${ACR_URL}/csv-adapter:${BUILD_NUMBER}"
#     docker push "${ACR_URL}/csv-adapter:latest"
# fi

# Example: SQL Server Adapter (when containerized)
# if [ -f "adapters/sqlserver/Dockerfile" ]; then
#     echo "Building SQL Server Adapter image..."
#     docker build -t "${ACR_URL}/sqlserver-adapter:${BUILD_NUMBER}" -t "${ACR_URL}/sqlserver-adapter:latest" adapters/sqlserver/
#     docker push "${ACR_URL}/sqlserver-adapter:${BUILD_NUMBER}"
#     docker push "${ACR_URL}/sqlserver-adapter:latest"
# fi

echo "NOTE: Adapters are currently part of the Azure Function App deployment."
echo "When adapters are containerized, this script will build and push their Docker images."
echo "To containerize adapters in the future:"
echo "  1. Create Dockerfiles in adapters/{adapter-name}/"
echo "  2. Uncomment the build/push sections above"
echo "  3. Configure Azure Container Apps to pull from ACR"

echo "Adapter image build/push completed (currently no-op)"

