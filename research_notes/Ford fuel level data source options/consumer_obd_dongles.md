# Consumer OBD-II dongles with a cloud API as a fuel-level source for a 2021 Ford F-250 (gas) and 2021 Expedition 3.5L EcoBoost (US, as of 2026-10-25)

Legend used throughout: **[confirmed]** = stated on a primary source I fetched; **[secondary]** = stated by a third-party/aggregator page; **[inferred]** = my inference from cited facts; **[unverified]** = could not find a source.

Scope note: the FordFuelAlert function needs one number — `PercentRemaining` — roughly every 2 hours, from a device the owner buys and owns, via an official API (no app scraping). The table below is organized around that need.

## Summary fact table

| Vendor / device | Fuel % in API? | Public API / webhooks | Key acquisition | Auth | Fuel freshness while parked | Hardware price (2026) | Subscription (2026) | Reliability signal | Verdict for this project |
|---|---|---|---|---|---|---|---|---|---|
| **Bouncie** (OBD-II, 4G LTE) | Yes: `stats.fuelLevel` = "Percentage of fuel remaining in tank as reported by vehicle" **[confirmed, OpenAPI spec]**; vendor says fuel is available on "most (but not all) vehicles" **[confirmed]**. No Ford-F-250/Expedition-specific confirmation found **[unverified]**. | REST `GET /v1/vehicles`, `/v1/trips`; webhooks: `tripStart, tripData, tripEnd, tripMetrics, mil, battery, connect, disconnect, vinChange, userGeozone, applicationGeozone` — no fuel-specific webhook event **[confirmed]** | Self-serve: register app at bouncie.dev, get Client ID/Secret, set redirect URL **[secondary, bouncie-mcp README]**; bouncie.dev login is Keycloak SSO against the Bouncie account **[confirmed, redirect observed]** | OAuth 2.0 authorization code; token endpoint `https://auth.bouncie.com/oauth/token`, grants `authorization_code` + `refresh_token`, Bearer tokens **[confirmed]** | Device enters low-power state after ignition off and "checks in every few hours" to report status/signal/location **[confirmed]**; whether fuelLevel is refreshed on those check-ins is **not documented [unverified]**; `stats.lastUpdated` exposes staleness **[confirmed]** | $89.99 one-time **[confirmed, bouncie.com/health]** | $8/mo per device (bouncie.com/health) **[confirmed]**; $8.35/device at 3+ and $9.65 single-device figures appear on third-party pages **[secondary, conflicting]** | Trustpilot 1.3/5 (939 reviews) **[confirmed]**; Aug-2026 1-star review: "webhook deactivated repeatedly", old trips unavailable via API **[confirmed]**; May-2023 review: unit on a 2020 F-150 would not sync, replacement died after 9 days **[confirmed]**; no documented rate limit **[confirmed]** | **Best fit.** Only vendor with a self-serve consumer API that documents a fuel-percent field. Must verify fuel populates on these two Fords (not documented). |
| **Zubie** (Zinc API) | Yes: `fuel_level` (int, sample 84) + `fuel_level_timestamp` on vehicle object; also `range_remaining` **[confirmed]**; help center: "only about 80% of vehicles in the Zubie system report it" **[confirmed]** | REST `GET https://api.zubiecar.com/api/v2/zinc/vehicles`; webhooks with HMAC `Zubie-Signature`; `fuel_level` also present inside webhook `vehicle` object **[confirmed]** | developer.zubie.com → Manage Apps → Create New App → Client ID/Secret; portal says "for developers and businesses" — no explicit statement that consumer accounts can create apps **[confirmed text; eligibility unverified]** | OAuth 2.0 authorization code; `https://login.zubiecar.com/authorize`, `/oauth/token`; access token ~1 h (`expires_in: 3600`), refresh token grant **[confirmed]** | "Heartbeat events are sent at the beginning of a trip, and periodically when the vehicle is not in a trip" (no interval given) **[confirmed]**; fuel refresh while parked **[unverified]** | Terms page lists "Zubie Device: $85" **[secondary via search snippet]** | Consumer "Zubie Car Connect" still sold via store link (help article dated Sep 11 2026) but price not shown **[confirmed]**; aggregators: ~$17.99/mo consumer, $15–$36/mo fleet tiers **[secondary, conflicting]** | Acquired by TSD Mobility (Reynolds & Reynolds subsidiary) Oct 9 2025 **[confirmed]**; no outage data found **[unverified]** | **Second choice.** Richer fuel fields (timestamp + range), but pricing higher and consumer API eligibility post-acquisition is unclear. |
| **Vyncs** (Agnik) | Fuel level listed as included on all plans "as long as the vehicle reports this data through the OBD-II port" **[confirmed]** | **No public API.** "API integrations with fleet management systems" only on Fleet plan, "offered only based on a case-by-case evaluation of the requirement", may carry additional charges **[confirmed]** | Apply / case-by-case, Fleet plan only **[confirmed]** | n/a | GPS batches sent every 3 min (Basic/Premium/Fleet) or 1 min (Pro) while driving **[confirmed]**; parked refresh **[unverified]** | Included in annual fee; one-time $45.99 activation fee **[confirmed]** | $99.99/yr Basic 4G+, $109.99 Premium, $119.99 Pro, $119.99 Fleet; "no monthly fee" **[confirmed]** | Not researched in depth | **Not viable** — no self-serve API. |
| **Vinli** | Platform docs say it supports "most legislatively required, Mode 1 PIDs" — fuel level not named **[confirmed]** | Docs at docs.vin.li still live, but company page is "agentic fleet management… powered by Velona", Book-a-Demo only; hardware production discontinued 2018 **[confirmed]** | Enterprise / contact sales **[inferred]** | OAuth-style app auth per docs **[confirmed but stale]** | n/a | No consumer device sold **[confirmed]** | n/a | Joined Sofico June 2 2026 **[confirmed]** | **Not viable** — enterprise-only, no consumer hardware. |
| **Carly** | Reads live data incl. fuel via phone app **[secondary]** | **No cloud API.** Adapter pairs to phone via Bluetooth/BLE (or Wi-Fi) and only works with the phone present **[secondary, how-to guides]**; Carly site offers no API/remote monitoring **[confirmed by absence]** | n/a | n/a | n/a — no cellular radio | Not captured (site pages were navigation-only) **[unverified]** | Not captured **[unverified]** | n/a | **Not viable** — Bluetooth-only diagnostic tool. |
| **Mojio / T-Mobile SyncUP DRIVE** | T-Mobile marketing: "know your fuel level…" with fine print "not available on all makes and models" **[secondary]** | **No public developer API in 2026.** `developer.moj.io` does not resolve (DNS ENOTFOUND) **[confirmed]**; moj.io now says "Mojio is now Force Fleet" (SMB fleet product), APIs/SDKs only via "Mobility Studio" / Contact Us **[confirmed]** | Contact sales **[inferred]** | n/a | n/a | SyncUP DRIVE still sold by T-Mobile; legacy hardware/app retired Mar 15 2025; new unit SD-7000T1 is eSIM, T-Mobile-locked **[secondary]** | Not captured **[unverified]** | Legacy shutdown 2025 **[secondary]** | **Not viable** — no API for individuals. |
| **Verizon Hum** | n/a | n/a | n/a | n/a | n/a | n/a | n/a | **Service discontinued Dec 31 2025**; all data deleted after that **[secondary, multiple outlets]** | **Dead.** |
| **MOTOsafety** | Unknown **[unverified]** | No API found **[unverified]** | n/a | n/a | n/a | $24.99–$69.99 device **[secondary]** | +$25.00/mo on official store **[secondary]** | n/a | Not viable (no API, 3x Bouncie's price). |
| **Linxup / Spytec** | Linxup reports once a minute while running; fuel-tax/gallons reports exist; raw fuel level via API **[unverified]** | Linxup has an "API" help article but integration (e.g. Fleetio) is set up by emailing a Linxup rep, 2–3 business days **[secondary]**; Spytec: no API found **[unverified]** | Rep-assisted **[secondary]** | n/a | n/a | n/a | Spytec $8.95–$14.95/mo **[secondary]** | n/a | Not viable as self-serve. |
| **Kayo** | Unknown **[unverified]** | No API found **[unverified]**; kayousa.com redirects to a parked `ww19.` host **[confirmed]** — company status doubtful | n/a | n/a | n/a | $65.89–$167.66 (conflicting third-party listings) **[secondary]** | $6.99/vehicle/mo **[secondary]** | Site apparently parked **[inferred]** | Not viable. |
| **Automatic** (legacy) | n/a | n/a | n/a | n/a | n/a | n/a | n/a | Mentioned in HA thread as the product Bouncie users migrated from **[confirmed]** | Dead (not re-verified; out of scope). |

---

## Key Question 1 — Bouncie: does the API expose fuelLevel, is it populated for Ford F-250 / Expedition, rate limit, parked behavior, price?

### Takeaway
Bouncie's API does expose a fuel percentage (`stats.fuelLevel`) on `GET /v1/vehicles` and a working Home Assistant integration maps it to a `%` sensor, but there is **no fuel-specific webhook**, **no documented rate limit**, **no statement that fuel refreshes while parked**, and **no Ford-truck-specific confirmation** that the field is populated. Price is $89.99 + $8/mo.

### Cited Findings
- `stats.fuelLevel` (number) is documented as "Percentage of fuel remaining in tank as reported by vehicle"; `stats.lastUpdated` is "Last time vehicle document was updated"; other stats are `odometer`, `location{lat,lon,heading,address}`, `isRunning`, `speed`, `mil{milOn,lastUpdated,qualifiedDtcList}`, `battery{status: normal|low|critical, lastUpdated}` — [Bouncie OpenAPI spec mirrored by Jentic](https://raw.githubusercontent.com/jentic/jentic-public-apis/main/apis/openapi/api.bouncie.dev/main/1.0.0/openapi.json)
- Webhook `events` enum: `tripStart, tripData, tripEnd, tripMetrics, mil, battery, connect, disconnect, vinChange, userGeozone, applicationGeozone`; `tripData` streams during a trip, `tripMetrics` fires once at trip end, `battery` only when voltage drops below threshold — [same OpenAPI spec](https://raw.githubusercontent.com/jentic/jentic-public-apis/main/apis/openapi/api.bouncie.dev/main/1.0.0/openapi.json)
- Token endpoint `https://auth.bouncie.com/oauth/token` (POST JSON), grant types `authorization_code` and `refresh_token`, response `access_token, refresh_token, expires_in, token_type=Bearer`; consent at `https://auth.bouncie.com/dialog/authorize` — [OpenAPI spec](https://raw.githubusercontent.com/jentic/jentic-public-apis/main/apis/openapi/api.bouncie.dev/main/1.0.0/openapi.json); [Jentic summary](https://jentic.com/apis/bouncie.dev/bouncie)
- "Bouncie does not document explicit per-second rate limits in the spec"; advice is to rely on webhooks and back off on HTTP 429 — [Jentic](https://jentic.com/apis/bouncie.dev/bouncie); [Jentic api.bouncie.dev page](https://jentic.com/apis/api.bouncie.dev/bouncie)
- App registration is self-serve: "Register at bouncie.dev and create an app… note the Client ID and Client Secret… set the redirect URL" — [digitalhen/bouncie-mcp README](https://github.com/digitalhen/bouncie-mcp). That README also throttles upstream `/trips` calls to 250 ms by default citing "Bouncie's rate limit" without a number.
- bouncie.dev login redirects to `https://sso.login.bouncie.com/realms/bouncie/protocol/openid-connect/auth?...client_id=maker-web` (Keycloak, OIDC + PKCE) — observed 302 when fetching [https://www.bouncie.dev/](https://www.bouncie.dev/)
- The Home Assistant custom integration `mandarons/ha-bouncie` (49 stars, 126 commits) exposes a `car-fuel` sensor with `native_unit_of_measurement="%"` and `value_fn=lambda v: int(v["stats"]["fuelLevel"])`, plus `stats.lastUpdated`, `stats.isRunning`, `stats.odometer`, `stats.battery.status`, `stats.mil.milOn`; default `scan_interval` 10 s — [sensor.py](https://raw.githubusercontent.com/mandarons/ha-bouncie/main/custom_components/bouncie/sensor.py); [repo](https://github.com/mandarons/ha-bouncie)
- HA community user "ChargerDad" (Nov 24 2022): "getting fuel levels for 4 vehicles" via the REST API, polling every 10 s; no makes/models named — [HA community thread p.2](https://community.home-assistant.io/t/new-integration-request-bouncie-obd2-vehicle-tracking/254016?page=2)
- Vendor: "This feature is only available if your vehicle reports the fuel level through your OBD2 Port… Fuel level reporting is available on most (but not all) vehicles"; low-fuel push notification fires "each time the fuel level drops 5% below the level you have set" — [help.bouncie.com Fuel Level Notifications](https://help.bouncie.com/en/articles/2998579-fuel-level-notifications); [help.bouncie.com app vehicle card](https://help.bouncie.com/en/articles/6365683)
- Parked behavior: after engine off the device sends an end-of-trip signal, drops to a low-power state, and "checks in with our system every few hours to report its operational status, cell network signal details, and update its location"; garages can block signal so sleep begins before trip data is sent — [help.bouncie.com Device Health and Location](https://help.bouncie.com/en/articles/9797549-device-health-and-location); [Lost Connection](https://help.bouncie.com/en/articles/1018750-lost-connection); [Delayed Driving Trip Data](https://help.bouncie.com/en/articles/1743967-delayed-driving-trip-data)
- Price: "$8 MONTHLY, $89.99 one-time device charge"; "no activation fees", month-to-month, cancel anytime — [bouncie.com/health](https://www.bouncie.com/health); device listed at $89.99 USD on bouncie.com pages (search snippet); official /pricing page renders the 3+ device price as "0.00" (rendering bug) — [bouncie.com/pricing](https://www.bouncie.com/pricing)
- Third-party 2026 figures: $89.99 device, $9.65/mo single, $8.35/mo at 3+ devices (HotAirTag, updated Jul 6 2026) — [hotairtag.com review](https://hotairtag.com/bouncie-gps-tracker-review/); $67 device + $8/mo (airpinpoint) — [airpinpoint](https://airpinpoint.com/compare/airtags-vs-bouncie); $90 device + $8.35/mo/vehicle (Olivehurst PUD staff report) — [PDF](https://olivehurstpud.specialdistrict.org/files/0e26436a5/08.02_Staff_Report_Fleet_Vehicle_GPS_Tracking_System.pdf). Figures conflict; bouncie.com/health ($8) is the primary source.
- Reliability: Trustpilot TrustScore 1.3/5 from 939 reviews; Aug 1 2026 1-star: webhook "deactivated repeatedly", 2020 trips visible in app but not retrievable via API, unresolved for months; May 7 2023 2-star: on a **2020 Ford F150** the unit "would not sync with the app", replacement worked 9 days; Feb 2023: "missing trip data, frequent disconnect errors"; Sep 2021: network sunset required new device at full price — [Trustpilot](https://www.trustpilot.com/review/www.bouncie.com)
- Status trackers: Jan 2026 44-min chat-support outage; Apr 27 2026 15-min degraded-performance incident; user reports of stale location/trips not showing — [isdown.app](https://isdown.app/status/bouncie); [pingoru](https://pingoru.io/providers/bouncie/incidents/15891); [statusgator](https://statusgator.com/services/bouncie)
- HA thread: "The device and service has been pretty reliable too" (Bosston, Dec 6 2022) — [HA community](https://community.home-assistant.io/t/bouncie-integration/418553); but "I'm pretty disappointed in these devices" citing limited data detail (omayhemo, Nov 28 2022) — [HA community p.2](https://community.home-assistant.io/t/new-integration-request-bouncie-obd2-vehicle-tracking/254016?page=2)
- IFTTT exposes a Bouncie "Vehicle has low fuel" trigger ("The percent of fuel remaining"), polled every 5 min (Pro) / hourly (Free) — evidence that fuel % flows through Bouncie's platform — [IFTTT low_fuel trigger](https://ifttt.com/bouncie/triggers/low_fuel)
- Bouncie markets itself as a Hum replacement after Verizon's shutdown — [bouncie.com/hum-by-verizon](https://www.bouncie.com/hum-by-verizon)

### Inferences
- For a 2-hour poll, `GET /v1/vehicles` once per run is the right call; there is no fuel webhook, so webhooks only help via `tripEnd`/`tripMetrics` (fuel used per trip) and don't carry a tank-level event. Polling every 2 h is far below any plausible limit (community integrations poll every 10 s without reported throttling).
- Because the only documented parked check-in is "every few hours" and is described as reporting status/signal/location (not fuel), the fuelLevel value is most likely frozen at the last ignition-on reading until the next trip. For a low-fuel alert this is acceptable: fuel can't drop while parked. Compare `stats.lastUpdated` to detect staleness.
- The authorization-code flow requires a one-time browser consent by the owner; the function would then store and rotate the refresh token (Key Vault). This is the same pattern as the existing Smartcar provider.
- Whether a 2021 F-250 (6.2L/7.3L gas) and 2021 Expedition populate `fuelLevel` is **unknown**; the only Ford-specific Bouncie data point found is a hardware sync failure on a 2020 F-150, not a fuel-data report. Treat as "likely but unproven" given Bouncie's "most vehicles" language and the Ford PID caveat in Key Question 7.

### Gaps
- Could not fetch docs.bouncie.dev directly (JavaScript SPA; returned only the title); exact per-event webhook payloads and any documented token lifetime remain unread.
- No numeric rate limit anywhere (vendor does not publish one).
- No Reddit/forum/app-store report found that explicitly says "Bouncie shows fuel level on my F-250 / Expedition / F-150"; also none saying it does not.
- Whether Bouncie refreshes `fuelLevel` during the parked periodic check-in is undocumented.

---

## Key Question 2 — Zubie: API status in 2026, open to individuals?, fuel field, price

### Takeaway
Zubie's Zinc API is live in 2026 (copyright 2026, v2 endpoints) with a documented `fuel_level` + `fuel_level_timestamp` on the vehicle object and webhooks, and apps are created in a self-serve portal, but the portal describes itself as for "developers and businesses"; Zubie was acquired by TSD Mobility in Oct 2025 and a consumer "Zubie Car Connect" subscription is still sold (price not published on the help page).

### Cited Findings
- Vehicle object fields: `fuel_level` (integer, sample 84), `fuel_level_timestamp` (ISO 8601, e.g. `2025-06-29T12:01:00+00:00`), `range_remaining`/`range_remaining_um` (e.g. 16.0 mi), `fuel_economy`, `style_fuel_capacity_gal`, `odometer`/`odometer_timestamp`, `battery_volts`, `battery_low`, TPMS fields, `vehicle_location{point,speed_mph,heading,motion_status,timestamp}`, `mil_status`, `active_trouble_codes` — [Zubie API Reference: Vehicles](https://developer.zubie.com/reference/vehicles)
- Endpoints: `GET https://api.zubiecar.com/api/v2/zinc/vehicles`, `GET .../vehicle/{vehicle_key}`, `.../battery-history`; "Heartbeat events are sent at the beginning of a trip, and periodically when the vehicle is not in a trip" — [Vehicles reference](https://developer.zubie.com/reference/vehicles)
- Auth: OAuth 2.0 Authorization Code; `https://login.zubiecar.com/authorize`, token `https://login.zubiecar.com/oauth/token`, `Authorization: Bearer`, `expires_in: 3600`, refresh_token grant; create app at developer.zubie.com/manage-apps ("My Apps → Create New App → name, redirect URI, description" → Client ID/Secret) — [Getting Started](https://developer.zubie.com/overview/getting-started)
- Webhooks: set URL + event checkboxes in Manage Apps; POST JSON with `user_key, account_key, event, timestamp`; `Zubie-Signature` header = base64 HMAC of payload with app secret; sample payload categories Device Lifecycle, Driving Activity, Driving Behavior, Vehicle Health; sample events `trip_start`, `location_change`, `device_unplug`; `fuel_level` and `battery_level` appear inside the `vehicle` object of webhook payloads; no fuel-specific event listed — [Enabling Webhooks](https://developer.zubie.com/webhooks/enabling-webhooks); [Getting Started](https://developer.zubie.com/overview/getting-started)
- Portal landing: "for developers and businesses"; lists Fuel Level, Diagnostic Codes, Battery Level, Odometer as available sensor data; © 2026 Zubie, Inc. — [developer.zubie.com](https://developer.zubie.com/)
- No rate-limit text on Getting Started, Account, Vehicles, or Webhooks pages — [Account reference](https://developer.zubie.com/reference/account)
- Help center: "only about 80% of vehicles in the Zubie system report [fuel level]"; if there is no Fuel Level section the vehicle doesn't report it; low-fuel alert threshold is configurable — [help.zubie.com Vehicle Health](https://help.zubie.com/en/articles/11115674-vehicle-health-engine-battery-and-fuel-level-monitoring)
- Consumer product: "Purchasing a Zubie Consumer Subscription – Zubie Car Connect" help article dated Sep 11 2026 links to `https://store.zubie.com/solutions/CON50012M?...`; businesses are told to call Fleet Consultants; no price shown — [help.zubie.com](https://help.zubie.com/en/articles/11115730-purchasing-a-zubie-consumer-subscription-zubie-car-connect)
- Acquisition: TSD Mobility Solutions (a Reynolds & Reynolds subsidiary since 2024) acquired Zubie, announced Oct 9–10 2025 — [Digital Dealer](https://digitaldealer.com/news/tsd-mobility-acquires-zubie-bolsters-its-telematics-technology/167728/); [Auto Rental News](https://www.autorentalnews.com/news/tsd-mobility-buys-zubie-to-bolster-tech-suite); [Houlihan Lokey](https://hl.com/about-us/transactions/zubie-tsd-mobility-solutions-reynolds-reynolds/)
- Pricing (all secondary, conflicting): consumer plan "from $17.99/month" (subger, checked Mar 2026) — [subger](https://subger.com/en/service/zubie); fleet tiers $15/$24/$36 per month (CostBench, Aug 2026) — [costbench](https://costbench.com/software/fleet-management/zubie/); Capterra $18/$22/$27 — [capterra](https://www.capterra.com/p/144648/Zubie/pricing/); "Zubie Device: $85" on Zubie terms page and $24–$27/mo partner subscriptions (Connexion/Turo) — [zubie.com/terms-of-use](https://zubie.com/terms-of-use/), [zubie.com/turo](https://zubie.com/turo/)
- Only 2015 coverage exists of the "open API for individuals" positioning — [TechCrunch 2015](https://techcrunch.com/?p=1183029); [Automotive Fleet 2015](https://www.automotive-fleet.com/news/zubie-unveils-open-api-platform-for-internet-of-cars)

### Inferences
- The developer portal has no stated gating, so an individual with a Zubie Car Connect subscription can probably create an app and authorize their own account via OAuth — but "for developers and businesses" plus the TSD/Reynolds fleet-dealer ownership makes this less certain than Bouncie. Verify by attempting app creation before buying hardware.
- `fuel_level_timestamp` is a real advantage over Bouncie for freshness logic (you can tell exactly when the fuel reading was taken rather than when the vehicle document changed).
- Total cost likely ~2x Bouncie (≈$18/mo vs $8/mo).

### Gaps
- Live consumer price at store.zubie.com not captured.
- No explicit statement whether consumer (non-fleet) accounts can create Zinc apps.
- No rate limit documented.
- No Ford-specific fuel reports found.

---

## Key Question 3 — Vyncs: API availability and fuel support; pricing

### Takeaway
Vyncs reports fuel level on every plan when the vehicle exposes it over OBD-II, but API access exists only on the Fleet plan and only "based on a case-by-case evaluation" with possible extra charges — there is no self-serve consumer API.

### Cited Findings
- Comparison table: Fuel level included on Basic/Premium/Pro/Fleet; "API integrations with fleet management systems" only on Fleet, footnote "API-integration is offered only based on a case-by-case evaluation of the requirement… additional charges may apply if approved" — [vyncs.com product comparison](https://vyncs.com/product-comparison.aspx)
- "Fuel level is supported as long as the vehicle reports this data through the OBD-II port" — [vyncs.com FAQ / features](https://vyncs.com/frequently-asked-question.aspx), [features page](https://vyncs.com/features-vyncs-gps-tracking.aspx)
- Pricing: Basic 4G+ $99.99/yr, Premium $109.99/yr, Pro $119.99/yr, Fleet $119.99/yr, plus one-time $45.99 activation fee (older page said $39.99); renewal same as first year; "No Monthly Fee, 30-Day No-Risk Trial" — [vyncs.com/pricing.aspx](https://vyncs.com/pricing.aspx); [product comparison](https://vyncs.com/product-comparison.aspx)
- GPS sampling 60 s (Basic/Premium/Fleet; points batched to the account every 3 min) or 20 s (Pro; batched every minute); paid upgrades to 30/15 s — [product comparison](https://vyncs.com/product-comparison.aspx)
- App Store listing (Agnik LLC) added a "Fuel Level Graph under Vehicle Notifications" — [apps.apple.com](https://apps.apple.com/us/app/-/id6443603113)

### Inferences
- Not viable for this project: no owner-accessible API; the only "integration" path is a fleet-sales negotiation.

### Gaps
- No Vyncs developer documentation exists publicly; unknown what the fleet API exposes.

---

## Key Question 4 — Vinli: still a consumer/developer platform in 2026?

### Takeaway
No. Vinli stopped making consumer hardware in 2018, is now an enterprise "agentic fleet management" platform (Fleet/Insurance/Intelligence, demo-only sales), and joined Sofico in June 2026; its legacy developer docs remain online but there is no consumer device to pair them with.

### Cited Findings
- vin.li homepage: "an agentic fleet management company, powered by Velona"; product lines Fleet, Insurance, Intelligence; CTAs are "Book a Demo"; data comes from "aftermarket devices, oem integration, or phone enabled gps"; news: "Today, Vinli is Joining Sofico" (Jun 2 2026), Toyota collaboration (May 13 2025); © 2026 — [vin.li](https://www.vin.li/)
- Sofico NV acquired Vinli Inc. in June 2026 — [Preqin](https://www.preqin.com/data/profile/asset/vinli-inc-/169436)
- 2018: Vinli discontinued hardware production and pivoted to enterprise data services — [TechCrunch](https://techcrunch.com/2018/10/15/vinli-launches-mobility-data-platform-signs-partnership-with-ald-automotive)
- Legacy docs still served (Telemetry, Supported PIDs, Virtual Vinli); supports "most legislatively required, Mode 1 PIDs"; fuel level not named — [docs.vin.li](https://docs.vin.li/en/latest/); [Supported PIDs](https://docs.vin.li/en/latest/web/telemetry-services/pids.html)

### Inferences
- Not viable: no hardware to buy, enterprise sales motion.

### Gaps
- Whether the public API still accepts new developer registrations was not testable.

---

## Key Question 5 — Carly: any cloud API?

### Takeaway
No. The Carly adapter is a Bluetooth/BLE (or Wi-Fi) dongle that talks only to the Carly phone app; it has no cellular radio, no GPS, and no cloud/developer API.

### Cited Findings
- Setup pairs the adapter over Bluetooth with the Carly app ("Choose Carly Adapter from the list of available devices, and enter the pairing code") — [getjerry.com how-to](https://getjerry.com/questions/how-do-i-use-carly-for-my-bmw)
- Carly's German guide: the adapter connects to the phone via Bluetooth or Wi-Fi, BLE on iOS/Android, and is "the link between the car and the smartphone" — [mycarly.com blog (DE)](https://www.mycarly.com/de/blog/codieren-tuning/auto-codiergeraet-mit-bluetooth-welche-geraete-gibt-es/)
- mycarly.com, /how-it-works/, and /plan/ contain no API, cloud, or remote-monitoring offering; features are Diagnostics, Live Data, Coding, Maintenance, Smart Mechanic; a "B2B" link exists — [mycarly.com](https://www.mycarly.com/); [how it works](https://www.mycarly.com/how-it-works/)
- Note: search results for "Carly API" surface **usecarly.com**, an unrelated AI-assistant company — do not confuse — [usecarly.com example](https://www.usecarly.com/blog/claude-kickserv-integration/)

### Inferences
- Not viable: requires a phone in range and a user-initiated session.

### Gaps
- Adapter and subscription prices were not rendered on the fetched pages.

---

## Key Question 6 — Mojio / T-Mobile SyncUP DRIVE: developer API status, fuel

### Takeaway
Mojio's public developer platform is effectively gone: `developer.moj.io` no longer resolves, and Mojio rebranded in 2026 as "Force Fleet" (SMB fleet software) with APIs available only via enterprise "Mobility Studio"/Contact Us. SyncUP DRIVE is still sold by T-Mobile (new eSIM SD-7000T1 unit; legacy units/app retired Mar 15 2025) and advertises fuel level "not available on all makes and models", but there is no owner-facing API.

### Cited Findings
- `developer.moj.io` → DNS `ENOTFOUND` on fetch (2026-10-25) — [developer.moj.io](https://developer.moj.io/)
- moj.io: "Mojio is now Force Fleet"; products Force Fleet, Felix AI fleet manager, Video Telematics, connected-vehicle platform (Telematics Engine, Mobility Studio with "APIs and SDKs", no link); audiences are OEMs, insurers, MNOs; consumer examples only T-Mobile Czech "Chytré Auto" and "Motion for Audi connect"; © 2026 — [moj.io](https://www.moj.io/)
- T-Mobile retired legacy SyncUP DRIVE hardware and app on Mar 15 2025; features moved into T Life app; new device eSIM-only and T-Mobile-locked — [PhoneArena](https://www.phonearena.com/news/t-mobile-syncup-drive-app-discontinue_id168182); [Android Authority](https://androidauthority.com/legacy-t-mobile-syncup-drive-3531996)
- SyncUP DRIVE marketing: "know their fuel level, battery voltage, and brake fluid levels"; fine print: fuel readings "not available on all makes and models" — [T-Mobile newsroom](https://www.t-mobile.com/news/syncup-drive-roadside-assistance); app "compatible only with SD-7000T1", Android store updated Feb 13 2026 — [appgoblin](https://www.appgoblin.info/apps/com.tmobile.drive)
- Older evidence a Mojio fuel-level field existed: openHAB community binding log (2015) — [openHAB community](https://community.openhab.org/t/mojio-connected-car/2446)

### Inferences
- Not viable for an individual in 2026.

### Gaps
- No 2026 statement from Mojio/T-Mobile confirming the consumer API is closed; inference rests on the dead developer host and the enterprise-only website.

---

## Key Question 7 — Technical caveat: do 2021 Ford trucks expose fuel level via standard PID 0x2F or only Ford-specific PIDs?

### Takeaway
Multiple owner reports say modern Fords do **not** answer standard Mode 01 PID 0x2F (fuel tank level input) — a 2020 F-150 owner got nothing back, and a 2024 Mondeo thread found fuel only on Ford Mode 22 PID `22F42F` — which means a dongle that only reads the SAE standard PID may show no fuel on these trucks. Whether Bouncie/Zubie read Ford's enhanced PID is **undocumented**; both vendors warn that fuel is "not available on all vehicles".

### Cited Findings
- Standard PID 0x2F = "Fuel Level Input", 0–100 %, formula 100·A/255; vehicles advertise support via the PID 00/20/40 bitmasks and "not all vehicles will support all PIDs" — [Wikipedia OBD-II PIDs](https://en.wikipedia.org/wiki/OBD-II_PIDs); [Total Car Diagnostics PID table](https://www.totalcardiagnostics.com/support/Knowledgebase/Article/View/104/0/obd2-pids-for-programmers-technical)
- 2020 F-150 owner: "it says the PID Hex for the Fuel Tank Level Input is 2F, but this isn't returning anything" — [Ford Truck Enthusiasts "Fuel Level PID" thread](https://www.ford-trucks.com/forums/1674086-fuel-level-pid.html) (search snippet; page returned 403 on direct fetch so resolution not read)
- Ford Mondeo mk4 (Jan 2024): "the 2F code just isn't sent"; Mode 22 PID `22F42F` was "the only pid to have shown a change in value which matches the fuel level change"; response `62F42F 67` → 0x67 = 103 interpreted by RealDash dev as fuel %; a separate Ford diesel PID list gives `22F42F` conversion A × 0.39216 — [RealDash forum](https://forum.realdash.net/t/obd2-ford-fuel-level-code/4568)
- 2021 F-150 owner: "the PID that worked on my old 2011 no longer works on my 2021" (Torque Pro custom PIDs) — [f150gen14.com PID experimenting](https://www.f150gen14.com/forum/threads/pid-experimenting.20273/post-391658)
- OBDLink: Ford-specific (enhanced) PIDs require the Ford add-on and appeared only for models up to 2022 for one 2023 owner; standard SAE PIDs still work — [f150gen14.com](https://www.f150gen14.com/forum/goto/post?id=402002); [OBDLink app page](https://www.obdlink.com/obd-apps/obdlink-app/)
- Vendor hedges: Bouncie "Fuel level reporting is available on most (but not all) vehicles" — [help.bouncie.com](https://help.bouncie.com/en/articles/2998579-fuel-level-notifications); Zubie "only about 80% of vehicles in the Zubie system report it" — [help.zubie.com](https://help.zubie.com/en/articles/11115674-vehicle-health-engine-battery-and-fuel-level-monitoring); Vyncs "as long as the vehicle reports this data through the OBD-II port" — [vyncs.com](https://vyncs.com/frequently-asked-question.aspx); T-Mobile SyncUP DRIVE "not available on all makes and models" — [T-Mobile](https://www.t-mobile.com/news/syncup-drive-roadside-assistance)
- Ford's own OBD service manual (2018 edition) lists PID descriptions but the fetched sources did not include a per-model 0x2F support matrix — [Ford OBDSM1802-2018 PDF](https://www.fordservicecontent.com/ford_content/catalog/motorcraft/OBDSM1802-2018.pdf)
- Unrelated but relevant to accuracy: Ford TSBs for 2024 Expedition/Navigator describe fuel gauge/DTE reading low after partial fills until IPC reprogramming — [NHTSA TSB MC-10253686](https://static.nhtsa.gov/odi/tsbs/2024/MC-10253686-0001.pdf)

### Inferences
- The 2021 F-250 and 2021 Expedition share Ford's 2020+ electrical architecture with the 2020/2021 F-150 reports above, so there is a material risk that standard PID 0x2F is not broadcast and a generic dongle shows no fuel. Commercial telematics vendors (Bouncie, Zubie) often implement OEM-specific PID tables — which is why they can claim "most vehicles" — but neither publishes a per-model list, so this must be tested empirically (buy one device, plug into each truck, check `stats.fuelLevel` / `fuel_level` after a drive).
- The ~80% / "most" coverage numbers are consistent with "standard PID plus some OEM PIDs"; Ford's gas trucks could be on either side of the line.
- IFTTT's Bouncie "low fuel" trigger and the HA `car-fuel` sensor prove the field is populated for *some* vehicles, not for Fords specifically.

### Gaps
- No vendor support article found that names Ford (or any make) as lacking fuel level.
- No Ford owner report found for Bouncie or Zubie fuel on F-150/F-250/Expedition — positive or negative.
- Could not read the full ford-trucks.com thread (403) to see whether the 2020 F-150 owner found a working PID.

---

## Key Question 8 — Home Assistant / GitHub projects polling these APIs for fuel on Fords

### Takeaway
Two active open-source consumers of the Bouncie API map `stats.fuelLevel` to a fuel-percent sensor/tool (mandarons/ha-bouncie, digitalhen/bouncie-mcp), and HA forum users report pulling fuel for multiple vehicles, but none of them names a Ford.

### Cited Findings
- `mandarons/ha-bouncie`: `car-fuel` sensor, unit `%`, from `vehicle_info["stats"]["fuelLevel"]`; polls REST every 10 s by default; OAuth config keys `redirect_uri` and `code` — [sensor.py](https://raw.githubusercontent.com/mandarons/ha-bouncie/main/custom_components/bouncie/sensor.py); [const.py](https://raw.githubusercontent.com/mandarons/ha-bouncie/main/custom_components/bouncie/const.py); [repo](https://github.com/mandarons/ha-bouncie)
- `digitalhen/bouncie-mcp`: `get_vehicles` returns "live stats (GPS location, speed, fuel level, odometer…)"; supports stdio (static `BOUNCIE_ACCESS_TOKEN`) and HTTP (OAuth proxy, PKCE) — [GitHub](https://github.com/digitalhen/bouncie-mcp)
- `streetsmartslabs/bouncie` Ruby client: constructed with `api_key` and `authorization_code`; points to docs.bouncie.dev — [GitHub](https://github.com/streetsmartslabs/bouncie)
- HA forum: "getting fuel levels for 4 vehicles" (ChargerDad, Nov 24 2022), "I have fuel gauge, speedometer, and odometer pulling via REST"; omayhemo references "my truck" but no make; nobody names Ford — [HA community p.2](https://community.home-assistant.io/t/new-integration-request-bouncie-obd2-vehicle-tracking/254016?page=2); [HA Bouncie integration thread](https://community.home-assistant.io/t/bouncie-integration/418553)
- A third-party web app ("EveryStreet") also integrates Bouncie via client ID/secret/redirect URI/auth code — [everystreet profile](https://everystreet.duckdns.org/profile)
- No Zubie, Vyncs, or Mojio Home Assistant integrations surfaced in searches — gap.

### Inferences
- Bouncie is the only one of these vendors with a living hobbyist ecosystem, which lowers integration risk and gives reference implementations of the OAuth flow and field mapping that can be mirrored in a new `IFuelService` implementation (`stats.fuelLevel` → `PercentRemaining`, `stats.lastUpdated` → `RetrievedAtUtc`; no range field, so `RangeMiles` would be null).

### Gaps
- No GitHub issue or forum post found that reports fuel behavior on a Ford with any of these dongles.

---

## Overall recommendation for the report writer (inferred)
1. **Bouncie** is the only consumer OBD dongle in Oct 2026 with (a) a self-serve developer portal, (b) a documented fuel-percent field on a REST endpoint, (c) $8/mo pricing, and (d) reference open-source clients. Caveats: webhooks carry no fuel event (poll `/v1/vehicles`), rate limits are undocumented, Trustpilot sentiment is poor (1.3/5) with 2026 complaints about webhook deactivation, and Ford fuel support is unproven.
2. **Zubie** is the fallback: better fuel fields (`fuel_level_timestamp`, `range_remaining`), OAuth with 1-hour tokens, HMAC-signed webhooks; but ~2x cost, post-acquisition fleet focus, and uncertain individual eligibility.
3. Everything else (Vyncs, Vinli, Carly, Mojio/SyncUP DRIVE, Hum, MOTOsafety, Linxup/Spytec, Kayo) either has no owner-accessible API or no longer exists as a consumer product.
4. **Before buying:** the decisive unknown is whether these 2021 Fords emit fuel on a PID the dongle reads. Buy one Bouncie ($89.99 + $8), test in both vehicles for a week, and check `stats.fuelLevel` is non-null and tracks the gauge; fall back to Zubie if not.
