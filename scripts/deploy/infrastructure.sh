#!/bin/bash

set -euo pipefail

# Get the script dir so it doesn't matter where it is called from
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
cd $SCRIPT_DIR/../../

# Load settings
export $(grep -v '^#' .env | xargs)

# Login with service principal if approperiate vars are set
if [ ${ARM_CLIENT_ID+x} ] && [ ${ARM_CLIENT_SECRET+x} ] && [ ${ARM_TENANT_ID+x} ]; 
then
    az login --service-principal --username "$ARM_CLIENT_ID" --password "$ARM_CLIENT_SECRET" --tenant "$ARM_TENANT_ID"
fi

# Select our subscription
az account set --subscription $SUBSCRIPTION_ID

# Create Resource Group
az group create -l $LOCATION -n $RESOURCE_GROUP_NAME

# Create Storage Account
storage=$(az storage account create --name $SITE_BLOB_ACCOUNT_NAME --resource-group $RESOURCE_GROUP_NAME --sku Standard_GRS)
az storage container create --name 'builds' --account-name $SITE_BLOB_ACCOUNT_NAME
az storage container create --name '$web' --account-name $SITE_BLOB_ACCOUNT_NAME

storagecdn=$(az storage account create --name $CDN_BLOB_ACCOUNT_NAME --resource-group $RESOURCE_GROUP_NAME --sku Standard_GRS --https-only false)
az storage container create --name data --account-name $CDN_BLOB_ACCOUNT_NAME

# Create Function
az functionapp create --resource-group $RESOURCE_GROUP_NAME \
    --consumption-plan-location $LOCATION --runtime python --runtime-version 3.8 --functions-version 3 \
    --name $FUNCTION_APP_NAME --storage-account $SITE_BLOB_ACCOUNT_NAME --os-type linux

# Get latest release of azure-web-img-dwnszr
curl -s https://api.github.com/repos/mikaelweave/azure-web-img-dwnszr/releases/latest | jq '.assets[0].browser_download_url' | xargs wget -O azure-web-img-dwnszr.zip

# Deploy function 
# Will currently throw error but deploy successfully - https://github.com/Azure/Azure-Functions/issues/1674
set +e
az functionapp deployment source config-zip --name $FUNCTION_APP_NAME --resource-group $RESOURCE_GROUP_NAME --src azure-web-img-dwnszr.zip
sleep 10
set -euo pipefail

# Setup settings for function
az functionapp config appsettings set --name $FUNCTION_APP_NAME --resource-group $RESOURCE_GROUP_NAME \
    --settings "StorageAccountConnectionString=$(az storage account show-connection-string --name $CDN_BLOB_ACCOUNT_NAME --output tsv)"
az functionapp config appsettings set --name $FUNCTION_APP_NAME  --resource-group $RESOURCE_GROUP_NAME \
    --settings "ImageSizes=$IMAGE_SIZES"

# Create blob trigger
# May require preview version of the exenthub extension
# See https://github.com/Azure/azure-cli/issues/12092#issuecomment-584883771
function_id=$(az functionapp show --name $FUNCTION_APP_NAME --resource-group $RESOURCE_GROUP_NAME --output tsv --query 'id')/functions/azure-web-img-dwnszr
az eventgrid event-subscription create \
  --source-resource-id $(echo $storagecdn | jq -r '.id') \
  --name $FUNCTION_APP_NAME-azure-web-img-dwnszr \
  --endpoint-type azurefunction \
  --endpoint $function_id \
  --included-event-types 'Microsoft.Storage.BlobCreated'
  #--advanced-filter subject StringEndsWith '.jpg' '.jpeg' 'png'

# Create action group for alerts
action_group_id=$(az monitor action-group create --resource-group $RESOURCE_GROUP_NAME --action email admin $EMAIL --name EmailErrorsAzureWebImgDwnszr --output tsv --query id)

# Create alert on function failures
az monitor metrics alert create --name "MIKAELDEV Azure Web Img Dwnsizr Error" --resource-group $RESOURCE_GROUP_NAME \
    --description "Alert when our Azure Web Img Dwnsizr function has an error (MIKAELDEV)" \
    --scopes "/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP_NAME}/providers/microsoft.insights/components/${FUNCTION_APP_NAME}" \
    --condition "count requests/failed > 0" \
    --evaluation-frequency "15m" \
    --window-size "30m" \
    --action $action_group_id