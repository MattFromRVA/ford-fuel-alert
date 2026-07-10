@description('Name of the Key Vault (3-24 alphanumerics and dashes).')
param keyVaultName string

@description('Azure region for the resources.')
param location string

@description('Tags applied to every resource.')
param tags object = {}

@description('Name of the ACS resource whose connection string is stored as a secret.')
param communicationServiceName string

@description('Smartcar OAuth client secret. Only stored when createSmartcarClientSecret is true.')
@secure()
param smartcarClientSecret string = ''

@description('Smartcar OAuth refresh token. Only stored when createSmartcarRefreshToken is true.')
@secure()
param smartcarRefreshToken string = ''

@description('Whether to create the Smartcar client-secret secret. Kept separate from the secure value so outputs stay untainted.')
param createSmartcarClientSecret bool = false

@description('Whether to create the Smartcar refresh-token secret.')
param createSmartcarRefreshToken bool = false

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

resource smartcarRefreshTokenSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (createSmartcarRefreshToken) {
  parent: keyVault
  name: 'smartcar-refresh-token'
  properties: {
    value: smartcarRefreshToken
  }
}

output keyVaultName string = keyVault.name
output acsConnectionStringSecretUri string = acsConnectionStringSecret.properties.secretUri
output smartcarClientSecretUri string = createSmartcarClientSecret ? '${keyVault.properties.vaultUri}secrets/smartcar-client-secret' : ''
output smartcarRefreshTokenUri string = createSmartcarRefreshToken ? '${keyVault.properties.vaultUri}secrets/smartcar-refresh-token' : ''
