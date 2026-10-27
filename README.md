# ford-fuel-alert

A small personal utility: a timer-triggered Azure Function (.NET isolated worker) that periodically
checks a vehicle's fuel level and emails when it gets low.

> **The fake fuel service is the default, but the real providers are implemented.**
> `FordConnectFuelService` (Ford's FordConnect Query API), `SmartcarFuelService` (live Smartcar API),
> `AcsEmailService` (Azure Communication Services email), and `TableAlertStateService` (Azure Table
> Storage) all exist and are opt-in via configuration. The fake provider stays the default
> (`Fuel__Provider=fake`) until the live E2E cutover.

---

## What it does

On a schedule (every 2 hours by default), the function:

1. Reads the current fuel status from an `IFuelService` (the **fake** provider by default).
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
   └──────────────────────────── IFuelService       ─► FakeFuelService      (config values, default) / FordConnectFuelService (Ford API) / SmartcarFuelService (Smartcar API)
```

Everything behind an interface is swappable via dependency injection in `Program.cs`.

```
src/FordFuelAlert.Functions/
  Program.cs                  DI wiring (selects the fuel, email, and state providers via configuration)
  host.json
  Functions/CheckFuelLevelFunction.cs
  Abstractions/               IFuelService, IEmailService, IAlertStateService
  Models/                     FuelStatus, AlertState, EmailMessage, FuelAlertResult, *Options
  Services/                   FuelAlertService, FakeFuelService, FordConnectFuelService,
                              FordConnectTokenStore, SmartcarFuelService, FuelServiceFactory,
                              ConsoleEmailService, AcsEmailService, EmailServiceFactory,
                              FileAlertStateService, TableAlertStateService,
                              AlertStateServiceFactory
tests/FordFuelAlert.Tests/    xUnit tests for the alert logic, the providers and the factories
```

---

## Why a fake fuel service first?

Integrating a real vehicle-data provider (Smartcar / official Ford APIs) requires OAuth onboarding,
vehicle-owner authorization, and live credentials. By starting with a fake provider we can build and
*prove* the parts that are entirely ours — scheduling, the alert/reset state machine, email
formatting, persistence, and tests — without waiting on any of that. The real FordConnect Query and
Smartcar providers have since been implemented behind that same `IFuelService` interface — with no
changes to the alert logic — and are selected with `Fuel__Provider=fordconnect` or
`Fuel__Provider=smartcar` (dispatched by `FuelServiceFactory`).

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

The timer fires every 2 hours (the `FuelAlertSchedule` value in the sample settings). To trigger it immediately without waiting, call the admin endpoint:

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
| `FuelAlertSchedule`              | *(none, required)* | NCRONTAB schedule for the fuel check; the sample settings and Bicep set `0 0 */2 * * *` (every 2 hours). Required: the function does not load without it. |
| `FuelAlert__ThresholdPercent`    | `25`             | Send an alert when fuel `% <=` this value.                     |
| `FuelAlert__ResetPercent`        | `40`             | Clear the alert when fuel `% >=` this value.                   |
| `FuelAlert__RecipientEmail`      | *(none)*         | Where the alert email is addressed.                            |
| `FuelAlert__VehicleId`           | *(none)*         | Identifier for the vehicle being checked.                      |
| `Fuel__Provider`                 | `fake`           | `fake` = sample data; `smartcar` = Smartcar Vehicle API v3; `fordconnect` = Ford's FordConnect Query API. |
| `FakeFuel__PercentRemaining`     | *(none)*         | Fake fuel %. Empty = simulate "unavailable".                   |
| `FakeFuel__RangeMiles`           | *(none)*         | Fake estimated range in miles (optional).                      |
| `AlertState__Provider`           | `file`           | `file` = local JSON (dev only); `table` = Azure Table Storage. |
| `AlertState__FilePath`           | local app-data   | `file` provider: where the JSON state is stored.               |
| `AlertState__ConnectionString`   | *(none)*         | `table` provider: storage connection string. **Secret.**       |
| `AlertState__ServiceUri`         | *(none)*         | `table` provider: table endpoint for managed identity (preferred over the connection string in Azure). |
| `AlertState__TableName`          | `FuelAlertState` | `table` provider: table name.                                  |
| `Smartcar__ClientId`             | *(none)*         | Smartcar API client id, from the Dashboard's API Credentials tab (required when `Fuel__Provider=smartcar`). |
| `Smartcar__ClientSecret`         | *(none)*         | Smartcar API client secret. **Secret.**                        |
| `Smartcar__UserId`               | *(none)*         | Smartcar user id (UUID) of the vehicle owner, returned as `user_id` on the Connect redirect. Sent as the `sc-user-id` header. Required. |
| `Smartcar__VehicleId`            | *(none)*         | Smartcar vehicle id — the `{vehicleId}` in `GET /vehicles/{vehicleId}/signals/...`. Optional: when empty, the vehicle is looked up from the user's connection (must be exactly one) and logged so it can be pinned. |
| `FordConnect__ClientId`          | *(none)*         | OAuth client id of the app registered on developer.ford.com (NA portal). Required when `Fuel__Provider=fordconnect`. |
| `FordConnect__ClientSecret`      | *(none)*         | OAuth client secret from the same registration; expires on the date shown in the portal (~2 years). **Secret.** |
| `FordConnect__RefreshToken`      | *(none)*         | Seed refresh token from the one-time owner consent. Used only until the token store holds a rotated token. **Secret.** |
| `FordConnect__Vin`               | *(none)*         | VIN of the consented vehicle, reported as `FuelStatus.VehicleId`. Optional: when empty it is read once from `GET /garage`. |
| `FordConnect__MaxDataAgeHours`   | `48`             | Age of the `fuelLevel.updateTime` beyond which a staleness warning is logged. The reading is still used. |
| `FordConnect__TokenStoreConnectionString` | *(none)* | Table Storage connection string for the rotated refresh token (local Azurite: `UseDevelopmentStorage=true`). **Secret.** |
| `FordConnect__TokenStoreServiceUri` | *(none)*      | Table endpoint for managed identity (preferred over the connection string in Azure). One of the two is required when `Fuel__Provider=fordconnect`. |
| `FordConnect__TokenStoreTableName` | `FordConnectTokens` | Table holding the single refresh-token row.                |
| `Email__Provider`                | `console`        | `console` = logs the email (dev default); `acs` = Azure Communication Services. |
| `Email__FromAddress`             | *(none)*         | "From" address. For `acs`, a verified sender on your provisioned domain. Required when `Provider=acs`. |
| `Email__AcsConnectionString`     | *(none)*         | `acs` provider: connection string (endpoint + access key). **Secret.**  |
| `Email__AcsEndpoint`             | *(none)*         | `acs` provider: resource endpoint for managed identity (preferred over the connection string in Azure). |

`AzureWebJobsStorage` and `FUNCTIONS_WORKER_RUNTIME` (`dotnet-isolated`) are required by the host.
`FuelAlertSchedule` is a flat name, not under `FuelAlert__`, because the `%FuelAlertSchedule%` timer
binding resolves a flat app-setting name.

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

## FordConnect Query (Ford's official API)

`FordConnectFuelService` reads fuel level directly from Ford's
[FordConnect Query API](https://developer.ford.com) and is selected with `Fuel__Provider=fordconnect`.
Ford reopened this API for personal use in North America in January 2026 (see Ford's developer
changelog); registration is free. It is unit-tested with a mocked HTTP handler. Notes for operating it:

- **One-time owner consent (manual).** Register an app on `developer.ford.com/developer-na` as a
  personal-use app with the `Vehicle Data` category, wait for Ford to activate it (hours), then run
  the OAuth 2.0 authorization-code flow in a browser once per vehicle and exchange the code at
  Ford's token endpoint. Keep the `refresh_token` from that exchange: it seeds
  `FordConnect__RefreshToken`. The consent page selects one vehicle, so each vehicle needs its own
  consent and token. Step-by-step instructions are in
  [Registering your own FordConnect app](#registering-your-own-fordconnect-app).
- **Per run** the provider refreshes the access token at
  `https://api.vehicle.ford.com/dah2vb2cprod.onmicrosoft.com/oauth2/v2.0/token?p=B2C_1A_FCON_AUTHORIZE`
  (`grant_type=refresh_token`, cached in memory until it nears expiry), reads
  `GET https://api.vehicle.ford.com/fcon-query/v1/telemetry` with the bearer token, and reads
  `GET .../garage` once to learn the VIN when `FordConnect__Vin` is empty.
- **Durable refresh token.** Ford may rotate the refresh token on every refresh, so the latest one is
  written to the `FordConnectTokens` table (`FordConnectTokenStore`) together with the consent time
  from the `id_token`'s `auth_time` claim. The seed in `FordConnect__RefreshToken` is used only when
  that row does not exist yet.
- **Annual re-consent.** Ford stops issuing refresh tokens 365 days after the owner's consent, with
  no exceptions. The provider logs a warning in the last 30 days. When Ford answers `invalid_grant`
  for the stored token, the provider tries the `FordConnect__RefreshToken` seed once (if it differs)
  and, on success, overwrites the stored row; if the seed is rejected too, the run **throws** (visible
  failure) instead of returning `null` every two hours for the rest of the year. Recovery is therefore:
  redo the browser consent, put the new refresh token in the seed secret, and the next run heals
  itself. The client secret also expires (~2 years).
- **Mapping.** `metrics.fuelLevel.value` is a percentage but Ford reports values above 100 after a
  fill-up, so it is clamped to 0–100. `metrics.fuelRange.value` is kilometres, converted to miles
  (km × 0.621371). `RetrievedAtUtc` is the fuel metric's own `updateTime` (falling back to the root
  `updateTime`, then the clock) because Query returns cloud-cached values that can be older than other
  metrics; readings older than `FordConnect__MaxDataAgeHours` are still used but logged.
