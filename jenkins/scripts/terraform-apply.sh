#!/bin/bash
set -e

export PATH="/usr/bin:/usr/local/bin:$PATH"

echo "Running Terraform to deploy/update Azure infrastructure..."

if [ -z "$ARM_CLIENT_ID" ] || [ -z "$ARM_CLIENT_SECRET" ] || [ -z "$ARM_TENANT_ID" ] || [ -z "$ARM_SUBSCRIPTION_ID" ]; then
    echo "ERROR: Azure credentials not set (ARM_CLIENT_ID, ARM_CLIENT_SECRET, ARM_TENANT_ID, ARM_SUBSCRIPTION_ID)"
    exit 1
fi

# Use Terraform Docker image
/usr/bin/docker run --rm --volumes-from interface-configurator-jenkins -w "$PWD/terraform" \
  -e ARM_CLIENT_ID="$ARM_CLIENT_ID" \
  -e ARM_CLIENT_SECRET="$ARM_CLIENT_SECRET" \
  -e ARM_TENANT_ID="$ARM_TENANT_ID" \
  -e ARM_SUBSCRIPTION_ID="$ARM_SUBSCRIPTION_ID" \
  hashicorp/terraform:latest bash -c "
  echo 'Initializing Terraform...'
  terraform init -upgrade
  
  echo ''
  echo 'Validating Terraform configuration...'
  terraform validate
  
  echo ''
  echo 'Planning Terraform changes...'
  terraform plan -out=tfplan
  
  echo ''
  echo 'Applying Terraform changes...'
  terraform apply -auto-approve tfplan
  
  echo ''
  echo 'Terraform outputs:'
  terraform output
  
  echo ''
  echo '✅ Terraform deployment completed successfully'
"

echo "Azure infrastructure deployment completed"

