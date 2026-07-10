# ford-fuel-alert

A small personal utility: a timer-triggered Azure Function (.NET isolated worker) that periodically
checks a vehicle's fuel level and emails when it gets low.

> **First pass uses a *fake* fuel service.** No Smartcar or Ford APIs are integrated yet. The goal of
> this version is to prove the Function, alert logic, email logic, state handling, and tests all work
> with sample data — then leave clean extension points for a real vehicle-data provider later.

---

## What it does

On a schedule (every 2 hours by default), the function:

1. Reads the current fuel status from an `IFuelService` (currently the **fake** provider).
2. If fuel `% <= threshold` (default **25%**) **and** no alert has been sent yet → sends a "Ford fuel is low" email and records that an alert was sent.
3. Suppresses repeat emails while the alert is active (no duplicate spam every run).
4. Resets the alert once fuel rises back to `% >= reset` (default **40%**), so the next low reading can alert again.
5. Persists state between runs (alert sent flag, timestamps, last known % / range).

All decision logic lives in `FuelAlertService` (free of any Functions types) so it is easy to unit-test.
The Azure Function itself is a thin trigger.

---

## Architecture

```
Timer (every 2h)
      │
      ▼
CheckFuelLevelFunction            Functions/CheckFuelLevelFunction.cs  (thin trigger)
      │  calls EvaluateAsync()
      ▼
FuelAlertService                  Services/FuelAlertService.cs  (all decision logic, unit-tested)
   │            │            │
   │            │            └─ IAlertStateService ─► FileAlertStateService (JSON on disk, dev) / TableAlertStateService (Azure Table Storage)
   │            └─────────────── IEmailService      ─► ConsoleEmailService  (logs the email, dev) / AcsEmailService (Azure Communication Services)
   └──────────────────────────── IFuelService       ─► FakeFuelService      (config values, default) / SmartcarFuelService (live Smartcar API)
```

Everything behind an interface is swappable via dependency injection in `Program.cs`.

```
src/FordFuelAlert.Functions/
  Program.cs                  DI wiring (chooses fake vs. Smartcar, console email, file state)
  host.json
  Functions/CheckFuelLevelFunction.cs
  Abstractions/               IFuelService, IEmailService, IAlertStateService
  Models/                     FuelStatus, AlertState, EmailMessage, FuelAlertResult, *Options
  Services/                   FuelAlertService, FakeFuelService, ConsoleEmailService,
                              AcsEmailService, EmailServiceFactory,
                              FileAlertStateService, TableAlertStateService,
                              AlertStateServiceFactory, SmartcarFuelService,
                              TableSmartcarTokenStore, SmartcarTokenStoreFactory
tests/FordFuelAlert.Tests/    xUnit tests for the alert logic and the fake fuel service
```

---

## Why a fake fuel service first?

Integrating a real vehicle-data provider (Smartcar / official Ford APIs) requires OAuth onboarding,
vehicle-owner authorization, and live credentials. By starting with a fake provider we can build and
*prove* the parts that are entirely ours — scheduling, the alert/reset state machine, email
formatting, persistence, and tests — without waiting on any of that. The real provider then slots in
behind the existing `IFuelService` interface with no changes to the alert logic.

---

## Running locally

### Prerequisites

- **.NET SDK** — this repo targets `net10.0` (isolated worker).
- **Azure Functions Core Tools v4** — `npm install -g azure-functions-core-tools@4`
- **Azurite** (storage emulator; timer triggers need a storage backend) — `npm install -g azurite`

### Steps

```bash
# 1. Create your local settings from the template (this file is gitignored)
cp src/FordFuelAlert.Functions/local.settings.sample.json src/FordFuelAlert.Functions/local.settings.json

# 2. Start the storage emulator (in its own terminal)
azurite --silent --location ./.azurite

# 3. Build + run the function host (from the function project folder)
cd src/FordFuelAlert.Functions
func start
```

The timer fires every 2 hours. To trigger it immediately without waiting, call the admin endpoint:

```bash
curl -X POST http://localhost:7071/admin/functions/CheckFuelLevelFunction \
  -H "Content-Type: application/json" -d '{"input":""}'
```

With the sample settings (fake fuel at 20%, threshold 25%) you'll see the alert fire and the fake
email logged to the console. Trigger it again and it won't re-send (dedup), because state was saved.

### Tests

```bash
dotnet test
```

---

