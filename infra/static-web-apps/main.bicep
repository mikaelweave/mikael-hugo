targetScope = 'resourceGroup'

@description('Azure region for the Static Web App.')
param location string = resourceGroup().location

@description('SKU for the Static Web App. Use Free for lower cost or Standard for production features.')
@allowed([
  'Free'
  'Standard'
])
param skuName string = 'Free'

@description('Name of the Static Web App.')
param staticWebAppName string

@description('Base tags to apply to the Static Web App.')
param tags object = {}

var defaultTags = union(tags, {
  managedBy: 'github-actions'
  workload: 'mikael.dev'
})

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staticWebAppName
  location: location
  sku: {
    name: skuName
    tier: skuName
  }
  tags: defaultTags
  properties: {
    allowConfigFileUpdates: true
  }
}

output defaultHostname string = staticWebApp.properties.defaultHostname
