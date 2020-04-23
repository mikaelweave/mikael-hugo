#1/bin/bash

# Load settings
export $(grep -v '^#' .env | xargs)

# Login
az login
az account set --subscription $SUBSCRIPTION_ID

# Create Resource Group
az group create -l $LOCATION -n $RESOURCE_GROUP_NAME
az configure --defaults group=$RESOURCE_GROUP_NAME location=$LOCATION   

# Create Storage Account
storage=$(az storage account create --name $SITE_BLOB_ACCOUNT_NAME --sku Standard_GRS)
storagecdn=$(az storage account create --name $CDN_BLOB_ACCOUNT_NAME --sku Standard_GRS --https-only false)
az storage container create --name data --account-name $CDN_BLOB_ACCOUNT_NAME

# Create Function
az functionapp create --consumption-plan-location $LOCATION \
    --runtime python --runtime-version 3.8 --functions-version 3 \
    --name $FUNCTION_APP_NAME --storage-account $SITE_BLOB_ACCOUNT_NAME \
    --os-type linux

# Get latest release
curl -s https://api.github.com/repos/mikaelweave/azure-web-img-dwnszr/releases/latest | jq '.assets[0].browser_download_url' | xargs wget -O azure-web-img-dwnszr.zip

# Deploy function 
# Will currently throw error but deploy successfully - https://github.com/Azure/azure-cli/issues/12513
az functionapp deployment source config-zip --name $FUNCTION_APP_NAME \
    --src azure-web-img-dwnszr.zip

# Setup settings for function
az functionapp config appsettings set --name $FUNCTION_APP_NAME \
    --settings "StorageAccountConnectionString=$(az storage account show-connection-string --name $CDN_BLOB_ACCOUNT_NAME --output tsv)"
az functionapp config appsettings set --name $FUNCTION_APP_NAME \
    --settings "ImageSizes=480,768,1200,1400,1700,2000,2436"

# Create blob trigger
# May require preview version of the exenthub extension
# See https://github.com/Azure/azure-cli/issues/12092#issuecomment-584883771
function_id=$(az functionapp show --name $FUNCTION_APP_NAME --output tsv --query 'id')/functions/azure-web-img-dwnsizr
az eventgrid event-subscription create \
  --source-resource-id $(echo $storagecdn | jq -r '.id') \
  --name $FUNCTION_APP_NAME-azure-web-img-dwnszr \
  --endpoint-type azurefunction \
  --endpoint $function_id \
  --included-event-types 'Microsoft.Storage.BlobCreated' \
  --advanced-filter subject StringEndsWith '.jpg' '.jpeg' 'png'

# Create action group for alerts
action_group_id=$(az monitor action-group create --action email admin $EMAIL --name EmailErrorsAzureWebImgDwnszr --output tsv --query id)

# Create alert on function failures
az monitor metrics alert create --name "MIKAELDEV Azure Web Img Dwnsizr Error" \
    --description "Alert when our Azure Web Img Dwnsizr function has an error (MIKAELDEV)" \
    --scopes "/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP_NAME}/providers/microsoft.insights/components/${FUNCTION_APP_NAME}" \
    --condition "count requests/failed > 0" \
    --evaluation-frequency "15m" \
    --window-size "30m" \
    --action $action_group_id