## Changing the fake fuel values for testing

Edit `FakeFuel__PercentRemaining` (and optionally `FakeFuel__RangeMiles`) in `local.settings.json`,
then restart `func start`. To re-arm the alert between experiments, delete the saved state file
(see `AlertState__FilePath`, defaults under your local app-data folder).

| Set `FakeFuel__PercentRemaining` | Prior state        | Expected behavior                         |
| -------------------------------- | ------------------ | ----------------------------------------- |
| `50`                             | no alert sent      | No alert (above threshold)                |
| `20`                             | no alert sent      | **Sends** alert, saves `AlertSent=true`   |
| `20`                             | alert already sent | No email (duplicate suppressed)           |
| `45`                             | alert already sent | Resets alert state (>= reset %)           |
| *(empty / unset)*                | any                | Treated as "data unavailable" — no email  |

---

## Settings

Set via `local.settings.json` locally and **Azure App Settings / Key Vault** in the cloud.
Names use the double-underscore convention that binds to options sections.

| Setting                          | Default          | Purpose                                                        |
| -------------------------------- | ---------------- | -------------------------------------------------------------- |
| `FuelAlert__ThresholdPercent`    | `25`             | Send an alert when fuel `% <=` this value.                     |
| `FuelAlert__ResetPercent`        | `40`             | Clear the alert when fuel `% >=` this value.                   |
| `FuelAlert__RecipientEmail`      | *(none)*         | Where the alert email is addressed.                            |
| `FuelAlert__VehicleId`           | *(none)*         | Identifier for the vehicle being checked.                      |
| `FuelAlert__UseFakeFuelService`  | `true`           | `true` = fake provider; `false` routes to the real Smartcar provider. |
| `FakeFuel__PercentRemaining`     | *(none)*         | Fake fuel %. Empty = simulate "unavailable".                   |
| `FakeFuel__RangeMiles`           | *(none)*         | Fake estimated range in miles (optional).                      |
| `AlertState__Provider`           | `file`           | `file` = local JSON (dev only); `table` = Azure Table Storage. |
| `AlertState__FilePath`           | local app-data   | `file` provider: where the JSON state is stored.               |
| `AlertState__ConnectionString`   | *(none)*         | `table` provider: storage connection string. **Secret.**       |
| `AlertState__ServiceUri`         | *(none)*         | `table` provider: table endpoint for managed identity (preferred over the connection string in Azure). |
| `AlertState__TableName`          | `FuelAlertState` | `table` provider: table name.                                  |
| `Smartcar__ClientId`             | *(none)*         | Smartcar OAuth client id (required when `UseFakeFuelService=false`). |
| `Smartcar__ClientSecret`         | *(none)*         | Smartcar OAuth client secret. **Secret.**                      |
| `Smartcar__RefreshToken`         | *(none)*         | Bootstrap refresh token from onboarding. **Secret.** Seeds an empty token store; thereafter the rotated token lives in Table Storage. |
| `Smartcar__VehicleId`            | *(none)*         | Smartcar vehicle id (UUID) — the `{id}` in `GET /vehicles/{id}/fuel`. |
| `Smartcar__ConnectionString`     | *(none)*         | Refresh-token store: storage connection string. **Secret.**    |
| `Smartcar__ServiceUri`           | *(none)*         | Refresh-token store: table endpoint for managed identity (preferred over the connection string in Azure). |
| `Smartcar__TableName`            | `SmartcarTokens` | Refresh-token store: table name.                               |
| `Email__Provider`                | `console`        | `console` = logs the email (dev default); `acs` = Azure Communication Services. |
| `Email__FromAddress`             | *(none)*         | "From" address. For `acs`, a verified sender on your provisioned domain. Required when `Provider=acs`. |
| `Email__AcsConnectionString`     | *(none)*         | `acs` provider: connection string (endpoint + access key). **Secret.**  |
| `Email__AcsEndpoint`             | *(none)*         | `acs` provider: resource endpoint for managed identity (preferred over the connection string in Azure). |

`AzureWebJobsStorage` and `FUNCTIONS_WORKER_RUNTIME` (`dotnet-isolated`) are required by the host.

---

## How the alert + reset logic works

The logic uses **hysteresis** so a vehicle hovering near the threshold doesn't flap between alerting
and not-alerting:

