@description('Name of the Azure Communication Services resource.')
param communicationServiceName string

@description('Name of the Email Communication Service resource.')
param emailServiceName string

@description('Where ACS stores its data. Must be a supported ACS data location.')
param dataLocation string = 'United States'

@description('Tags applied to every resource.')
param tags object = {}

resource emailService 'Microsoft.Communication/emailServices@2023-04-01' = {
  name: emailServiceName
  location: 'global'
  tags: tags
  properties: {
    dataLocation: dataLocation
  }
}

resource managedDomain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  parent: emailService
  name: 'AzureManagedDomain'
  location: 'global'
  tags: tags
  properties: {
    domainManagement: 'AzureManaged'
    userEngagementTracking: 'Disabled'
  }
}

resource senderUsername 'Microsoft.Communication/emailServices/domains/senderUsernames@2023-04-01' = {
  parent: managedDomain
  name: 'donotreply'
  properties: {
    username: 'DoNotReply'
    displayName: 'Ford Fuel Alert'
  }
}

resource communicationService 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: communicationServiceName
  location: 'global'
  tags: tags
  properties: {
    dataLocation: dataLocation
    linkedDomains: [
      managedDomain.id
    ]
  }
}

output communicationServiceName string = communicationService.name
output senderAddress string = '${senderUsername.properties.username}@${managedDomain.properties.fromSenderDomain}'
