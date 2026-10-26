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

@description('NCRONTAB schedule for the fuel check timer.')
param schedule string = '0 0 */2 * * *'

@description('.NET isolated runtime version for Flex Consumption.')
param dotnetVersion string = '10.0'

@description('Maximum Flex Consumption instance count.')
param maximumInstanceCount int = 40

@description('Per-instance memory (MB) for Flex Consumption.')
param instanceMemoryMB int = 2048

@description('ACS data location.')
param dataLocation string = 'United States'

@description('Smartcar API client secret. Empty = secret not created.')
@secure()
param smartcarClientSecret string = ''

@description('Smartcar API client id. Empty = setting omitted.')
param smartcarClientId string = ''

@description('Smartcar user id of the vehicle owner, returned by Smartcar Connect. Empty = setting omitted.')
param smartcarUserId string = ''

@description('Smartcar vehicle id. Empty = setting omitted and the provider looks it up from the user\'s single connection.')
param smartcarVehicleId string = ''

@description('Fuel data provider: fake (default), smartcar, or fordconnect.')
@allowed(['fake', 'smartcar', 'fordconnect'])
param fuelProvider string = 'fake'

@description('FordConnect Query OAuth client secret from developer.ford.com. Empty = secret not created.')
@secure()
param fordConnectClientSecret string = ''

@description('FordConnect Query seed refresh token from the one-time owner consent. Empty = secret not created.')
@secure()
param fordConnectRefreshToken string = ''

@description('FordConnect Query OAuth client id. Empty = setting omitted.')
param fordConnectClientId string = ''

@description('VIN of the consented vehicle. Empty = setting omitted and the provider reads it from /garage.')
param fordConnectVin string = ''

@description('Fake provider fuel percent (0-100). Empty = setting omitted, which the app treats as unavailable data.')
param fakeFuelPercentRemaining string = ''

@description('Fake provider estimated range in miles. Empty = setting omitted.')
param fakeFuelRangeMiles string = ''

var abbrs = loadJsonContent('abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = {
  'azd-env-name': environmentName
}
var deploymentContainerName = 'deployment-package'
var stateTableName = 'FuelAlertState'
var createSmartcarClientSecret = !empty(smartcarClientSecret)
var createFordConnectClientSecret = !empty(fordConnectClientSecret)
var createFordConnectRefreshToken = !empty(fordConnectRefreshToken)

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
    createSmartcarClientSecret: createSmartcarClientSecret
    fordConnectClientSecret: fordConnectClientSecret
    createFordConnectClientSecret: createFordConnectClientSecret
    fordConnectRefreshToken: fordConnectRefreshToken
    createFordConnectRefreshToken: createFordConnectRefreshToken
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
    schedule: schedule
    recipientEmail: recipientEmail
    vehicleId: vehicleId
    emailSenderAddress: communication.outputs.senderAddress
    acsConnectionStringSecretUri: keyVault.outputs.acsConnectionStringSecretUri
    smartcarClientSecretUri: keyVault.outputs.smartcarClientSecretUri
    smartcarClientId: smartcarClientId
    smartcarUserId: smartcarUserId
    smartcarVehicleId: smartcarVehicleId
    fuelProvider: fuelProvider
    fordConnectClientSecretUri: keyVault.outputs.fordConnectClientSecretUri
    fordConnectRefreshTokenUri: keyVault.outputs.fordConnectRefreshTokenUri
    fordConnectClientId: fordConnectClientId
    fordConnectVin: fordConnectVin
    fakeFuelPercentRemaining: fakeFuelPercentRemaining
    fakeFuelRangeMiles: fakeFuelRangeMiles
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
