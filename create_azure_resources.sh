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
storage=$(az storage account create -n $BLOB_ACCOUNT_NAME --sku Standard_RAGRS)
echo $storage

# Create Function
az functionapp create --consumption-plan-location $LOCATION \
    --runtime python --runtime-version 3.7 --functions-version 2 \
    --name $BLOB_ACCOUNT_NAME --storage-account $BLOB_ACCOUNT_NAME \
    --os-type linux

# Create blob trigger
az eventgrid event-subscription create \
  --source-resource-id $(echo $storage | jq -r '.id') \
  --name $BLOB_ACCOUNT_NAME \
  --endpoint https://$BLOB_ACCOUNT_NAME.azurewebsites.net/api/updates \
  --advanced-filter subject StringEndsWith '[".jpg", ".jpeg", "png"]' \
  --advanced-filter subject StringNotIn '["$web", "azure-webjobs-"]'

wget https://github.com/mikaelweave/azure_image_resizer/raw/master/azure_image_resizer.zip
az functionapp deployment source config-zip --name $BLOB_ACCOUNT_NAME \
    --src azure_image_resizer.zip
rm -f azure_image_resizer.zip

az functionapp config appsettings set --name $BLOB_ACCOUNT_NAME \
    --settings "ImageSizes=480,768,1200,1400,1700,2000,2436"