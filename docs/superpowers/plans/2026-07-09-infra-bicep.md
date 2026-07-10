# Infrastructure as Code (Bicep / azd) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Author an `azd`-compatible Bicep template set under `infra/` that provisions the FordFuelAlert Azure environment (Flex Consumption Function App, Storage, ACS Email, Key Vault, App Insights) using managed identity and Key Vault references throughout, with no secrets in source.

**Architecture:** A subscription-scoped `main.bicep` creates a resource group and orchestrates six resource-group-scoped modules (monitoring, storage, communication, keyvault, functionapp, roleAssignments). The Function App uses a system-assigned managed identity; storage and table access are identity-based (no connection strings), and secrets live only in Key Vault, referenced from app settings via `@Microsoft.KeyVault(...)`.

**Tech Stack:** Bicep (local modules only, no AVM/registry references), Azure Functions Flex Consumption (`Microsoft.Web` API `2024-04-01`), Azure CLI + Bicep compiler for local validation, `azd` for provisioning (documented, not run here).

## Global Constraints

- **This is template-authoring only.** Do NOT run a live deployment. Validation = `az bicep build` compiles with **0 errors** (and ideally 0 warnings). A live `azd provision --preview` / `az deployment sub create --what-if` is documented in the README for the operator.
- **No C# source changes.** Do NOT touch `Program.cs`, `EmailOptions.cs`, `SmartcarFuelService`, `TableAlertStateService`, or any file under `src/`/`tests/`. This ticket is `infra/` + `azure.yaml` + `README.md` only.
- **No new NuGet packages.** (Irrelevant to Bicep, but do not add any while here.)
- **`FuelAlert__UseFakeFuelService` stays `true`** in the template. The E2E issue flips it.
- **No secrets in source.** Secret *values* come only from secure parameters (empty defaults) or runtime `listKeys()`; `main.parameters.json` uses `${ENV_VAR}` substitution, never literal secrets.
- **Local modules only.** Do NOT reference `br/public:` AVM modules — keep `az bicep build` self-contained after the compiler is installed.
- **Flex Consumption specifics (verified against Microsoft Learn):**
  - Plan: `Microsoft.Web/serverfarms@2024-04-01`, `sku: { name: 'FC1', tier: 'FlexConsumption' }`, `properties.reserved: true`.
  - Site: `Microsoft.Web/sites@2024-04-01`, `kind: 'functionapp,linux'`, runtime set via `functionAppConfig.runtime` (`name: 'dotnet-isolated'`), NOT via a `FUNCTIONS_WORKER_RUNTIME` app setting (unsupported on Flex).
  - Host storage uses identity: app setting `AzureWebJobsStorage__accountName` = storage account name (system-assigned identity, no `__credential` needed).
- **Role definition GUIDs (built-in):** Storage Blob Data Owner `b7e6dc6d-f1e8-4753-8033-0f276bb0955b`; Storage Table Data Contributor `0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3`; Key Vault Secrets User `4633458b-17de-408a-b874-0445c86b69e6`.

---

## File Structure

```
azure.yaml                       # azd manifest → maps the fuel-alert service to src/FordFuelAlert.Functions
infra/
  main.bicep                     # subscription scope: RG + module orchestration + outputs
  main.parameters.json           # azd parameter file (env-var substitution, secure params)
  abbreviations.json             # resource-name abbreviations
  modules/
    monitoring.bicep             # Log Analytics workspace + Application Insights
    storage.bicep                # Storage account + deployment blob container + state table
    communication.bicep          # ACS + Email Comm Service + AzureManagedDomain + sender
    keyvault.bicep               # Key Vault (RBAC) + secrets (ACS conn string + optional Smartcar)
    functionapp.bicep            # Flex Consumption plan + Function App + system-assigned MI + app settings
    roleAssignments.bicep        # MI role grants scoped to storage + Key Vault
```

Each module compiles standalone (`az bicep build --file <module>`). `main.bicep` compiles the whole graph. Modules are wired only through explicit params/outputs.

---

### Task 1: Monitoring module (Log Analytics + Application Insights)

**Files:**
- Create: `infra/modules/monitoring.bicep`

**Interfaces:**
- Consumes: nothing (leaf module).
- Produces: `output applicationInsightsConnectionString string`, `output applicationInsightsName string`.

- [ ] **Step 1: Install the Bicep compiler** (one-time; needed for every compile step below)

```bash
az bicep install
az bicep version
```
Expected: prints a Bicep CLI version (e.g. `Bicep CLI version 0.x.y`). If already installed, `az bicep install` is a no-op.

