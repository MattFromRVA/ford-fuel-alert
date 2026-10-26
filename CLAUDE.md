# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# FordFuelAlert

FordFuelAlert is a small personal utility: a timer-triggered Azure Function (.NET 10 isolated worker) that checks a vehicle's fuel level every 2 hours and emails when it gets low. The real Smartcar fuel provider, Azure Communication Services email, and Azure Table Storage state are all implemented, but **the fake fuel service remains the default** (`Fuel__Provider=fake`) so the Function, alert/reset state machine, email logic, persistence, and tests run locally with sample data. Providers are swappable behind interfaces + DI, selected by configuration.

Always reference these instructions first and fall back to search or bash commands only when you encounter unexpected information that does not match the info here.

### Session quick start (the 60-second version)

1. **Pull fresh main** (see next section) — never start from a stale base.
2. **Spec-to-Reality Check** — open every file/class the ticket names before writing code.
3. **Check scope** — `git log --oneline -10` and `git diff origin/main...HEAD --name-only`; touch only files in your ticket's scope.
4. **Alert decision logic lives in one place** — `FuelAlertService.EvaluateAsync`. Do not duplicate threshold/reset/dedup rules anywhere else (see "Single Sources of Truth").
5. **Reproduce logic bugs with a failing xUnit test first** using the test doubles in `tests/FordFuelAlert.Tests/Fakes/TestDoubles.cs`.
6. **Before pushing:** `dotnet build -warnaserror` (CI treats warnings as errors) and `dotnet test` — both must be clean.

---

## Pull Main First (DO THIS BEFORE ANY OTHER WORK)

**Every agent session must pull a fresh main before making any changes.** Starting work against a stale base produces merge conflicts and "phantom regressions" where you re-introduce code another branch already fixed or removed.

As the very first step in any session — before reading code, before running tests, before editing any file, and **before creating any new branch**:

- **If you are about to create a new feature branch:** run `git checkout main && git pull origin main` first, then `git checkout -b <new-branch>` from that fresh state. Never branch off stale local main.
- If you are already on `main`: run `git pull origin main`.
- If you are already on a feature branch: run `git fetch origin main && git merge origin/main` (or rebase) to bring the latest main into your branch.

If the pull/merge introduces conflicts, resolve them before touching ticket work — do not stack new changes on top of an unresolved merge.

---

## Scope and Out-of-Scope Concerns

### The fake provider stays the default — do not change that uninvited

- **`SmartcarFuelService` and `TableAlertStateService` are fully implemented and registered** (selected via `Fuel__Provider=smartcar` and `AlertState__Provider=table` respectively), as is `FordConnectFuelService` (`Fuel__Provider=fordconnect`). Do not modify them unless the ticket explicitly targets the Smartcar or durable-state integration.
- **Keep `Fuel__Provider=fake`** in samples and defaults — local dev and tests run against the fake provider; the Smartcar and FordConnect providers are opted into per deployment.
- **Do not add graceful degradation or extra fallbacks** to the fuel providers — unavailable fuel data (null percent) already has defined behavior: log a warning, update `LastCheckedUtc`, send nothing.

### General out-of-scope rules

- **Do not add new NuGet packages** without explicit permission. The Functions project intentionally has a minimal dependency set (`Azure.Data.Tables`, `Azure.Communication.Email`, `Azure.Identity`, plus the Functions worker/hosting packages).
- **Do not refactor code outside the scope of your ticket.** File a separate ticket or mention it in the PR description instead.
- **Do not introduce new abstractions** beyond the three existing interfaces (`IFuelService`, `IEmailService`, `IAlertStateService`) unless the ticket explicitly requires them. No MediatR, AutoMapper, base classes, etc.
- **Do not add logging frameworks.** The project uses `ILogger<T>` from the worker host; keep it that way.
- **Do not add comments to code.** No explanatory comments, no XML doc comments, no `#` header blocks in YAML or Bicep. Let naming and structure carry the meaning; if a change needs justification, put it in the PR description. Existing comments stay as they are — do not strip them unless the ticket says to.

### Security rules (non-negotiable, from README)

- **Vehicle-owner authorization is required** for any real vehicle-data integration — official OAuth (Smartcar Connect) only.
- **Never store Ford account usernames/passwords, and never scrape FordPass** or other unofficial endpoints.
- **No secrets in source control.** `local.settings.json` is gitignored; commit only `local.settings.sample.json` with placeholders. Cloud secrets go in Key Vault.
- **Logs never print secrets.** Log only whether a credential is configured, never its value (see the pattern in `SmartcarFuelService`).
- **Do not guess undocumented API details** — follow official Smartcar / Ford developer docs when changing the real provider.

