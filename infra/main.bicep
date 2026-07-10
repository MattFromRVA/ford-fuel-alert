targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment (azd) — drives resource naming and the azd-env-name tag.')
param environmentName string

@description('Azure region for all resources.')
param location string

@description('Recipient email address for low-fuel alerts.')
param recipientEmail string

@description('Identifier of the vehicle being checked.')
param vehicleId string

@description('Fuel percentage at or below which an alert is sent.')
param thresholdPercent int = 25

@description('Fuel percentage at or above which the alert resets.')
param resetPercent int = 40

@description('.NET isolated runtime version for Flex Consumption.')
param dotnetVersion string = '10.0'

@description('Maximum Flex Consumption instance count.')
param maximumInstanceCount int = 40

@description('Per-instance memory (MB) for Flex Consumption.')
param instanceMemoryMB int = 2048

@description('ACS data location.')
param dataLocation string = 'United States'

@description('Smartcar OAuth client secret. Empty = secret not created.')
@secure()
param smartcarClientSecret string = ''

@description('Smartcar OAuth refresh token. Empty = secret not created.')
@secure()
param smartcarRefreshToken string = ''

var abbrs = loadJsonContent('abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = {
  'azd-env-name': environmentName
}
var deploymentContainerName = 'deployment-package'
var stateTableName = 'FuelAlertState'
var createSmartcarClientSecret = !empty(smartcarClientSecret)
var createSmartcarRefreshToken = !empty(smartcarRefreshToken)

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  scope: rg
  params: {
    logAnalyticsName: '${abbrs.logAnalyticsWorkspace}${resourceToken}'
    applicationInsightsName: '${abbrs.applicationInsights}${resourceToken}'
    location: location
    tags: tags
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  scope: rg
  params: {
    storageAccountName: '${abbrs.storageAccount}${resourceToken}'
    location: location
    tags: tags
    deploymentContainerName: deploymentContainerName
    stateTableName: stateTableName
  }
}

module communication 'modules/communication.bicep' = {
  name: 'communication'
  scope: rg
  params: {
    communicationServiceName: '${abbrs.communicationServices}${resourceToken}'
    emailServiceName: '${abbrs.emailServices}${resourceToken}'
    dataLocation: dataLocation
    tags: tags
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  scope: rg
  params: {
    keyVaultName: '${abbrs.keyVault}${resourceToken}'
    location: location
    tags: tags
    communicationServiceName: communication.outputs.communicationServiceName
    smartcarClientSecret: smartcarClientSecret
    smartcarRefreshToken: smartcarRefreshToken
    createSmartcarClientSecret: createSmartcarClientSecret
    createSmartcarRefreshToken: createSmartcarRefreshToken
  }
}

module functionApp 'modules/functionapp.bicep' = {
  name: 'functionapp'
  scope: rg
  params: {
    functionAppName: '${abbrs.functionApp}${resourceToken}'
    appServicePlanName: '${abbrs.appServicePlan}${resourceToken}'
    location: location
    tags: tags
    storageAccountName: storage.outputs.name
    deploymentStorageContainerName: deploymentContainerName
    deploymentBlobEndpoint: storage.outputs.primaryBlobEndpoint
    stateTableEndpoint: storage.outputs.primaryTableEndpoint
    stateTableName: stateTableName
    applicationInsightsConnectionString: monitoring.outputs.applicationInsightsConnectionString
    dotnetVersion: dotnetVersion
    maximumInstanceCount: maximumInstanceCount
    instanceMemoryMB: instanceMemoryMB
    thresholdPercent: thresholdPercent
    resetPercent: resetPercent
    recipientEmail: recipientEmail
    vehicleId: vehicleId
    emailSenderAddress: communication.outputs.senderAddress
    acsConnectionStringSecretUri: keyVault.outputs.acsConnectionStringSecretUri
    smartcarClientSecretUri: keyVault.outputs.smartcarClientSecretUri
    smartcarRefreshTokenUri: keyVault.outputs.smartcarRefreshTokenUri
  }
}

module roleAssignments 'modules/roleAssignments.bicep' = {
  name: 'roleAssignments'
  scope: rg
  params: {
    functionAppPrincipalId: functionApp.outputs.principalId
    storageAccountName: storage.outputs.name
    keyVaultName: keyVault.outputs.keyVaultName
  }
}

output AZURE_LOCATION string = location
output RESOURCE_GROUP_NAME string = rg.name
output FUNCTION_APP_NAME string = functionApp.outputs.name
output FUNCTION_APP_HOSTNAME string = functionApp.outputs.defaultHostName
output KEY_VAULT_NAME string = keyVault.outputs.keyVaultName