- [ ] **Step 2: Create `infra/modules/monitoring.bicep`**

```bicep
@description('Name of the Log Analytics workspace.')
param logAnalyticsName string

@description('Name of the Application Insights component.')
param applicationInsightsName string

@description('Azure region for the resources.')
param location string

@description('Tags applied to every resource.')
param tags object = {}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

output applicationInsightsConnectionString string = applicationInsights.properties.ConnectionString
output applicationInsightsName string = applicationInsights.name
```

- [ ] **Step 3: Compile the module**

Run: `az bicep build --file infra/modules/monitoring.bicep --stdout > /dev/null && echo OK`
Expected: prints `OK` with no error/warning lines above it.

- [ ] **Step 4: Commit**

```bash
git add infra/modules/monitoring.bicep
git commit -m "Add monitoring Bicep module (Log Analytics + App Insights) (issue #6)"
```

---

### Task 2: Storage module (account + deployment container + state table)

**Files:**
- Create: `infra/modules/storage.bicep`

**Interfaces:**
- Consumes: nothing (leaf module).
- Produces: `output name string`, `output primaryBlobEndpoint string`, `output primaryTableEndpoint string`.

- [ ] **Step 1: Create `infra/modules/storage.bicep`**

```bicep
@description('Globally-unique storage account name (3-24 lowercase alphanumerics).')
param storageAccountName string

@description('Azure region for the resources.')
param location string

@description('Tags applied to every resource.')
param tags object = {}

@description('Blob container that holds the Flex Consumption deployment package.')
param deploymentContainerName string = 'deployment-package'

@description('Table that stores durable alert state.')
param stateTableName string = 'FuelAlertState'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowSharedKeyAccess: false
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource deploymentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: deploymentContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource stateTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  parent: tableService
  name: stateTableName
}

output name string = storageAccount.name
output primaryBlobEndpoint string = storageAccount.properties.primaryEndpoints.blob
output primaryTableEndpoint string = storageAccount.properties.primaryEndpoints.table
```

- [ ] **Step 2: Compile the module**

Run: `az bicep build --file infra/modules/storage.bicep --stdout > /dev/null && echo OK`
Expected: prints `OK` with no error/warning lines.

- [ ] **Step 3: Commit**

```bash
git add infra/modules/storage.bicep
git commit -m "Add storage Bicep module (account + deploy container + state table) (issue #6)"
```

---

### Task 3: Communication module (ACS + Email + managed domain)

**Files:**
- Create: `infra/modules/communication.bicep`

**Interfaces:**
- Consumes: nothing (leaf module).
- Produces: `output communicationServiceName string`, `output senderAddress string` (e.g. `DoNotReply@<generated>.azurecomm.net`).

Note: Communication resources are always `location: 'global'`. `dataLocation` controls where data is stored. The ACS connection string is intentionally NOT emitted as an output — the keyvault module reads it via `listKeys()` on the existing resource (Task 4) to avoid putting a secret in a module output.

- [ ] **Step 1: Create `infra/modules/communication.bicep`**

```bicep
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
```

- [ ] **Step 2: Compile the module**

Run: `az bicep build --file infra/modules/communication.bicep --stdout > /dev/null && echo OK`
Expected: prints `OK` with no error/warning lines.

- [ ] **Step 3: Commit**

```bash
git add infra/modules/communication.bicep
git commit -m "Add communication Bicep module (ACS + email + managed domain) (issue #6)"
```

---

### Task 4: Key Vault module (RBAC vault + secrets)

**Files:**
- Create: `infra/modules/keyvault.bicep`

