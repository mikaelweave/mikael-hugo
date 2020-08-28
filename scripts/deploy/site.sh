#!/bin/bash

set -euo pipefail

# Get the script dir so it doesn't matter where it is called from
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
cd $SCRIPT_DIR/../../

# Load settings
export $(grep -v '^#' .env | xargs)

# Install node and python packages
npm install
pip install -r python_requirements.txt

# Login with service principal if approperiate vars are set
if [ ${ARM_CLIENT_ID+x} ] && [ ${ARM_CLIENT_SECRET+x} ] && [ ${ARM_TENANT_ID+x} ]; 
then
    az login --service-principal --username "$ARM_CLIENT_ID" --password "$ARM_CLIENT_SECRET" --tenant "$ARM_TENANT_ID"
fi

# Select our subscription
az account set --subscription $SUBSCRIPTION_ID

# Pull Srcset file
export CDN_BLOB_ACCOUNT_KEY=$(az storage account keys list --account-name $CDN_BLOB_ACCOUNT_NAME --output tsv --query '[0].value')
npm run-script pull-srcsets

# Build Hugo
hugo --minify

# Get end date for SAS token for upload
if [[ "$OSTYPE" == "darwin"* ]]; then
    end=`date -u -v+5M '+%Y-%m-%dT%H:%M:00Z'`
elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
    end=`date -u -d "5 minutes" '+%Y-%m-%dT%H:%M:00Z'`
else
    echo "Invalid OS detected - this script only supports Darwin or Linux"
    exit -1
fi

# Get SAS tokens for upload
buildsSas=`az storage container generate-sas --account-name $SITE_BLOB_ACCOUNT_NAME --name builds --https-only --permissions dlrw --expiry $end -o tsv`
webSas=`az storage container generate-sas --account-name $SITE_BLOB_ACCOUNT_NAME --name '$web' --https-only --permissions dlrw --expiry $end -o tsv`

# Get the git sha if not set (will be set in pipeline, not locally)
if [ -z ${GITHUB_SHA+x} ]; 
then
    GITHUB_SHA=`git rev-parse HEAD`
fi

# Upload site to Azure
azcopy sync "./public/" "https://${SITE_BLOB_ACCOUNT_NAME}.blob.core.windows.net/builds/${GITHUB_SHA::8}?${buildsSas}" --delete-destination=true
azcopy sync "https://${SITE_BLOB_ACCOUNT_NAME}.blob.core.windows.net/builds/${GITHUB_SHA::8}?${buildsSas}" "https://${SITE_BLOB_ACCOUNT_NAME}.blob.core.windows.net/\$web?${webSas}" --delete-destination=true