- **Send** when `percent <= ThresholdPercent` **and** `AlertSent == false`. Then set `AlertSent = true`.
- **Suppress** any further emails while `AlertSent == true` (even on repeated low readings).
- **Reset** (`AlertSent = false`) only when `percent >= ResetPercent`. No email is sent on reset.
- Between threshold and reset (e.g. 25–40%), nothing changes — an active alert stays active.
- If fuel data is unavailable (null %), the run logs a warning, updates "last checked", and does nothing else.

Each run also records `LastCheckedUtc`, `LastKnownPercent`, and `LastKnownRangeMiles`.

The alert email subject is **"Ford fuel is low"**; the body includes the fuel percentage, estimated
range (if available), and the time the reading was taken.

---

## Smartcar / Ford authorization

`SmartcarFuelService` reads the real vehicle's fuel level via the [Smartcar API](https://smartcar.com/docs)
and is selected by setting `FuelAlert__UseFakeFuelService=false`. It is unit-tested with a mocked
HTTP handler; live cutover happens in a later E2E issue. Notes for operating it:

- Use **Smartcar Connect (OAuth)**: the vehicle owner explicitly authorizes access (a separate
  one-time onboarding step), after which you receive a refresh token. Store `Smartcar__*` secrets in
  Key Vault, never in source.
- The service exchanges the refresh token for an access token at
  `https://auth.smartcar.com/oauth/token`, then calls `GET /vehicles/{id}/fuel`. Smartcar returns
  `percentRemaining` as a 0–1 decimal, which is scaled to the `FuelStatus` 0–100 contract; `range` is
  requested in imperial units (miles).
- **Refresh tokens rotate on every exchange.** The rotated token is persisted durably in Azure Table
  Storage (`Smartcar__ConnectionString`/`ServiceUri`, `Smartcar__TableName`); `Smartcar__RefreshToken`
  only seeds an empty store on first run.
- Rate limits, an asleep vehicle, API errors, or unparseable responses degrade to "data unavailable"
  (a logged warning + `null`) rather than throwing — the alert logic already treats `null` as a no-op.
- **Do not** guess undocumented API details — follow the official Smartcar (or Ford developer) docs.
- **Do not** use unofficial FordPass scraping, and **never** store Ford account usernames/passwords.

---

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

- **`dotnetVersion` defaults to `10.0`** to match this project's `net10.0` target. The runtime
  version and the app's target framework must move together: a `net10.0` build will not load on a
  .NET 9 Flex runtime. If your region has not yet enabled .NET 10 on Flex Consumption, prefer a
  region that has; otherwise retarget the project to `net9.0` **and** pass
  `--parameters dotnetVersion=9.0` until .NET 10 is available.
- Compile the templates locally with `az bicep build --file infra/main.bicep`.

---

## Deploying to Azure

> Prefer the Bicep templates in [`infra/`](infra/) — see [Infrastructure (Bicep / azd)](#infrastructure-bicep--azd). The steps below are the manual equivalent.

1. Create a **Function App** on the .NET **isolated** stack (matching this project's target framework).
2. Configure **Application Settings** with the same keys as above; put secrets in **Key Vault** and
   reference them from App Settings.
3. Replace the dev-only pieces with durable equivalents:
   - Set `AlertState__Provider=table` so state lives in Azure Table Storage (`TableAlertStateService`).
     The Functions file system is ephemeral, so the default file-based state is **local development only**.
     Prefer `AlertState__ServiceUri` + managed identity (grant the app **Storage Table Data Contributor**)
     over a connection string.
   - Set `Email__Provider=acs` (with `Email__FromAddress` and either `Email__AcsConnectionString` or
     `Email__AcsEndpoint` + managed identity) so alerts send via Azure Communication Services instead of
     just logging to the console.
   - `FakeFuelService` → `SmartcarFuelService` once implemented.
4. Ensure `AzureWebJobsStorage` points at a real storage account.
5. Adjust the schedule in `CheckFuelLevelFunction` if you want a cadence other than every 2 hours.

---

## Security notes

- **Vehicle-owner authorization is required.** Only access vehicle data for a vehicle whose owner has
  explicitly authorized it through the provider's official OAuth flow.
- **No secrets in source control.** `local.settings.json` is gitignored; commit only
  `local.settings.sample.json` with placeholder values. Use Key Vault for cloud secrets.
- **No credential storage / scraping.** Never store Ford account credentials and never scrape FordPass.
- **Logs never print secrets.** The code logs only whether a credential is configured, never its value.