**Interfaces:**
- Consumes: `communicationServiceName` (from Task 3's `communicationServiceName` output); secure params `smartcarClientSecret`, `smartcarRefreshToken` (empty by default).
- Produces: `output keyVaultName string`, `output acsConnectionStringSecretUri string`, `output smartcarClientSecretUri string` (empty string when the secret was not created), `output smartcarRefreshTokenUri string` (empty string when not created).

Note: The ACS connection-string secret is always created from `listKeys()` on the existing ACS resource. The two Smartcar secrets are created only when a non-empty value is supplied, so a deploy without Smartcar credentials leaves no dangling Key Vault references (their app settings in Task 5 are also conditional).

- [ ] **Step 1: Create `infra/modules/keyvault.bicep`**

```bicep
@description('Name of the Key Vault (3-24 alphanumerics and dashes).')
param keyVaultName string

@description('Azure region for the resources.')
param location string

@description('Tags applied to every resource.')
param tags object = {}

@description('Name of the ACS resource whose connection string is stored as a secret.')
param communicationServiceName string

@description('Smartcar OAuth client secret. Empty = do not create the secret.')
@secure()
param smartcarClientSecret string = ''

@description('Smartcar OAuth refresh token. Empty = do not create the secret.')
@secure()
param smartcarRefreshToken string = ''

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

resource smartcarClientSecretSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(smartcarClientSecret)) {
  parent: keyVault
  name: 'smartcar-client-secret'
  properties: {
    value: smartcarClientSecret
  }
}

resource smartcarRefreshTokenSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(smartcarRefreshToken)) {
  parent: keyVault
  name: 'smartcar-refresh-token'
  properties: {
    value: smartcarRefreshToken
  }
}

output keyVaultName string = keyVault.name
output acsConnectionStringSecretUri string = acsConnectionStringSecret.properties.secretUri
output smartcarClientSecretUri string = !empty(smartcarClientSecret) ? smartcarClientSecretSecret.properties.secretUri : ''
output smartcarRefreshTokenUri string = !empty(smartcarRefreshToken) ? smartcarRefreshTokenSecret.properties.secretUri : ''
```

- [ ] **Step 2: Compile the module**

Run: `az bicep build --file infra/modules/keyvault.bicep --stdout > /dev/null && echo OK`
Expected: prints `OK`. (If a `listKeys`-in-output warning appears, it should not — `listKeys()` is used only inside a resource `value`, never an output.)

- [ ] **Step 3: Commit**

```bash
git add infra/modules/keyvault.bicep
git commit -m "Add Key Vault Bicep module (RBAC vault + ACS/Smartcar secrets) (issue #6)"
```

---

### Task 5: Function App module (Flex Consumption plan + site + app settings)

**Files:**
- Create: `infra/modules/functionapp.bicep`

**Interfaces:**
- Consumes: `storageAccountName`, `deploymentBlobEndpoint`, `stateTableEndpoint` (Task 2); `applicationInsightsConnectionString` (Task 1); `emailSenderAddress` (Task 3); `acsConnectionStringSecretUri`, `smartcarClientSecretUri`, `smartcarRefreshTokenUri` (Task 4); plus config params below.
- Produces: `output name string`, `output principalId string` (system-assigned identity principal id, consumed by Task 6), `output defaultHostName string`.

- [ ] **Step 1: Create `infra/modules/functionapp.bicep`**

```bicep
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

@description('Key Vault secret URI for the Smartcar refresh token. Empty = omit the setting.')
param smartcarRefreshTokenUri string = ''

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
    name: 'Email__ConnectionString'
    value: '@Microsoft.KeyVault(SecretUri=${acsConnectionStringSecretUri})'
  }
]

var smartcarAppSettings = concat(
  empty(smartcarClientSecretUri) ? [] : [
    {
      name: 'Smartcar__ClientSecret'
      value: '@Microsoft.KeyVault(SecretUri=${smartcarClientSecretUri})'
    }
  ],
  empty(smartcarRefreshTokenUri) ? [] : [
    {
      name: 'Smartcar__RefreshToken'
      value: '@Microsoft.KeyVault(SecretUri=${smartcarRefreshTokenUri})'
    }
  ]
)

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
```

- [ ] **Step 2: Compile the module**

Run: `az bicep build --file infra/modules/functionapp.bicep --stdout > /dev/null && echo OK`
Expected: prints `OK` with no error/warning lines.

- [ ] **Step 3: Commit**

```bash
git add infra/modules/functionapp.bicep
git commit -m "Add Function App Bicep module (Flex Consumption + MI + app settings) (issue #6)"
```

---

### Task 6: Role assignments module (managed identity grants)

**Files:**
- Create: `infra/modules/roleAssignments.bicep`

**Interfaces:**
- Consumes: `functionAppPrincipalId` (Task 5's `principalId`), `storageAccountName` (Task 2), `keyVaultName` (Task 4).
- Produces: no outputs (side-effect module).

- [ ] **Step 1: Create `infra/modules/roleAssignments.bicep`**

```bicep
@description('Principal ID of the Function App system-assigned managed identity.')
param functionAppPrincipalId string

@description('Storage account to grant blob + table data access on.')
param storageAccountName string

@description('Key Vault to grant secrets read access on.')
param keyVaultName string

// Built-in role definition IDs.
var storageBlobDataOwnerRoleId = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var storageTableDataContributorRoleId = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource blobDataOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionAppPrincipalId, storageBlobDataOwnerRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataOwnerRoleId)
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource tableDataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionAppPrincipalId, storageTableDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageTableDataContributorRoleId)
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource keyVaultSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, functionAppPrincipalId, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}
```

- [ ] **Step 2: Compile the module**

Run: `az bicep build --file infra/modules/roleAssignments.bicep --stdout > /dev/null && echo OK`
Expected: prints `OK` with no error/warning lines.

- [ ] **Step 3: Commit**

```bash
git add infra/modules/roleAssignments.bicep
git commit -m "Add role-assignments Bicep module (MI storage + Key Vault grants) (issue #6)"
```

---

### Task 7: Orchestrator (`main.bicep`), abbreviations, azd manifest, params

**Files:**
- Create: `infra/abbreviations.json`
- Create: `infra/main.bicep`
- Create: `infra/main.parameters.json`
- Create: `azure.yaml`

**Interfaces:**
- Consumes: all six modules from Tasks 1-6.
- Produces: subscription-scoped deployment outputs (`AZURE_LOCATION`, `RESOURCE_GROUP_NAME`, `FUNCTION_APP_NAME`, `FUNCTION_APP_HOSTNAME`, `KEY_VAULT_NAME`).

- [ ] **Step 1: Create `infra/abbreviations.json`**

```json
{
  "logAnalyticsWorkspace": "log-",
  "applicationInsights": "appi-",
  "storageAccount": "st",
  "communicationServices": "acs-",
  "emailServices": "acsmail-",
  "keyVault": "kv-",
  "appServicePlan": "plan-",
  "functionApp": "func-"
}
```

- [ ] **Step 2: Create `infra/main.bicep`**

```bicep
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
```

- [ ] **Step 3: Create `infra/main.parameters.json`** (azd env-var substitution; no literal secrets)

```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "environmentName": {
      "value": "${AZURE_ENV_NAME}"
    },
    "location": {
      "value": "${AZURE_LOCATION}"
    },
    "recipientEmail": {
      "value": "${FUEL_ALERT_RECIPIENT_EMAIL}"
    },
    "vehicleId": {
      "value": "${FUEL_ALERT_VEHICLE_ID}"
    },
    "smartcarClientSecret": {
      "value": "${SMARTCAR_CLIENT_SECRET}"
    },
    "smartcarRefreshToken": {
      "value": "${SMARTCAR_REFRESH_TOKEN}"
    }
  }
}
```

- [ ] **Step 4: Create `azure.yaml`** (repo root)

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/Azure/azure-dev/main/schemas/v1.0/azure.yaml.json
name: ford-fuel-alert
metadata:
  template: ford-fuel-alert@0.0.1
services:
  fuel-alert:
    project: src/FordFuelAlert.Functions
    language: dotnet
    host: function
```

- [ ] **Step 5: Compile the full graph**

Run: `az bicep build --file infra/main.bicep --stdout > /dev/null && echo OK`
Expected: prints `OK` with no error/warning lines. This transitively compiles every module.

- [ ] **Step 6: Validate the JSON files parse**

Run: `python3 -c "import json; json.load(open('infra/main.parameters.json')); json.load(open('infra/abbreviations.json')); print('OK')"`
Expected: prints `OK`.

- [ ] **Step 7: Commit**

```bash
git add infra/main.bicep infra/main.parameters.json infra/abbreviations.json azure.yaml
git commit -m "Add main.bicep orchestrator, params, abbreviations, azure.yaml (issue #6)"
```

---

### Task 8: README infra section + final validation

**Files:**
- Modify: `README.md` (add an "Infrastructure (Bicep / azd)" section; link it from the existing "Deploying to Azure" section around line 189).

**Interfaces:**
- Consumes: the completed `infra/` set.
- Produces: documentation only.

- [ ] **Step 1: Add the infra section to `README.md`**

Insert the following section immediately BEFORE the existing `## Deploying to Azure` heading (so the manual steps that follow read as the fallback to IaC). Then add a one-line pointer at the top of `## Deploying to Azure`: `> Prefer the Bicep templates in [`infra/`](infra/) — see [Infrastructure (Bicep / azd)](#infrastructure-bicep--azd). The steps below are the manual equivalent.`

````markdown
## Infrastructure (Bicep / azd)

The Azure environment is defined as code under [`infra/`](infra/) so provisioning is
repeatable. It is `azd`-compatible and can also be deployed with plain `az`.

### Layout

```
azure.yaml                     # azd manifest → maps the fuel-alert service to the Function App
infra/
  main.bicep                   # subscription scope: creates the resource group + orchestrates modules
  main.parameters.json         # azd parameter file (environment-variable substitution)
  abbreviations.json           # resource-name abbreviations
  modules/
    monitoring.bicep           # Log Analytics workspace + Application Insights
    storage.bicep              # Storage account + deployment container + alert-state table
    communication.bicep        # Azure Communication Services + Email + Azure-managed domain
    keyvault.bicep             # Key Vault (RBAC) + secrets (ACS connection string, optional Smartcar)
    functionapp.bicep          # Flex Consumption plan + Function App + system-assigned identity
    roleAssignments.bicep      # managed-identity role grants (storage + Key Vault)
```

### What it provisions

- A **Flex Consumption** Function App (.NET isolated) with a **system-assigned managed identity**.
- A **Storage account** (shared-key access disabled) for host storage, the deployment package,
  and the durable alert-state table.
- **Azure Communication Services + Email** with an Azure-managed domain (`DoNotReply@…`).
- **Key Vault** (RBAC) holding the ACS connection string and, when supplied, the Smartcar secrets.
- **Application Insights** (backed by a Log Analytics workspace) for the timer function's logs.

### Security model

- **Identity, not secrets, for connections:** host storage uses `AzureWebJobsStorage__accountName`
  and alert state uses `AlertState__ServiceUri` — both resolved by the managed identity. Role grants:
  Storage Blob Data Owner, Storage Table Data Contributor, Key Vault Secrets User.
- **Secrets live only in Key Vault**, referenced from app settings via `@Microsoft.KeyVault(SecretUri=…)`.
  Secret values come from secure parameters (Smartcar) or the ACS resource's runtime keys — never
  from source control. `main.parameters.json` uses `${ENV_VAR}` substitution only.

### App settings wired by the template

`APPLICATIONINSIGHTS_CONNECTION_STRING`, `AzureWebJobsStorage__accountName`, `FuelAlert__*`
(with `UseFakeFuelService=true` — the E2E work flips it), `AlertState__Provider=table` +
`AlertState__ServiceUri` + `AlertState__TableName`, `Email__Provider=acs` + `Email__FromAddress`,
and Key Vault references for `Email__ConnectionString` (and `Smartcar__ClientSecret` /
`Smartcar__RefreshToken` when those secrets are supplied).

> On **Flex Consumption**, the worker runtime is set through `functionAppConfig.runtime`
> (`dotnet-isolated`), which replaces the classic `FUNCTIONS_WORKER_RUNTIME` app setting.
> `Email__ConnectionString` is wired ahead of the ACS-email service implementation (a separate
> issue); the current `ConsoleEmailService` does not read it.

### Deploy

With `azd` (recommended):

```bash
azd env new ffa-dev
azd env set FUEL_ALERT_RECIPIENT_EMAIL you@example.com
azd env set FUEL_ALERT_VEHICLE_ID demo-vehicle
azd provision --preview   # what-if
azd up                    # provision + deploy code
```

Or with plain `az` (subscription-scoped):

```bash
az deployment sub create \
  --location eastus2 \
  --template-file infra/main.bicep \
  --parameters environmentName=ffa-dev location=eastus2 \
               recipientEmail=you@example.com vehicleId=demo-vehicle \
  --what-if     # drop --what-if to actually deploy
```

### Notes

- **`dotnetVersion` defaults to `10.0`** to match this project's `net10.0` target. If a region has
  not yet enabled .NET 10 on Flex Consumption, pass a supported value
  (e.g. `--parameters dotnetVersion=9.0`) until it does.
- Compile the templates locally with `az bicep build --file infra/main.bicep`.
````

- [ ] **Step 2: Verify the README renders the section and the pointer**

Run: `grep -n "Infrastructure (Bicep / azd)" README.md && grep -n "Prefer the Bicep templates" README.md`
Expected: two matching lines print (the section heading and the pointer).

- [ ] **Step 3: Final full-graph validation**

Run: `az bicep build --file infra/main.bicep --stdout > /dev/null && echo BUILD_OK`
Expected: prints `BUILD_OK`. Investigate and fix any error or warning before committing.

- [ ] **Step 4: Commit**

```bash
git add README.md
git commit -m "Document infra Bicep layout and deployment (issue #6)"
```

---

## Acceptance-criteria mapping

- **`what-if` / `azd provision --preview` runs clean** → cannot be run here (no `azd`, no authenticated
  subscription); commands documented in the README for the operator. Local proxy: `az bicep build`
  compiles the whole graph with 0 errors (Tasks 1-8).
- **No secrets in source** → secure params + Key Vault + `${ENV_VAR}` substitution; verified by review
  (no literal secret values in any committed file).
- **Managed identity + Key Vault references used throughout** → Task 5 (system-assigned identity,
  identity-based storage/table, KV references) + Task 6 (role grants).
- **README describes layout + deploy** → Task 8.
```
