@description('Name of the Key Vault (3-24 alphanumerics and dashes).')
param keyVaultName string

@description('Azure region for the resources.')
param location string

@description('Tags applied to every resource.')
param tags object = {}

@description('Name of the ACS resource whose connection string is stored as a secret.')
param communicationServiceName string

@description('Smartcar API client secret. Only stored when createSmartcarClientSecret is true.')
@secure()
param smartcarClientSecret string = ''

@description('Whether to create the Smartcar client-secret secret. Kept separate from the secure value so outputs stay untainted.')
param createSmartcarClientSecret bool = false

@description('FordConnect Query OAuth client secret. Only stored when createFordConnectClientSecret is true.')
@secure()
param fordConnectClientSecret string = ''

@description('Whether to create the FordConnect client-secret secret.')
param createFordConnectClientSecret bool = false

@description('FordConnect Query seed refresh token. Only stored when createFordConnectRefreshToken is true.')
@secure()
param fordConnectRefreshToken string = ''

@description('Whether to create the FordConnect refresh-token secret.')
param createFordConnectRefreshToken bool = false

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    publicNetworkAccess: 'Enabled'
  }
}

resource communicationService 'Microsoft.Communication/communicationServices@2023-04-01' existing = {
  name: communicationServiceName
}

resource acsConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'acs-connection-string'
  properties: {
    value: communicationService.listKeys().primaryConnectionString
  }
}

resource smartcarClientSecretSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (createSmartcarClientSecret) {
  parent: keyVault
  name: 'smartcar-client-secret'
  properties: {
    value: smartcarClientSecret
  }
}

resource fordConnectClientSecretSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (createFordConnectClientSecret) {
  parent: keyVault
  name: 'fordconnect-client-secret'
  properties: {
    value: fordConnectClientSecret
  }
}

resource fordConnectRefreshTokenSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (createFordConnectRefreshToken) {
  parent: keyVault
  name: 'fordconnect-refresh-token'
  properties: {
    value: fordConnectRefreshToken
  }
}

output keyVaultName string = keyVault.name
output acsConnectionStringSecretUri string = acsConnectionStringSecret.properties.secretUri
output smartcarClientSecretUri string = createSmartcarClientSecret ? '${keyVault.properties.vaultUri}secrets/smartcar-client-secret' : ''
output fordConnectClientSecretUri string = createFordConnectClientSecret ? '${keyVault.properties.vaultUri}secrets/fordconnect-client-secret' : ''
output fordConnectRefreshTokenUri string = createFordConnectRefreshToken ? '${keyVault.properties.vaultUri}secrets/fordconnect-refresh-token' : ''
