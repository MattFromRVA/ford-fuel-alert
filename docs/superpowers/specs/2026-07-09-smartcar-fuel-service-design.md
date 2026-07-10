# SmartcarFuelService — Design (Issue #4)

Implement the `SmartcarFuelService` placeholder so the function can read a real vehicle's
fuel level via Smartcar, behind the existing `IFuelService` interface. Fake service stays the
default; Smartcar is selected when `FuelAlert__UseFakeFuelService=false`.

## Smartcar API contract (from official docs — not guessed)

- **Token refresh:** `POST https://auth.smartcar.com/oauth/token`
  - `Authorization: Basic base64(clientId:clientSecret)`
  - `Content-Type: application/x-www-form-urlencoded`
  - body: `grant_type=refresh_token&refresh_token=<token>`
  - response JSON: `access_token`, `token_type` (`Bearer`), `expires_in` (7200s), and a
    **rotated** `refresh_token` that invalidates the one just used.
- **Fuel:** `GET https://api.smartcar.com/v2.0/vehicles/{id}/fuel`
  - `Authorization: Bearer <access_token>`
  - `SC-Unit-System: imperial` so `range` is returned in **miles**.
  - response JSON: `percentRemaining` (0–1 decimal), `amountRemaining`, `range`.

Note: Smartcar flags v2.0 for deprecation by Q4 2026, but it is the current documented version;
we use it as documented rather than guess a successor.

## Why the refresh token must be persisted

The access token lives 2 hours and the timer runs every 2 hours, so essentially every run
exchanges the refresh token, and each exchange rotates it. Losing the rotated token strands the
integration. In-memory caching cannot survive across runs (2 hours apart, host may recycle), so
the rotated refresh token is persisted durably.

**Decision:** persist in **Azure Table Storage**, reusing the existing `Azure.Data.Tables`
dependency and the same storage account as alert state. (Key Vault rejected: needs a new NuGet
package + managed-identity secret-write permission — heavier than warranted for a personal utility.)

## Components

1. **`ISmartcarTokenStore` + `TableSmartcarTokenStore`** — new internal seam. Justified because
   the ticket explicitly requires persisting the rotated token *and* unit-testing token refresh;
   a seam makes both testable with an in-memory double. Stores a single entity holding the current
   refresh token (PartitionKey fixed, RowKey per vehicle). Mirrors `TableAlertStateService`'s
   `CreateIfNotExists` + upsert pattern.
2. **`SmartcarFuelService : IFuelService`** — orchestration:
   1. Load refresh token from the store; on first run (store empty) fall back to
      `Smartcar__RefreshToken` config.
   2. Exchange for an access token; **immediately persist the rotated refresh token** before the
      fuel call so a later failure can't lose it.
   3. Cache the access token in-memory until near expiry (skip refresh if still valid).
   4. `GET .../fuel`, map to `FuelStatus`: `PercentRemaining = percentRemaining * 100`,
      `RangeMiles = range`, `RetrievedAtUtc = TimeProvider.GetUtcNow()`,
      `VehicleId = Smartcar__VehicleId`.
3. **`SmartcarOptions`** gains `VehicleId`, `TableName` (default e.g. `SmartcarTokens`), and reuses
   `ConnectionString`/`ServiceUri` (same shape as `AlertStateOptions`) for the token store.
4. **`Program.cs`** — in the `else` (non-fake) branch, register the token store + service, wired
   through `IHttpClientFactory`.

## Dependencies

- Add `Microsoft.Extensions.Http` (for `IHttpClientFactory`) — explicitly authorized by the ticket.
- No other new packages. `Azure.Data.Tables` already present.

## Error handling (graceful degradation)

Any non-success HTTP status (429 rate limit, vehicle asleep, 4xx/5xx), network exception, or
malformed/unparseable body → **log a warning and return `null`**. Never throw — a crashed timer run
is worse than a skipped reading, and the alert logic already treats null as "data unavailable".
A failed token exchange also returns null. Never log token values, only presence.

## Testing (mocked `HttpMessageHandler`, in-memory token store)

- Token refresh happy path persists the rotated refresh token to the store.
- Fuel happy path maps fields correctly.
- `percentRemaining` 0–1 is scaled to 0–100.
- Each error path (token exchange fails, fuel call 4xx/5xx/429, network throw, bad JSON) → `null`.
- Cached access token is reused within its validity window (no second token call).

## Out of scope

- One-time OAuth onboarding (separate issue).
- Flipping `FuelAlert__UseFakeFuelService=false` in production (E2E cutover issue).

## Docs

- Update `README.md` settings table: add `Smartcar__VehicleId`, token-store settings; note that
  `Smartcar__RefreshToken` is a bootstrap seed, thereafter rotated in Table Storage.
- Add new settings to `local.settings.sample.json` with placeholders.
