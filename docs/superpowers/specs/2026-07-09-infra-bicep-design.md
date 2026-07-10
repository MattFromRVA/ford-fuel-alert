# Infrastructure as Code: Bicep for Function App, Storage, ACS Email, Key Vault

**Issue:** #6 — "Infrastructure as code: Bicep for Function App, Storage, ACS Email, Key Vault"
**Date:** 2026-07-09
**Scope:** Authoring the templates only. Actual provisioning happens in the separate deploy issue.

## Goal

Define the Azure environment as code (Bicep, `azd`-compatible) so provisioning is
repeatable and nothing is hand-clicked in the portal. Managed identity + Key Vault
references are used throughout; no secrets in source control.

## Non-goals / out of scope

- **No live provisioning.** This session authors and locally compiles the templates.
  A live `azd provision --preview` / `az deployment sub create --what-if` requires an
  authenticated subscription and is run by the operator (documented in the README).
- **No C# changes.** `Program.cs`, `EmailOptions.cs`, `TableAlertStateService`,
  `SmartcarFuelService`, and all other source files are untouched. Implementing the ACS
  email service and the Smartcar provider are separate issues.
- **`FuelAlert__UseFakeFuelService` stays `true`.** The E2E issue flips it.
- **No new NuGet packages.** Infra is Bicep + README only.

## File layout

```
azure.yaml                     # azd manifest → maps the Function App service to src/FordFuelAlert.Functions
infra/
  main.bicep                   # subscription scope: creates the resource group, orchestrates modules
  main.parameters.json         # azd parameter file (env-var substitution, secure params)
  abbreviations.json           # standard azd resource-name abbreviations
  modules/
    monitoring.bicep           # Log Analytics workspace + Application Insights
    storage.bicep              # Storage account (AzureWebJobsStorage + Table service) + deployment container
    keyvault.bicep             # Key Vault (RBAC authorization) + secrets sourced from secure params
    communication.bicep        # Azure Communication Services + Email Communication Service + Azure-managed domain
    functionapp.bicep          # Flex Consumption plan + Function App + system-assigned MI + app settings
    roleAssignments.bicep      # role grants for the Function App's managed identity (scoped to storage + KV)
```

## Architecture / resource graph

`main.bicep` is **subscription-scoped**. It:

1. Derives a `resourceToken` from `uniqueString(subscription().id, environmentName)` and
   builds resource names from `abbreviations.json` + prefix + token.
2. Creates a resource group `rg-${environmentName}` tagged `azd-env-name`.
3. Invokes modules (resource-group scoped) in dependency order:
   - `monitoring` → Log Analytics + App Insights (returns AI connection string).
   - `storage` → storage account + table service + blob deployment container (returns
     account name, table endpoint, blob endpoint).
   - `keyvault` → Key Vault + secrets (returns vault URI + secret URIs).
   - `communication` → ACS + Email Comm Service + managed domain (returns sender address;
     the ACS connection string is written into Key Vault, not emitted as an output).
   - `functionapp` → Flex Consumption plan + Function App with system-assigned identity and
     all app settings wired (returns the principalId).
   - `roleAssignments` → grants the Function App identity its roles, scoped to storage + KV.

Ordering note: role assignments are created **after** the Function App so the identity's
`principalId` is available. `azd deploy` pushes the app code after infra completes, so the
identity + roles exist before the first cold start resolves Key Vault references.

## Identity & security model

- **System-assigned managed identity** on the Function App. Role assignments:
  - **Storage Blob Data Owner** — Flex Consumption deployment container +
    `AzureWebJobsStorage` via `AzureWebJobsStorage__credential=managedidentity`.
  - **Storage Table Data Contributor** — durable alert state (`TableAlertStateService`).
  - **Key Vault Secrets User** — resolve Key Vault references.
- **Identity-based connections, no connection-string secrets in app settings:**
  - `AzureWebJobsStorage__accountName` (+ `AzureWebJobsStorage__credential=managedidentity`).
  - `AlertState__ServiceUri` = the storage account's table endpoint.
