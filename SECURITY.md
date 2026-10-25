# Security Policy

`ford-fuel-alert` is a small personal utility, provided as-is under the [MIT License](LICENSE).
It is not a supported product and has no release channel or patch SLA. Reports are still welcome,
and anything affecting credential handling or vehicle-data access is taken seriously.

## Reporting a vulnerability

Please **do not open a public issue** for a security problem.

Use GitHub's private vulnerability reporting instead: go to the **Security** tab of this repository
and choose **Report a vulnerability**. That opens a private advisory visible only to the maintainer.

Expect an initial response within a couple of weeks. This is a personal project maintained in spare
time, so please size your expectations accordingly.

## Scope

In scope:

- Credential or token handling in `SmartcarFuelService`, `AcsEmailService`, and `TableAlertStateService`.
- Secret leakage through logs, state files, or Table Storage entities.
- The GitHub Actions workflows in [`.github/workflows/`](.github/workflows/) — particularly anything
  that could let untrusted code reach the deployment identity.
- The Bicep templates in [`infra/`](infra/) — role assignments, Key Vault access, and public network exposure.

Out of scope:

- Vulnerabilities in Azure, Smartcar, or Ford services themselves. Report those to the respective vendor.
- Anything requiring an attacker to already control the deployed Function App or its managed identity.
- Missing hardening on the fake/dev providers (`FakeFuelService`, `ConsoleEmailService`,
  `FileAlertStateService`), which are explicitly local-development only.

## Design rules this project holds to

These are enforced in review; a change that breaks one of them is a bug:

- **Vehicle-owner authorization only.** Vehicle data is reached exclusively through Smartcar Connect,
  the provider's official OAuth flow. No FordPass scraping, and no storing of Ford account credentials.
- **No secrets in source control.** `local.settings.json` is gitignored; only
  `local.settings.sample.json`, with placeholders, is committed. Cloud secrets live in Key Vault and
  are reached through Key Vault references and managed identity.
- **Logs never print secrets.** The code logs only whether a credential is configured, never its value.
- **Identity over connection strings.** Storage and Table access prefer managed identity
  (`AlertState__ServiceUri`) over embedded keys.

## Deploying your own copy

If you fork or deploy this yourself, note that the deployment workflow authenticates to Azure with
OIDC and no stored credential. Scope the federated credential's subject to your own repository and
branch, and grant the deployment identity only the roles it needs. See
[Infrastructure (Bicep / azd)](README.md#infrastructure-bicep--azd) for the exact setup.