---

## Spec-to-Reality Check (READ BEFORE STARTING ANY TICKET)

Before writing any code for a ticket, verify that the ticket's description matches the current state of the codebase:

1. **If the ticket references a file, class, or method, open it first** and confirm it exists with the structure the ticket describes.
2. **If the ticket says "create X" but X already exists**, stop. Do not create a duplicate — the existing implementation may need extending instead. (The Smartcar, ACS email, and Table Storage integrations all already exist — extend them, don't recreate them.)
3. **If the ticket says "modify X" and X doesn't exist**, stop and flag the discrepancy rather than guessing what was meant.
4. **If the ticket says "mirror behavior from Y" and Y has changed since the ticket was written**, follow the current behavior of Y and note the discrepancy in the PR description.

---

## Single Sources of Truth

Before implementing any behavior, check whether an existing class already owns it:

- **Alert/reset decision logic (the whole state machine)** → `Services/FuelAlertService.cs` (`EvaluateAsync`). It is deliberately free of Azure Functions types so it can be unit-tested directly. Do not put threshold comparisons, dedup checks, or reset logic in the Function, an email service, or anywhere else.
- **The Azure Function is a thin trigger** → `Functions/CheckFuelLevelFunction.cs` only logs start/finish and calls `EvaluateAsync`. Keep it that way. Its `[TimerTrigger]` attribute binds to the `FuelAlertSchedule` app setting via `%FuelAlertSchedule%`; the setting is required and is set to `0 0 */2 * * *` (every 2 hours) in `local.settings.sample.json` and Bicep.
- **Fuel data retrieval** → implementations of `Abstractions/IFuelService.cs` (`FakeFuelService` by default; `SmartcarFuelService` or `FordConnectFuelService` when `Fuel__Provider` is `smartcar` or `fordconnect`, dispatched by `FuelServiceFactory`). Providers map onto `Models/FuelStatus.cs` (`PercentRemaining` 0–100, `RangeMiles`, `RetrievedAtUtc`).
- **Email delivery** → implementations of `Abstractions/IEmailService.cs` (`ConsoleEmailService` by default; `AcsEmailService` for Azure Communication Services — selected via `Email__Provider` in `EmailServiceFactory`). Email *content* is built in `FuelAlertService.BuildAlertEmail`, not in the email service.
- **State persistence** → implementations of `Abstractions/IAlertStateService.cs` (`FileAlertStateService` for local dev; `TableAlertStateService` for Azure Table Storage — selected via `AlertState__Provider` in `AlertStateServiceFactory`). State shape is `Models/AlertState.cs`.
- **Provider selection and all DI wiring** → `Program.cs`. New swappable implementations get registered there behind the existing interfaces, chosen by configuration.
- **Configuration** → options classes in `Models/` (`FuelAlertOptions`, `FuelOptions`, `FakeFuelOptions`, `SmartcarOptions`, `FordConnectOptions`, `EmailOptions`, `AlertStateOptions`), each with a `SectionName` constant, bound in `Program.cs`. Settings use the double-underscore convention (`FuelAlert__ThresholdPercent`). The full settings table lives in `README.md` — keep it updated when adding settings, and add new settings to `local.settings.sample.json` with placeholder values.

### Behavioral invariants (each exists on purpose — do not "simplify" them away)

- **Hysteresis, not a simple threshold.** Send when `percent <= ThresholdPercent` (default 25) and `AlertSent == false`; reset only when `percent >= ResetPercent` (default 40). Between the two, nothing changes — this prevents flapping when fuel hovers near the threshold. Both comparisons are inclusive (`<=` / `>=`) and tests pin the boundaries.
- **Duplicate suppression.** While `AlertSent == true`, repeated low readings send nothing. No email is sent on reset either.
- **Unavailable data is a defined no-op.** A null `PercentRemaining` logs a warning, saves `LastCheckedUtc`, and makes no alert decision. It does not send, reset, or throw.
- **Every run persists state** (`LastCheckedUtc`, `LastKnownPercent`, `LastKnownRangeMiles`) — even the unavailable-data path.
- **Missing recipient email is a warning, not an error — with the console provider.** `ConsoleEmailService` logs the alert with a placeholder recipient so the state machine behaves identically with and without configuration. `AcsEmailService` deliberately throws on a missing/invalid recipient instead of sending garbage to ACS — real deployments with `Email__Provider=acs` must set `FuelAlert__RecipientEmail`.
- **Time comes from the injected `TimeProvider`** in business logic — never `DateTime.Now`/`DateTimeOffset.UtcNow` inside `FuelAlertService`. This is what makes time-dependent assertions testable.

---

## Project Structure

Standard `src/` + `tests/` layout, one solution file (`FordFuelAlert.slnx`):

```
src/FordFuelAlert.Functions/
  Program.cs                  DI wiring (fake vs. Smartcar fuel, console vs. ACS email, file vs. table state)
  host.json
  Functions/                  CheckFuelLevelFunction (thin timer trigger)
  Abstractions/               IFuelService, IEmailService, IAlertStateService
  Models/                     FuelStatus, AlertState, EmailMessage, FuelAlertResult, *Options
  Services/                   FuelAlertService, FakeFuelService, SmartcarFuelService,
                              FordConnectFuelService (+ FordConnectTokenStore), ConsoleEmailService,
                              AcsEmailService, FileAlertStateService, TableAlertStateService, and
                              the FuelServiceFactory / EmailServiceFactory / AlertStateServiceFactory
                              provider selectors
tests/FordFuelAlert.Tests/
  FuelAlertServiceTests.cs    State-machine coverage (thresholds, dedup, reset, unavailable data)
  FakeFuelServiceTests.cs     Fake provider config parsing
  SmartcarFuelServiceTests.cs, FordConnectFuelServiceTests.cs, FordConnectTokenStoreTests.cs,
  AcsEmailServiceTests.cs, TableAlertStateServiceTests.cs,
  FuelServiceFactoryTests.cs, EmailServiceFactoryTests.cs, AlertStateServiceFactoryTests.cs
                              Real-provider and factory-selection coverage
  Fakes/TestDoubles.cs        StubFuelService, RecordingEmailService, InMemoryAlertStateService
```

---

## Building, Testing, and Running

- `dotnet restore` / `dotnet build` — a few seconds each. **CI builds with `-warnaserror`**, so build locally with `dotnet build -warnaserror`; any new warning fails the pipeline.
- `dotnet test` — a few seconds, all tests pass (xUnit).
- Run a single test class: `dotnet test --filter "FullyQualifiedName~FuelAlertServiceTests"`
- CI (`.github/workflows/ci.yml`): ubuntu-latest, .NET `10.0.x`, restore → build `-warnaserror` → test, on pushes to `main` and all PRs.

### Running the Function locally

Requires Azure Functions Core Tools v4 and Azurite (timer triggers need a storage backend):

```bash
cp src/FordFuelAlert.Functions/local.settings.sample.json src/FordFuelAlert.Functions/local.settings.json
azurite --silent --location ./.azurite          # separate terminal
cd src/FordFuelAlert.Functions && func start
```

Trigger immediately instead of waiting for the timer:

```bash
curl -X POST http://localhost:7071/admin/functions/CheckFuelLevelFunction \
  -H "Content-Type: application/json" -d '{"input":""}'
```

To experiment, edit `FakeFuel__PercentRemaining` in `local.settings.json` and restart `func start`. To re-arm the alert between experiments, delete the saved state file (path in `AlertState__FilePath`, defaults under the local app-data folder). The expected-behavior table for fake values is in `README.md`.

---

## Testing Conventions

- **Logic bugs: write a failing test first.** The state machine is driven entirely by injected inputs (fuel status, prior state, options), so any wrong-decision bug can be captured as a unit test. Do not try to reproduce logic bugs by running the Function host.
- **Use the existing test doubles** in `Fakes/TestDoubles.cs` — `StubFuelService` (canned status), `RecordingEmailService` (captures sent messages), `InMemoryAlertStateService` (holds state, counts saves). Extend these rather than introducing a mocking library; the repo intentionally has none.
- **Follow the `Build(...)` helper pattern** in `FuelAlertServiceTests`: construct the service with doubles, `Options.Create(...)`, `TimeProvider.System`, and `NullLogger<T>.Instance`.
- **Tests must not touch the network or the real filesystem state path.** Everything runs against in-memory doubles.
- **Assert boundaries structurally.** Threshold/reset comparisons are inclusive; boundary tests (exactly 25%, exactly 40%) exist on purpose — don't weaken them.
- When fixing a bug, add a test that fails without the fix and passes with it. Before updating a failing assertion, ask whether the test was asserting the old buggy behavior; only then change it, with a comment explaining why.

---

## PR Description Guidance

Keep PR descriptions concise and useful to a human reviewer:

- Only say a command was run if you actually ran it.
- If you added or changed tests, summarize what was added or changed.
- If local command execution could not be verified: one short sentence only, no boilerplate.

---

## Validation

- `dotnet build -warnaserror` completes with 0 warnings and 0 errors on current main — new warnings mean your change introduced them.
- `dotnet test` passes with 0 failures.
- If you changed settings or behavior described in `README.md` (settings table, fake-value behavior table, architecture diagram), update the README in the same PR.