- **Secrets live only in Key Vault**, populated from **secure parameters** with empty
  defaults (sourced from environment/`azd` at provision time, never committed):
  - `Smartcar--ClientSecret`, `Smartcar--RefreshToken`, `acs-connection-string`.
  - App settings reference them via `@Microsoft.KeyVault(SecretUri=...)`.
- Key Vault uses **RBAC authorization** (no access policies).

## App settings wired by the template

| Setting | Value / source |
| --- | --- |
| `FUNCTIONS_WORKER_RUNTIME` | `dotnet-isolated` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | App Insights connection string (module output) |
| `AzureWebJobsStorage__accountName` | storage account name |
| `AzureWebJobsStorage__credential` | `managedidentity` |
| `FuelAlert__ThresholdPercent` | `25` (parameterized) |
| `FuelAlert__ResetPercent` | `40` (parameterized) |
| `FuelAlert__RecipientEmail` | parameter |
| `FuelAlert__VehicleId` | parameter |
| `FuelAlert__UseFakeFuelService` | `true` (per ticket; E2E issue flips it) |
| `AlertState__Provider` | `table` |
| `AlertState__ServiceUri` | storage table endpoint |
| `AlertState__TableName` | `FuelAlertState` |
| `Email__Provider` | `acs` |
| `Email__FromAddress` | managed-domain sender address (module output) |
| `Email__ConnectionString` | Key Vault reference to `acs-connection-string` |

## Flex Consumption specifics

- Plan: `Microsoft.Web/serverfarms`, `sku: { name: 'FC1', tier: 'FlexConsumption' }`, Linux.
- Site: `Microsoft.Web/sites`, `kind: 'functionapp,linux'`, with a `functionAppConfig` block:
  - `runtime`: `{ name: 'dotnet-isolated', version: dotnetVersion }` — `dotnetVersion`
    parameter defaults to `'10.0'` to match the project's `net10.0` target.
  - `deployment.storage`: blob container, authenticated with the system-assigned identity.
  - `scaleAndConcurrency`: `maximumInstanceCount` + `instanceMemoryMB` (parameterized).

## Known nuances (documented in README, not blockers)

1. **net10 on Flex Consumption.** `dotnetVersion` is a parameter (default `10.0`). If a
   region hasn't GA'd net10 on Flex, it is a single-parameter change. Called out in the README.
2. **`Email__ConnectionString` is not on `EmailOptions.cs` yet.** The app setting is wired so
   infra is ready for the ACS-email issue, but no C# is changed here. The README notes this is
   forward-wiring for that future work.

## azure.yaml

```yaml
name: ford-fuel-alert
services:
  fuel-alert:
    project: src/FordFuelAlert.Functions
    language: dotnet
    host: function
```

The Function App carries an `azd-service-name: fuel-alert` tag so `azd deploy` targets it.

## Parameters (`main.bicep`)

- `environmentName` (azd-provided) — drives naming + `azd-env-name` tag.
- `location` — Azure region.
- `recipientEmail`, `vehicleId` — alert config.
- `thresholdPercent` (default 25), `resetPercent` (default 40).
- `dotnetVersion` (default `'10.0'`), `maximumInstanceCount`, `instanceMemoryMB`.
- Secure params (empty defaults): `smartcarClientSecret`, `smartcarRefreshToken`,
  `acsConnectionString`.

## README changes

New "Infrastructure (Bicep / azd)" section covering: the `infra/` layout, the resource
list, how to deploy (`azd up` / `azd provision`, or `az deployment sub create`), the
secrets/Key-Vault/managed-identity model, and the two known nuances above. The existing
"Deploying to Azure" section links to it.

## Validation

- `az bicep install` then `az bicep build --file infra/main.bicep` — must compile with no
  errors. `az bicep lint` — clean.
- The live acceptance-criteria command (`azd provision --preview` /
  `az deployment sub create ... --what-if`) is documented for the operator to run against a
  real subscription; it cannot run in this session (no `azd`, no authenticated subscription).

## Acceptance criteria mapping

- [ ] Templates compile clean locally (`az bicep build` / `lint`).
- [ ] No secrets in source: all secret values come from secure params → Key Vault; app
      settings use identity-based connections or Key Vault references.
- [ ] Managed identity + Key Vault references used throughout.
- [ ] README documents the layout and the live `what-if` / `provision --preview` command.