- Missing or non-numeric `fuelLevel`, HTTP 429/5xx, timeouts, and unparseable responses degrade to
  "data unavailable" (a logged warning + `null`). Only `invalid_grant` on the token refresh and a
  missing refresh token throw.
- Ford's terms license personal, non-public development use with your own vehicles only, and reserve
  the right to change the API without notice.

### Registering your own FordConnect app

All of the following is manual and happens once per deployment. None of it can be scripted: Ford
gates registration behind a signed-in portal and the consent behind a browser login.

**1. Create a Ford developer account** at [developer.ford.com](https://developer.ford.com) and accept
the Developer Marketplace T&C. The dialog's "I agree" button stays `disabled` until all three policy
links inside it have been opened — opening them is what enables it, and reloading the page resets it.

**2. Register the application** at `developer.ford.com/developer-na`:

| Field | Value |
| --- | --- |
| Is this app for personal use? | Yes (locked on for personal accounts) |
| Application Name | anything, e.g. `Fuel Alert` |
| Application Description | free text, max 500 characters |
| Purpose of Usage | free text, **max 100 characters** |
| Which device does your application service? | `Gas Vehicles` for an ICE or hybrid Ford |
| Allowed Redirect URI(s) | `http://localhost` |

Neither length limit is shown in the portal and neither text field accepts apostrophes. An over-long
or apostrophed value simply leaves "Continue" disabled with no message explaining why.

The redirect URI is used only by the one-time consent in step 4 and never by the Function, which
authenticates with `grant_type=refresh_token` and sends no redirect URI at all. It must not carry a
port — `http://localhost:8080` is rejected at registration — and nothing needs to listen on it: the
browser is redirected to an unreachable address and the authorization code is read from the address
bar.

**3. Select the data categories.** `Vehicle Basic Info` is mandatory and supplies the VIN that
`GET /garage` returns. Add **`Vehicle Data`** and nothing else: Ford's
[data dictionary](https://developer.ford.com/fordconnect-data-dictionary) lists `FUEL_LEVEL` and
`FUEL_RANGE` under that category alone. `Trip Data` is a decoy — it holds `TRIP_FUEL_CONSUMED` and
`TRIP_FUEL_ECONOMY`, which are per-journey totals rather than tank level.

Submit, then generate a credential to obtain the Client ID and Secret. Ford needs **at least two
hours** to deploy the app globally; running the consent before that completes fails at the Consent
button.

**4. Run the one-time owner consent.** The consent page selects a single vehicle, so repeat this per
vehicle. Open this URL in a browser, substituting your client id:

```
https://api.vehicle.ford.com/fcon-public/v1/auth/init?client_id=CLIENT_ID&response_type=code&redirect_uri=http://localhost&scope=openid%20offline_access&state=xyz123
```

Sign in as the vehicle's primary owner, select the vehicle, scroll to the bottom and press Consent.
The browser then fails to load `http://localhost/?code=...` — that is expected. Copy the `code`
value out of the address bar.

**5. Exchange the code for a refresh token** (the code is single-use and expires in minutes).
`--data-urlencode` is required rather than plain `-d`, which performs no form encoding: a client
secret or code containing `+`, `&` or `%` would otherwise reach Ford altered and be rejected.

```bash
curl -s -X POST "https://api.vehicle.ford.com/dah2vb2cprod.onmicrosoft.com/oauth2/v2.0/token?p=B2C_1A_FCON_AUTHORIZE" \
  --data-urlencode grant_type=authorization_code \
  --data-urlencode client_id="$FORDCONNECT_CLIENT_ID" \
  --data-urlencode client_secret="$FORDCONNECT_CLIENT_SECRET" \
  --data-urlencode redirect_uri=http://localhost \
  --data-urlencode code="$FORDCONNECT_AUTH_CODE"
```

Keep `refresh_token` from the response — that is `FordConnect__RefreshToken`. The `id_token`'s
`auth_time` claim starts the 365-day re-consent clock; calendar it alongside the client secret's
expiry date shown in the portal.

**6. Configure the app.** Locally, put the values in `local.settings.json` (gitignored) and set
`Fuel__Provider=fordconnect`. For Azure the two secrets must never be committed — they reach Key
Vault through environment variables consumed by `infra/main.parameters.json`:

```bash
azd env set FORDCONNECT_CLIENT_ID <client id>
azd env set FORDCONNECT_CLIENT_SECRET <client secret>
azd env set FORDCONNECT_REFRESH_TOKEN <refresh token>
azd env set FORDCONNECT_VIN <vin>
azd env set FUEL_PROVIDER fordconnect
```

`FORDCONNECT_VIN` is optional; when empty the VIN is read once from `GET /garage`. Only the client
secret and refresh token become Key Vault secrets (`fordconnect-client-secret`,
`fordconnect-refresh-token`); the client id and VIN are plain app settings.

### Troubleshooting the consent and the exchange

**The consent URL's `scope` is ignored.** `auth/init` discards whatever scope is passed and always
sends `<ford-client-id> openid` to `login.ford.com`. It is kept in the URL above only because the
community integrations send it. Nothing about scope at the authorize step can affect the outcome.

**A successful consent does not prove the registration is correct.** `auth/init` checks only that the
client id exists in Ford's API-gateway registry; probes confirm it accepts an unregistered
`redirect_uri` and a `response_type` of `token` and still issues a 302. A code therefore proves the
credential record exists and that you are a valid vehicle owner — not that the client secret, the
registered redirect URI, or the app's provisioning in the `dah2vb2cprod` B2C tenant are right.

**Two B2C tenants are involved.** Consent runs on `login.ford.com` (tenant `4566605f-…`); redemption
runs on `dah2vb2cprod`, whose issuer is `914d88b1-…`. The canonical token endpoint, from that
tenant's `.well-known/openid-configuration`, is
`https://dah2vb2cprod.b2clogin.com/dah2vb2cprod.onmicrosoft.com/b2c_1a_fcon_authorize/oauth2/v2.0/token`.
The `api.vehicle.ford.com/dah2vb2cprod.onmicrosoft.com/oauth2/v2.0/token?p=B2C_1A_FCON_AUTHORIZE`
URL used by `FordConnectFuelService` and by the community integrations is a proxy in front of it and
returns byte-identical responses.

**`invalid_grant` / `AADB2C90085` on the code exchange.** Ford returns this generic "internal error"
for several unrelated causes, so it carries little information on its own. Confirmed by testing:

- Failed redemptions do **not** consume the authorization code, so every variant can be batched onto
  one code rather than spending a consent per attempt.
- The error is unchanged across `redirect_uri=http://localhost`, the `fma-callback` URL, with and
  without `scope` — so a uniform error does not imply the parameters are correct.
- A junk-`refresh_token` probe cannot test the client secret: omitting the secret, sending it in the
  body and sending it as HTTP Basic all return the same `AADB2C90090`, because the refresh token is
  parsed before client authentication.

The two recorded occurrences of this error against this endpoint in the community trackers were both
credential problems, resolved by deleting the OAuth credential in the portal and generating a new
one. Try that first. If it persists, the remaining candidates are the app not yet being provisioned
in the B2C tenant (registration can take days, not the advertised two hours) and a Ford-side fault;
both are resolved by waiting rather than by changing anything here.

On the next consent, capture the full redirect chain with "preserve log" enabled. The second
authorize request — to `dah2vb2cprod.b2clogin.com/.../oauth2/v2.0/authorize` — shows the
`redirect_uri`, `scope` and any `code_challenge` that the exchange has to mirror. Keep the full error
body too: the `Correlation ID` in `error_description` is what Ford developer support needs.

## Smartcar / Ford authorization

`SmartcarFuelService` reads the real vehicle's fuel level via the
[Smartcar Vehicle API v3](https://smartcar.com/docs/api-reference/vehicles-api-intro) and is selected
by setting `Fuel__Provider=smartcar`. (Smartcar retires API v2.0 on December 1, 2026.)
It is unit-tested with a mocked HTTP handler. Notes for operating it:

- Use **Smartcar Connect (OAuth)**: the vehicle owner explicitly authorizes access once (requesting
  the `read_fuel` permission). The Connect redirect returns a `user_id`; set it as `Smartcar__UserId`.
  Store `Smartcar__ClientSecret` in Key Vault, never in source.
- Each run gets an application token with the client credentials grant at
  `https://iam.smartcar.com/oauth2/token` (valid 1 hour, cached in memory; there is no refresh token
  to persist), then reads the `internalcombustionengine-fuellevel` and `internalcombustionengine-range`
  signals from `https://vehicle.api.smartcar.com/v3/vehicles/{vehicleId}/signals/{code}` with the
  `sc-user-id` header.
- Fuel level is already 0–100 (`unit: percent`). Range carries its own unit and is converted to
  miles (km × 0.621371); an unknown unit, or a failed range read, just omits the range.
- A signal status other than `SUCCESS` (`UNAVAILABLE`, `ERROR`) is treated as unavailable data.
- If `Smartcar__VehicleId` is empty, the vehicle id is looked up once from
  `GET /v3/connections?filter[userId]=...` and logged; set it explicitly if the user has more than one vehicle.
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
- **Key Vault** (RBAC) holding the ACS connection string and, when supplied, the Smartcar client secret.
- **Application Insights** (backed by a Log Analytics workspace) for the timer function's logs.

### Security model

- **Identity, not secrets, for connections:** host storage uses `AzureWebJobsStorage__accountName`
  and alert state uses `AlertState__ServiceUri` — both resolved by the managed identity. Role grants:
  Storage Blob Data Owner, Storage Table Data Contributor, Key Vault Secrets User.
- **Secrets live only in Key Vault**, referenced from app settings via `@Microsoft.KeyVault(SecretUri=…)`.
  Secret values come from secure parameters (Smartcar) or the ACS resource's runtime keys — never
  from source control. `main.parameters.json` uses `${ENV_VAR}` substitution only.

### App settings wired by the template

`APPLICATIONINSIGHTS_CONNECTION_STRING`, `AzureWebJobsStorage__accountName`, `FuelAlertSchedule`,
`FuelAlert__*`, `AlertState__Provider=table` + `AlertState__ServiceUri` + `AlertState__TableName`,
`Email__Provider=acs` + `Email__FromAddress`, and a Key Vault reference for
`Email__AcsConnectionString`.

- `Fuel__Provider` comes from the `fuelProvider` parameter (default `fake`; `smartcar` or
  `fordconnect` for live reads). `FordConnect__TokenStoreServiceUri` is always set to the storage
  account's table endpoint (used only by the `fordconnect` provider).
- `FordConnect__ClientId` and `FordConnect__Vin` come from the `fordConnectClientId` and
  `fordConnectVin` parameters and are emitted only when non-empty.
- `FordConnect__ClientSecret` and `FordConnect__RefreshToken` are Key Vault references, created only
  when the secure `fordConnectClientSecret` / `fordConnectRefreshToken` parameters are supplied.
- `Smartcar__ClientId`, `Smartcar__UserId`, `Smartcar__VehicleId`, `FakeFuel__PercentRemaining`,
  and `FakeFuel__RangeMiles` come from the `smartcarClientId`, `smartcarUserId`,
  `smartcarVehicleId`, `fakeFuelPercentRemaining`, and `fakeFuelRangeMiles` parameters and are
  emitted only when the matching parameter is non-empty. `fakeFuelPercentRemaining` and
  `fakeFuelRangeMiles` must be numeric; the template does not validate them, and a non-numeric
  value fails at app startup.
- `Smartcar__ClientSecret` is a Key Vault reference, created only when the secure
  `smartcarClientSecret` parameter is supplied.

`siteConfig.appSettings` is authoritative: a template deployment replaces the Function App's whole
app-settings set, so a setting added by hand in the portal is removed on the next
`az deployment` / `azd provision`. Production values must go through template parameters.

> On **Flex Consumption**, the worker runtime is set through `functionAppConfig.runtime`
> (`dotnet-isolated`), which replaces the classic `FUNCTIONS_WORKER_RUNTIME` app setting.

### Deploy

With `azd` (recommended):

```bash
azd env new ffa-dev
azd env set FUEL_ALERT_RECIPIENT_EMAIL you@example.com
azd env set FUEL_ALERT_VEHICLE_ID demo-vehicle
azd env set SMARTCAR_CLIENT_ID <client-id>
azd env set SMARTCAR_USER_ID <smartcar-user-id>
azd env set SMARTCAR_VEHICLE_ID <smartcar-vehicle-id>   # optional
# azd env set FUEL_PROVIDER fordconnect     # optional cutover: live reads (or smartcar)
azd provision --preview   # what-if
azd up                    # provision + deploy code
```

`FUEL_ALERT_VEHICLE_ID` is the app's own label for the vehicle (used as the alert-state row key),
while `SMARTCAR_VEHICLE_ID` is Smartcar's id for it. `infra/main.parameters.json` also maps
`SMARTCAR_CLIENT_SECRET`, `FAKE_FUEL_PERCENT_REMAINING`, and `FAKE_FUEL_RANGE_MILES` to the
`smartcarClientSecret`, `fakeFuelPercentRemaining`, and `fakeFuelRangeMiles` parameters; leave any
of them unset to omit the matching setting. `FUEL_PROVIDER` maps to `fuelProvider`
and defaults to `true` when unset. azd remembers every `azd env set` value per environment, so after
`azd env set FUEL_PROVIDER fordconnect` later `azd provision` / `azd up` runs keep the live
provider.

Or with plain `az` (subscription-scoped):

```bash
az deployment sub create \
  --location eastus2 \
  --template-file infra/main.bicep \
  --parameters environmentName=ffa-dev location=eastus2 \
               recipientEmail=you@example.com vehicleId=demo-vehicle \
  --what-if     # drop --what-if to actually deploy
```

Production configuration (issue #8), with the Smartcar settings in place but the fake provider
still selected. The client secret comes from an environment variable, never a literal:

```bash
az deployment sub create \
  --location eastus2 \
  --template-file infra/main.bicep \
  --parameters environmentName=ffa-prod location=eastus2 \
               recipientEmail=you@example.com vehicleId=demo-vehicle \
               smartcarClientId=<client-id> smartcarUserId=<smartcar-user-id> \
               smartcarClientSecret="$SMARTCAR_CLIENT_SECRET" \
               fuelProvider=fake \
  --what-if     # drop --what-if to actually deploy
```

The cutover to live data is the same command with `fuelProvider=fordconnect` (plus the
`fordConnect*` parameters) or `fuelProvider=smartcar` (plus the `smartcar*` parameters).

> **Warning:** with plain `az`, `fuelProvider` is a command-line parameter and `az` has no
> memory, so every redeploy must repeat `fuelProvider=fordconnect`, or the app moves back to the
> fake provider. Redeploy against your **existing** environment: the same `environmentName` and
> `location` you provisioned with (a different pair creates a separate resource group and app),
> plus every parameter you had set, including `smartcarVehicleId` if you pinned one. An omitted
> optional parameter falls back to its default and its app setting is removed. With `azd`, set
> `FUEL_PROVIDER=fordconnect` once in the environment instead.

### Continuous deployment (GitHub Actions)

Code deploys automatically: every push to `main` runs [CI](.github/workflows/ci.yml), and when it
succeeds, [Deploy](.github/workflows/deploy.yml) publishes the Functions project and deploys it to
the Function App (it checks out the exact commit CI validated). It can also be run manually from the
Actions tab (`workflow_dispatch`).

Auth is **OIDC federated credentials** — no publish profiles or long-lived secrets in the repo. The
workflow logs in with `azure/login` using these repository **secrets**:

| Secret                  | Value                                   |
| ----------------------- | --------------------------------------- |
| `AZURE_CLIENT_ID`       | App registration used by GitHub Actions |
| `AZURE_TENANT_ID`       | Entra tenant                            |
| `AZURE_SUBSCRIPTION_ID` | Target subscription                     |

These are identifiers rather than credentials — access is granted by the federated credential, not by
knowing them — but they are stored as secrets so Actions masks them in run logs, which are world-readable
on a public repository.

Plus one repository **variable**, which is already public as the app's hostname:

| Variable                  | Value                                        |
| ------------------------- | -------------------------------------------- |
| `AZURE_FUNCTION_APP_NAME` | Function App created by the Bicep deployment |

One-time setup (already done for this repo — recorded here so it's reproducible):

```bash
# App registration + service principal for GitHub Actions
appId=$(az ad app create --display-name github-ford-fuel-alert-deploy --query appId -o tsv)
az ad sp create --id "$appId"

# Trust GitHub Actions tokens for the main branch of this repo
az ad app federated-credential create --id "$appId" --parameters '{
  "name": "github-main-branch",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:MattFromRVA/ford-fuel-alert:ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'

# Least scope: Website Contributor on the Function App only (the workflow publishes, never provisions)
az role assignment create --assignee "$appId" --role "Website Contributor" \
  --scope "/subscriptions/<sub-id>/resourceGroups/<rg-name>/providers/Microsoft.Web/sites/<function-app-name>"

# Expose the IDs to the workflow (secrets so they are masked in public run logs)
gh secret set AZURE_CLIENT_ID --body "$appId"
gh secret set AZURE_TENANT_ID --body "$(az account show --query tenantId -o tsv)"
gh secret set AZURE_SUBSCRIPTION_ID --body "$(az account show --query id -o tsv)"
gh variable set AZURE_FUNCTION_APP_NAME --body "<function-app-name>"
```

Because the deployment identity is scoped to the Function App, it cannot create or change other
resources. Run `azd provision` / `azd up` locally with an account that has rights on the resource
group; the workflow only publishes code.

### Notes

- **`dotnetVersion` defaults to `10.0`** to match this project's `net10.0` target. The runtime
  version and the app's target framework must move together: a `net10.0` build will not load on a
  .NET 9 Flex runtime. If your region has not yet enabled .NET 10 on Flex Consumption, prefer a
  region that has; otherwise retarget the project to `net9.0` **and** pass
  `--parameters dotnetVersion=9.0` until .NET 10 is available.
- **`schedule` defaults to `0 0 */2 * * *`** (every 2 hours) and is emitted as the
  `FuelAlertSchedule` app setting. Pass `--parameters schedule="0 0 */4 * * *"` (or edit the app
  setting) to change the cadence without a code change.
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
   - Set `Fuel__Provider=fordconnect` (plus the `FordConnect__*` settings) or `Fuel__Provider=smartcar`
     (plus the `Smartcar__*` settings) to switch from `FakeFuelService` to a live provider.
4. Ensure `AzureWebJobsStorage` points at a real storage account.
5. Set `FuelAlertSchedule` (NCRONTAB) in App Settings; it is required, and `0 0 */2 * * *` matches the default every-2-hours cadence.

---

## Security notes

- **Vehicle-owner authorization is required.** Only access vehicle data for a vehicle whose owner has
  explicitly authorized it through the provider's official OAuth flow.
- **No secrets in source control.** `local.settings.json` is gitignored; commit only
  `local.settings.sample.json` with placeholder values. Use Key Vault for cloud secrets.
- **No credential storage / scraping.** Never store Ford account credentials and never scrape FordPass.
- **Logs never print secrets.** The code logs only whether a credential is configured, never its value.
- **Reporting a problem.** Security issues go through private vulnerability reporting, not public
  issues — see [SECURITY.md](SECURITY.md).

---

## License

[MIT](LICENSE) © 2026 Matt Chaulklin.

Provided as-is, with no warranty. This is a personal utility rather than a supported product; if you
deploy your own copy, review the [security notes](#security-notes) and scope your Azure deployment
identity to your own repository.
