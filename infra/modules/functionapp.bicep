@description('Name of the Function App.')
param functionAppName string

@description('Name of the Flex Consumption App Service plan.')
param appServicePlanName string

@description('Azure region for the resources.')
param location string

@description('Tags applied to every resource.')
param tags object = {}

@description('Storage account used for host storage (AzureWebJobsStorage) and deployment.')
param storageAccountName string

@description('Blob container that holds the deployment package.')
param deploymentStorageContainerName string

@description('Primary blob endpoint of the storage account (ends with a slash).')
param deploymentBlobEndpoint string

@description('Primary table endpoint of the storage account (ends with a slash).')
param stateTableEndpoint string

@description('Table name for durable alert state.')
param stateTableName string

@description('Application Insights connection string.')
param applicationInsightsConnectionString string

@description('.NET isolated runtime version for Flex Consumption.')
param dotnetVersion string = '10.0'

@description('Maximum Flex Consumption instance count.')
param maximumInstanceCount int = 40

@description('Per-instance memory (MB) for Flex Consumption.')
param instanceMemoryMB int = 2048

@description('Fuel percentage at or below which an alert is sent.')
param thresholdPercent int = 25

@description('Fuel percentage at or above which the alert resets.')
param resetPercent int = 40

@description('Recipient email address for alerts.')
param recipientEmail string

@description('Identifier of the vehicle being checked.')
param vehicleId string

@description('ACS managed-domain sender address (e.g. DoNotReply@xxxx.azurecomm.net).')
param emailSenderAddress string

@description('Key Vault secret URI for the ACS connection string.')
param acsConnectionStringSecretUri string

@description('Key Vault secret URI for the Smartcar client secret. Empty = omit the setting.')
param smartcarClientSecretUri string = ''

resource appServicePlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  kind: 'functionapp'
  sku: {
    tier: 'FlexConsumption'
    name: 'FC1'
  }
  properties: {
    reserved: true
  }
}

var baseAppSettings = [
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: applicationInsightsConnectionString
  }
  {
    name: 'AzureWebJobsStorage__accountName'
    value: storageAccountName
  }
  {
    name: 'FuelAlert__ThresholdPercent'
    value: string(thresholdPercent)
  }
  {
    name: 'FuelAlert__ResetPercent'
    value: string(resetPercent)
  }
  {
    name: 'FuelAlert__RecipientEmail'
    value: recipientEmail
  }
  {
    name: 'FuelAlert__VehicleId'
    value: vehicleId
  }
  {
    name: 'FuelAlert__UseFakeFuelService'
    value: 'true'
  }
  {
    name: 'AlertState__Provider'
    value: 'table'
  }
  {
    name: 'AlertState__ServiceUri'
    value: stateTableEndpoint
  }
  {
    name: 'AlertState__TableName'
    value: stateTableName
  }
  {
    name: 'Email__Provider'
    value: 'acs'
  }
  {
    name: 'Email__FromAddress'
    value: emailSenderAddress
  }
  {
    name: 'Email__AcsConnectionString'
    value: '@Microsoft.KeyVault(SecretUri=${acsConnectionStringSecretUri})'
  }
]

var smartcarAppSettings = empty(smartcarClientSecretUri) ? [] : [
  {
    name: 'Smartcar__ClientSecret'
    value: '@Microsoft.KeyVault(SecretUri=${smartcarClientSecretUri})'
  }
]

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: functionAppName
  location: location
  tags: union(tags, { 'azd-service-name': 'fuel-alert' })
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    reserved: true
    serverFarmId: appServicePlan.id
    httpsOnly: true
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${deploymentBlobEndpoint}${deploymentStorageContainerName}'
          authentication: {
            type: 'SystemAssignedIdentity'
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: maximumInstanceCount
        instanceMemoryMB: instanceMemoryMB
      }
      runtime: {
        name: 'dotnet-isolated'
        version: dotnetVersion
      }
    }
    siteConfig: {
      appSettings: concat(baseAppSettings, smartcarAppSettings)
    }
  }
}

output name string = functionApp.name
output principalId string = functionApp.identity.principalId
output defaultHostName string = functionApp.properties.defaultHostName
