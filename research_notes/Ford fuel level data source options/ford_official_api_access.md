# Official Ford channels for programmatic fuel-level access (individual US owner, 2021 F-250 gas + 2021 Expedition, FordPass Connect) — status as of 2026-10-25

Scope notes for the report writer:
- Smartcar deliberately excluded (covered elsewhere).
- Unofficial FordPass reverse-engineering (fordpass-ha, FordPass Scriptable widget, etc.) is excluded as not owner-authorized/sanctioned; it is mentioned only where it dates the official API's history. One source notes Ford disabled the unofficial API surface in early October 2023 — [MacheForum post](https://www.macheforum.com/site/threads/fordpass-scriptable-widget-v2022-xx-ios-ipad-macos.14658/post-700707).
- developer.ford.com is a client-rendered SPA; every direct fetch of its pages (`/apis`, `/apis/fordconnect-query`, `/use-cases/...`) returned only the title "Ford Developer Marketplace." All FordConnect Query specifics below therefore come from community integrations and secondary indexes and are marked accordingly. Treat the headline finding (individual registration reopened) as **strongly indicated but not verified against Ford's own text**; the user should log in to developer.ford.com/developer-na to confirm.

---

## KQ1. Has Ford reopened FordConnect / developer.ford.com to individuals or small developers in 2025–2026? Process, requirements, pricing, sandbox?

### Takeaway
Very likely yes, under a renamed product: **"FordConnect Query"** (consumer, OAuth 2.0 authorization-code, read-only vehicle data incl. fuel level) is live on developer.ford.com with a North America portal and an explicit "This app is for personal use" option; community integrations built on it launched January 2026 and NA users reported it working March 2026. This supersedes the Nov 2024 Ford support refusal. No pricing (free developer account), no documented sandbox, no business-entity requirement found.

### Cited Findings
**History / the refusal the premise rests on**
- Nov 5, 2024: Ford support reply posted by a MacheForum member: "Unfortunately for now a decision has been made to no longer provide credentials and access for FordConnect... If this changes in the future, we will update developer.ford.com." Another member said many users received the same message. — [MacheForum "Ford Developer API" thread](https://www.macheforum.com/site/threads/ford-developer-api.40081/) (confirmed, dated)
- Undated forum post: developer portal registration showed "At this time, we are no longer accepting sign-ups for the FordConnect API." — [MacheForum post](https://www.macheforum.com/site/threads/fordpass-scriptable-widget-v2022-xx-ios-ipad-macos.14658/post-803785) (confirmed quote, undated)
- Nov 4, 2024: a self-identified former FordPass engineer said the public telemetry feeds were "not" meant for individuals and the developer page was "entirely incorrect and misleading"; disputed by another member citing public docs. — [MacheForum thread](https://www.macheforum.com/site/threads/ford-developer-api.40081/) (opinion, dated)
- Same thread: a Chicago-based member said he polled every 20 minutes ("well within the documented guidelines") and had access revoked anyway (2024 era). — [MacheForum thread](https://www.macheforum.com/site/threads/ford-developer-api.40081/) (anecdote, 2024)

**FordConnect Query — the current product (2025–2026)**
- Ford's listing (as indexed by search): FordConnect Query "enables developers to securely request data from connected vehicles with explicit customer authorization," using "industry-standard OAuth 2.0 authorization code flow and granular scopes." — [developer.ford.com/apis](https://developer.ford.com/apis) (confirmed via search index; page itself not fetchable)
- Ford publishes consumer-oriented use-case pages for it: "Connect Ford to daily tracking apps" and "Add Ford to smart home products." — [developer.ford.com use case 1](https://developer.ford.com/use-cases/connect-ford-to-daily-tracking-apps), [use case 2](https://developer.ford.com/use-cases/add-ford-to-smart-home-products) (confirmed URLs exist; content not fetchable)
- Independent API index (scored 2026-10-23): FordConnect Query covers consumer vehicle data and remote command access, consent via FordPass-account OAuth; "access requires a partner application and credentials issued through the Ford Developer Portal"; Ford "has periodically restricted individual-developer access"; onboarding/pricing "unknown", trial "no" (low confidence). — [apis.io FordPass/Ford Pro profile](https://apis.io/providers/fordpass/) (secondary index)
- Jan 9, 2026: Home Assistant integration "FordConnect-Query" released by marq24 ("I do not have any connection to Ford"); requires a Ford Developer Account, a registered Ford application, clientId/clientSecret. — [HA Community thread](https://community.home-assistant.io/t/fordconnect-query-integration-a-read-only-alternative-to-fordpass/972684) (confirmed, dated)
- Feb 18, 2026: MacheForum member recommends "FordConnect Query" to another member wanting a free way to pull simple car info. — [MacheForum thread](https://www.macheforum.com/site/threads/ford-developer-api.40081/) (dated)
- Mar 18, 2026: user "Skillet" reported the integration working in North America (v2026.3.4). — [HA Community thread](https://community.home-assistant.io/t/fordconnect-query-integration-a-read-only-alternative-to-fordpass/972684) (confirmed, dated)
- Jun 17, 2026: marq24 stated Ford offers the API "right now" for "EU / USA / CAN only"; an Australian user got HTTP 500 "Error validating provided info" at OAuth (Jun 14, 2026). — [HA Community thread](https://community.home-assistant.io/t/fordconnect-query-integration-a-read-only-alternative-to-fordpass/972684) (dated)
- evcc (EV charging) users also onboarded via developer.ford.com in 2026; report a multi-day Ford-side outage in June 2026 and an AADB2C90027 token error fixed by re-authorizing. — [evcc discussion #25639](https://github.com/evcc-io/evcc/discussions/25639) (dated 2026)

**Registration process (from the community guide — the only step-by-step found)**
- Free Ford developer account at developer.ford.com (MFA required). Profile name must contain only letters/digits/spaces (accented characters break credential generation). — [REGISTER_APPLICATION.md](https://github.com/marq24/ha-fordconnect-query/blob/main/docs/REGISTER_APPLICATION.md); accented-character failure also reported Feb 1, 2026 in [HA thread](https://community.home-assistant.io/t/fordconnect-query-integration-a-read-only-alternative-to-fordpass/972684)
- Regional app-registration portals: EU `developer.ford.com/developer-eu`, UK `developer.ford.com/developer-uk`, **North America `developer.ford.com/developer-na`**. — [REGISTER_APPLICATION.md](https://github.com/marq24/ha-fordconnect-query/blob/main/docs/REGISTER_APPLICATION.md)
- Form has a checkbox **"Yes - This app is for personal use"**; fields: application name, description, purpose; select vehicle types; redirect URL(s) must be `localhost` or publicly reachable (local IPs error); select "All Available Data Categories"; CAPTCHA; Submit; Client ID/Secret displayed. Wait at least ~2 hours for Ford to process (some users waited hours or days). — [REGISTER_APPLICATION.md](https://github.com/marq24/ha-fordconnect-query/blob/main/docs/REGISTER_APPLICATION.md)
- One user (Feb 1, 2026) saw a 400 error referencing a `businessName` parameter and resolved it by entering their full name in the "company" field of a personal developer profile. — [HA thread](https://community.home-assistant.io/t/fordconnect-query-integration-a-read-only-alternative-to-fordpass/972684)
- Vehicle-side requirements: latest onboard modem; vehicle registered in FordPass (or Lincoln Way); owner completes account-link/consent via FordPass login in the OAuth flow. — [ha-fordconnect-query README](https://github.com/marq24/ha-fordconnect-query/blob/main/README.md); account-link flow also described in [fordconnect-node-red](https://flows.nodered.org/node/@tally/fordconnect-node-red)
- Redirect-URL gotchas: ports in redirect URL cause "404 Error validating provided info"; multiple comma-separated redirect URLs don't work despite docs; plain HTTP only for localhost (HTTPS otherwise). — [evcc discussion #25639](https://github.com/evcc-io/evcc/discussions/25639)

**Data exposed (relevant to fuel)**
- README data table lists, for petrol/diesel/PHEV: **Fuel level (noted as "can exceed 100%")**, plus odometer, 12V battery, oil, tire pressure, GPS, ignition, doors/windows, speed, gear, warnings, temperatures, belt status. **Range is not listed.** — [ha-fordconnect-query README](https://github.com/marq24/ha-fordconnect-query/blob/main/README.md)
- Older FordConnect node set exposed: vehicle list, vehicle info, refresh vehicle status, location, refresh location; access tokens last 20 minutes; Ford advises limiting polling because it affects vehicle battery life. — [fordconnect-node-red](https://flows.nodered.org/node/@tally/fordconnect-node-red) (v1.1.2, ~2023–2024)
- Jun 28, 2026: a user reported seeing only some sensors (tire pressure etc. missing) vs. the README. — [HA thread](https://community.home-assistant.io/t/fordconnect-query-integration-a-read-only-alternative-to-fordpass/972684)

**Rate limits / cost**
- The HA integration polls every 60 s by default (cloud polling), with no reported bans in-thread; evcc users hit **HTTP 429** on repeated garage requests and settled on ~30 s spacing and a 900 s polling interval. No official quota numbers surfaced. — [README](https://github.com/marq24/ha-fordconnect-query/blob/main/README.md); [evcc #25639](https://github.com/evcc-io/evcc/discussions/25639)
- No Ford fee mentioned anywhere; developer account described as free. — [REGISTER_APPLICATION.md](https://github.com/marq24/ha-fordconnect-query/blob/main/docs/REGISTER_APPLICATION.md)

### Inferences
- The sequence (Nov 2024 refusal → Jan 2026 personal-use integration launch → Mar 2026 NA success → Jun 2026 "EU/USA/CAN only") strongly indicates Ford **relaunched** consumer developer access as "FordConnect Query" sometime between late 2024 and late 2025, with a personal-use tier. The "already established" premise (no individual credentials) appears to be stale as of 2026. (inference)
- A 2021 F-250 and 2021 Expedition with FordPass Connect (2020MY+ embedded modem) should meet the "latest onboard modem" requirement; both are gas, so the fuel-level field should apply. (inference; not vehicle-specifically confirmed)
- For FordFuelAlert's `PercentRemaining` 0–100 model, clamp fuel level at 100 since the API can report >100%. (practical inference from README note)
- A 2-hour poll cadence is far below any observed throttling (60 s polling tolerated; 429s only on burst garage calls). (inference)
- No sandbox tier was mentioned by any source; the EV/charging communities test against live vehicles. (inference from absence)

### Gaps
- Could not read Ford's own FordConnect Query terms, scopes list, documented rate limits, or any statement of eligibility (SPA pages unreadable without a browser session). Unknown whether Ford's ToS restricts personal-use apps to a single account's vehicles.
- No source confirmed whether the personal-use tier can be revoked at Ford's discretion as happened in 2024.
- No explicit confirmation of the fuel-level field on a 2021 F-250/Expedition specifically.
- No official launch date or press release for FordConnect Query found (media.ford.com not surfaced in searches).

---

## KQ2. Ford Pro Telematics / Ford Pro Intelligence / Ford Pro Data Services API — can a 1–2 vehicle owner subscribe? Cost, fuel level, API, onboarding?

### Takeaway
Ford Pro Telematics is a commercial-fleet product priced at **$20/vehicle/month** (US, 2026 dealer pages) with no stated fleet minimum (Ford markets to "small fleets (1–20 vehicles)"); the separate paid **Ford Pro Data Services** API delivers "remaining fuel" to third-party software and is sold to partners/enterprises, not advertised to individuals. Nothing found says a private individual (non-business) qualifies, and pricing for Data Services is quote-only.

### Cited Findings
- Ford Pro Telematics "requires a purchased subscription"; Essentials is complimentary; "Ford Pro Data Services is a separate paid subscription for third-party telematics and enterprise software" whose "API delivers manufacturer-grade data including driver behavior, trip reports, vehicle health and more." Page © 2026, build v4.6.270. — [fordpro.com/en-us/telematics](https://www.fordpro.com/en-us/telematics/) (confirmed, official)
- US dealer page reproducing Ford Pro's table: Telematics Essentials "Complimentary for the life of the vehicle"; Ford Pro Telematics **$20/mo. per vehicle** (features: dashboard, vehicle health, dealer scheduling, maintenance tracking, GPS tracking/geofencing, **fuel efficiency analysis**, Drive App, trip history, non-Ford support, in-vehicle coaching, security); plug-in modem kit $129 for non-modem vehicles; no plug-in needed on 2020MY+ modem-equipped Ford commercial vehicles. — [Ed Murdock Ford / WorkTruckSolutions Ford Pro page](https://edmurdockford.worktrucksolutions.com/p/telematics) (dealer mirror of official content, 2026)
- Older conflicting price: a Mar 2024 report put the paid package at $27/vehicle/month. — [Teslarati](https://www.teslarati.com/ford-pro-e-telematcis-subscription-commercial-electric-vehicles/) (2024; superseded by 2026 $20 listing)
- A 2020-era report: Ford fleet-management software available "for as low as $5 per month per vehicle" (small-business suite). — [ForConstructionPros](https://www.forconstructionpros.com/construction-technology/project-management/product/22445178/ford-motor-company-ford-offers-fleet-management-suite-for-small-businesses) (dated; likely obsolete pricing)
- UK/EU paid tier: £15 / €18 per vehicle. — [Ford UK telematics](https://www.ford.co.uk/commercial-vehicles/telematics) (non-US, context only)
- Ford Pro Data Services (dealer page reproducing Ford content): data from the factory modem, "businesses of all sizes and fleet makeup"; **vehicle data includes odometer, VIN, remaining fuel, refined GPS**; vehicle health (oil life, tire pressure, engine temp, battery voltage, faults); driver behavior; "developer-friendly API connects third-party software"; 21 partners listed: Azuga, Connexion, Dealerware, Arsloaner, Fleet Complete, CerebrumX, Cox Automotive, Intellishift, Geotab, Fleetio, Merchants Fleet, Mix Telematics, Motive, Motorq, Samsara, Teletrac Navman, TSD, Verizon Connect, Vinli, Spireon, Velocitor. No pricing, no enrollment steps (sales form). — [Five Star Ford / WorkTruckSolutions Data Services page](https://5starford.worktrucksolutions.com/p/data-services) (dealer mirror)
- Independent index of the "Ford Pro REST API" resource groups: Vehicle Status, Vehicle Historical Status, Vehicle Metadata, Vehicle Efficiency (fuel/energy/idle), Driver Behavior, Trip Reports, Vehicle Health. — [apis.io](https://apis.io/providers/fordpass/) (secondary)
- Ford Pro small-fleets page defines "Small Fleets (1–20 Vehicles)"; "Ford Pro Telematics and Ford Pro Data Services require a purchased subscription"; first step is creating a Ford Pro account; does not state whether individuals/sole proprietors qualify. — [fordpro.com small fleets](https://www.fordpro.com/en-us/solutions/small-fleets/) (official, © 2026)
- Ford Pro Marketplace account is "required to access and control Ford Pro products and services"; created via the Ford Pro website Log In / Sign Up. — [Ford UK support: create Ford Pro Marketplace account](https://www.ford.co.uk/support/how-tos/support-search-only/announcement/how-do-i-create-a-ford-pro-marketplace-account) (non-US page; process likely similar)
- Canadian Telematics Essentials job aid: enrollment requires company name and address; FIN code and fleet size optional; VINs uploaded then vehicle must be driven within 24 h; quarterly attestation of ownership in Fleet Marketplace. — [Ford Pro CA job aid PDF](https://content.fordpro.com/content/dam/fordpro/ca/en-ca/pdf/telematics/Ford-Pro-Telematics-Essentials-Training-Job-Aid.pdf) (official, Canada)
- Third-party aggregator description of Ford Pro feed: "Fuel level and EV battery state of charge" exposed; "Subscription tiers determine which data fields are exposed"; refresh intervals and API rate limits "vary by plan"; enrollment is OAuth-connecting a Ford Pro account. — [Navixy Ford Pro integration](https://navixy.com/learn-more/oem-telematics/ford-pro-integration) (vendor)
- Data-quality caveat: NHTSA-filed Ford TSB (PDF created May 13, 2026; search index cites TSB 26-2220 dated May 7, 2026) for 2023–2025 F-Super Duty: fuel level shows 0% or "Unknown" in Ford Pro Telematics Drive App, Telematics software, and PTS even with fuel in tank. — [NHTSA TSB MC-11032696-0001](https://static.nhtsa.gov/odi/tsbs/2026/MC-11032696-0001.pdf) (official; body not text-extractable here, details from search index — treat affected-year list as unverified)

### Inferences
- A sole proprietor could plausibly open a Ford Pro account (company name + address is all the Canadian job aid requires; fleet size optional) and pay $20/vehicle/month for Telematics, but that product is a dashboard/app, **not an API**; programmatic fuel access requires Ford Pro Data Services, which is sold through partners/sales and has no published price. (inference)
- Ford Pro Data Services is positioned B2B2B (Ford → TSP → fleet); an individual would almost certainly be routed to a partner (see KQ4) rather than given direct API credentials. (inference)
- The 2023–2025 Super Duty fuel-level TSB does not list 2021 F-250, but it shows the Ford Pro fuel field has had reliability issues on Super Duty. (inference)

### Gaps
- No US-specific statement of Ford Pro eligibility for private individuals or whether a business entity/EIN is required (UK/CA pages only).
- Ford Pro Data Services pricing and direct-developer onboarding not public anywhere found.
- Ford Pro Telematics API documentation (if any public) not found; apis.io resource list is the only schema-level view.
- Could not confirm whether the $20 tier includes a *fuel level* field (only "fuel efficiency analysis" named).

---

## KQ3. Ford Pro Telematics Essentials (free tier) — still exists, eligibility, data, API?

### Takeaway
Still exists in 2026, "complimentary for the life of the vehicle," for 2021MY+ (and some 2020MY) Ford/Lincoln vehicles with embedded modems enrolled through a Ford Pro (fleet) account; it is a dashboard product aimed at commercial customers with **no API** found, and fuel-level inclusion in the free tier is unconfirmed.

### Cited Findings
- Essentials is complimentary and available on 2021 model year or newer Ford and Lincoln vehicles and certain 2020MY vehicles with an embedded modem; modem activation required; excludes late-2025 Transit with 58U/V radios and certain 2026 F-650/750 and E-Series. — [fordpro.com/en-us/telematics](https://www.fordpro.com/en-us/telematics/) (official, 2026)
- "Complimentary for the life of the vehicle"; upgrading from Essentials to paid requires re-enrollment and two ignition cycles. — [Ed Murdock Ford Ford Pro page](https://edmurdockford.worktrucksolutions.com/p/telematics) (2026 dealer mirror)
- Launch (Nov 2021): available to "all commercial customers using gas-powered Ford vehicles with embedded modems," free for the life of the vehicle; EVs get E-Telematics (3 years complimentary at launch). — [FleetOwner](https://www.fleetowner.com/technology/article/21181019/ford-pro-offers-free-fleet-telematics-for-gas-and-electric-commercial-vehicles); [Work Truck Online](https://www.worktruckonline.com/news/ford-pro-offers-complimentary-access-to-oem-telematics-services) (dated 2021)
- Enrollment via fleet portal: eligible vehicles 2020MY+ with embedded modem; company name/address required; FIN and fleet size optional; vehicle must be driven within 24 h; quarterly ownership attestation. — [Ford Pro CA job aid](https://content.fordpro.com/content/dam/fordpro/ca/en-ca/pdf/telematics/Ford-Pro-Telematics-Essentials-Training-Job-Aid.pdf) (official, Canada)
- EV trial: eligible 2024–2027 Ford vehicles get complimentary E-Telematics trial, annual service contract after. — [fordpro.com/en-us/telematics](https://www.fordpro.com/en-us/telematics/) (not applicable to gas F-250/Expedition)

### Inferences
- An individual who opens a Ford Pro account as a sole proprietor could likely enroll two 2021 vehicles in Essentials at $0, but would get a web dashboard, not programmatic access; the quarterly attestation implies Ford expects a commercial owner. (inference)
- Expedition is a retail SUV, not a "commercial vehicle"; the fordpro.com page says "Ford and Lincoln vehicles" 2021MY+, so it may be eligible, but commercial-customer framing persists. (inference)

### Gaps
- No feature list for the Essentials tier found on 2026 pages (fuel level in/out unknown).
- No evidence of any API or export surface on Essentials.
- US-specific eligibility for non-business individuals not stated.

---

## KQ4. Ford-approved third-party telematics partners receiving Ford OEM data without a dongle — can an individual subscribe? Costs?

### Takeaway
Geotab, Samsara, Motive, Verizon Connect, Azuga, Fleetio and ~15 others receive Ford embedded-modem data via Ford Pro Data Services; all require a Ford Pro (fleet) account plus the partner's own fleet subscription, pricing is quote-only (Geotab's generic OEM-partner plan benchmark is ~$3–3.50/month on top of a base plan; industry total ~$20–45/vehicle/month), and no partner was found that sells to a private individual for a personal vehicle.

### Cited Findings
- Ford Pro Data Services partner list (21 names incl. Geotab, Samsara, Motive, Verizon Connect, Azuga, Fleetio, Fleet Complete, Spireon). — [Five Star Ford Data Services page](https://5starford.worktrucksolutions.com/p/data-services)
- Geotab "Integrated Solution for Ford Vehicles": no additional hardware; US/Canada/EU Ford & Lincoln only; most Q4-2019+ models eligible after Ford validation; 2026 E-Series not eligible; sold via MyAdmin on "Ford Premium" and "Ford Basic" plans (Basic data only at ignition on/off); customer must activate the Ford modem and add consent on the Ford portal with the correct Fleet Identification Number (FIN); pricing in MyAdmin rate plans (not public); billed monthly, prorated. — [Geotab Ford partner doc](https://support.geotab.com/oem-integration/doc/ford-partner) (vendor, © 2026)
- Geotab/Ford launch: "Ford Data Services will begin secure data transfers from Ford vehicles with a factory-installed modem to Geotab's cloud-based platform," starting 2020MY. — [Work Truck Online (2019)](https://www.worktruckonline.com/341599/geotab-adds-ford-vehicle-data-to-telematics-platform)
- Geotab's generic OEM/3rd-party-device rate table (July 2023): $3.00/month (Base/Regulatory/Pro), suggested MSRP $3.50 — not Ford-specific. — [Geotab Data Connector Partner Guide](https://support.geotab.com/mygeotab/mygeotab-add-ins/doc/data-conn-partner)
- Samsara: integration via Ford Data Services for eligible Ford vehicles MY2018+, "avoid installing additional hardware by leveraging data from Ford-embedded telematics devices." — [Samsara marketplace Ford Pro](https://www.samsara.com/uk/resources/marketplace/ford); [Samsara blog](https://www.samsara.com/blog/ford)
- Motive: pulls GPS, engine hours, odometer, fault codes, **fuel levels** from Ford Pro; "requires an active subscription to Motive and Ford Pro"; 2020+ Ford models; US & Canada. — [Motive marketplace Ford Pro](https://marketplace.gomotive.com/app/ford-pro)
- Fleetio lists a Ford Pro integration in its directory. — [Fleetio Ford Pro](https://www.fleetio.com/integrations-directory/fleet-telematics/ford-pro)
- Navixy: Ford Pro feed includes fuel level; fields depend on Ford Pro subscription tier; OAuth-connect a Ford Pro account. — [Navixy](https://navixy.com/learn-more/oem-telematics/ford-pro-integration)
- Ford Fleet Marketplace lets fleets "activate either third-party TSP platforms or Ford Telematics with a mouse click"; partners named incl. Verizon Connect, Spireon, Geotab, Fleet Complete, Samsara. — [Business Fleet](https://www.businessfleet.com/10124888/ford-takes-telematics-in-house-and-hardware-free) (2020)
- Industry benchmark: typical fleet telematics $20–45/vehicle/month. — [GPS Insight pricing guide (2026)](https://www.gpsinsight.com/blog/what-is-the-cost-of-telematics/)

### Inferences
- Every partner path requires (a) a Ford Pro/Fleet account with a FIN and (b) a paid TSP contract; TSPs sell to fleets with sales-led contracts (Samsara/Geotab/Motive typically multi-year). A private owner of two personal vehicles is outside their target and would at minimum need to present as a business. (inference)
- Most partner APIs (Geotab, Samsara, Motive) are themselves programmable, so this path *would* expose fuel level to the owner's software — but at a realistic $25–50+/vehicle/month total and with sales friction. (inference)

### Gaps
- No partner publishes per-vehicle Ford OEM-data pricing or a minimum fleet size.
- No source confirms or denies that any partner will sign a one- or two-vehicle individual.
- Verizon Connect and Azuga Ford-specific pages were not fetched.

---

## KQ5. Official Ford consumer integrations with a sanctioned automation surface (Alexa, Google Assistant, IFTTT, SmartThings, Siri Shortcuts, FordPass web, Power-Up)?

### Takeaway
The official FordPass Alexa skill (and a Google Assistant action, per 2020–21 forum lists) supports voice queries such as range, odometer, tire pressure, lock/unlock, remote start, and EV charge level, but none of these is a programmatic surface; no FordPass IFTTT, SmartThings, or Siri Shortcuts integration was found.

### Cited Findings
- Forum-compiled FordPass Alexa commands include asking for **range**, odometer, tire pressure, lock/unlock, start/stop, vehicle selection; "charge level" only for compatible EVs; requires FordPass app with vehicle registered, skill enabled, PIN for secured actions. — [Ranger5G thread](https://www.ranger5g.com/forum/threads/use-alexa-to-start-your-ranger-more.27254/post-629869); [MacheForum Alexa/Google Home thread](https://www.macheforum.com/site/threads/fordpass-alexa-skill-google-home.2540/post-93503) (community, 2020–2021)
- A 2022 F-150 owner initially got "This vehicle is not compatible" until re-linking the account. — [F150gen14 forum](https://www.f150gen14.com/forum/threads/ecu-module-programming-guide-using-fdrs.8249/post-263983) (anecdote)
- Searches for FordPass + IFTTT / SmartThings / Apple Shortcuts returned only generic IFTTT–Shortcuts pages with no FordPass service. — [IFTTT Shortcuts page](https://ifttt.com/explore/integrate-apple-shortcuts) (absence of evidence)

### Inferences
- Voice assistants are not usable for a scheduled, unattended fuel check; their only automation value would be an Alexa Routine, which cannot read a skill's response into another system. (inference)
- Ford's sanctioned consumer automation surface is now FordConnect Query (KQ1) — its own use-case page is literally "Add Ford to smart home products." (inference)

### Gaps
- Ford's official FordPass Alexa skill documentation page was not reached; fuel-level (vs. range) voice support unconfirmed.
- No sources on Ford Power-Up or a FordPass web app exposing data.

---

## KQ6. Ford dealer programs, Ford Credit, FordPass Rewards exposing vehicle data?

### Takeaway
No source surfaced any dealer, Ford Credit, or FordPass Rewards channel that exposes vehicle telemetry (fuel) to owners programmatically; the only dealer-adjacent data products found are Ford Pro Data Services partners "Dealerware" and "Cox Automotive" (dealer loaner/fleet use).

### Cited Findings
- Dealer-oriented names in the Ford Pro Data Services partner list: Dealerware, Arsloaner, Cox Automotive, TSD (loaner-fleet software). — [Five Star Ford Data Services page](https://5starford.worktrucksolutions.com/p/data-services)
- Ford Pro Telematics includes "Dealer Scheduling" and "Maintenance and Service Tracking" features (dealer-facing data flow, not owner API). — [Ed Murdock Ford page](https://edmurdockford.worktrucksolutions.com/p/telematics)

### Inferences
- These are B2B dealer tools; no owner-facing data access exists through dealer, Ford Credit, or Rewards programs. (inference from absence)

### Gaps
- No searches returned anything on Ford Credit or FordPass Rewards data access; treat as "none found," not "confirmed none."

---

## KQ7. Right-to-repair telematics laws (MA, ME) — Ford's position and any owner-facing data platform in 2026?

### Takeaway
No owner-facing telematics data platform has launched from either law as of Oct 2026: the Massachusetts law is in force but under First Circuit appeal (argued Feb 3, 2026; no decision found through late Aug 2026; the court urged mediation), and Maine's law survived a Jan 7, 2026 veto of weakening amendments while its "independent entity" has not been stood up and industry litigation continues. Ford's individual position is not documented; it acts through the Alliance for Automotive Innovation.

### Cited Findings
- MA: Feb 2025 district court dismissed Auto Innovators' remaining claims; Alliance appealed to the First Circuit (No. 25-1262). — [Seyfarth](https://www.seyfarth.com/news-insights/judge-denies-industry-challenge-to-massachusetts-data-access-law.html); [Nelson Mullins Oct 7, 2025](https://www.nelsonmullins.com/insights/blogs/driving-forward-developments-in-transportation-law-and-innovation/all/2026-shaping-up-to-be-big-year-for-access-to-vehicle-generated-telematics-data)
- Feb 3, 2026 oral argument: panel partly skeptical (one judge "troubled"), urged mediation/technical cybersecurity solution; parties agreed to consider settlement (Feb 10, 2026). — [Nelson Mullins](https://www.nelsonmullins.com/insights/blogs/driving-forward-developments-in-transportation-law-and-innovation/all/first-circuit-appears-skeptical-about-aspects-of-mass-data-law-urges-negotiated-resolution); [Repairer Driven News Feb 10, 2026](https://www.repairerdrivennews.com/2026/02/10/auto-innovators-massachusetts-ag-agree-to-consider-data-access-law-suit-settlement/)
- By Aug 2026 the MA AG rebuffed compromise and both sides still awaited a decision; Auto Innovators said mid-Aug it would keep pressing the Maine challenge. — [Nelson Mullins "proceeds cautiously"](https://www.nelsonmullins.com/insights/blogs/driving-forward-developments-in-transportation-law-and-innovation/all/automotive-right-to-repair-proceeds-cautiously-while-awaiting-first-circuit-decision) (latest found ~Aug 24, 2026)
- MA law requires, from MY2022, an "inter-operable, standardized and open access platform" accessible by owner-authorized mobile app. — [Wikipedia 2020 MA Question 1](https://en.wikipedia.org/wiki/2020_Massachusetts_Question_1); [Fleet Maintenance summer 2026 update](https://www.fleetmaintenance.com/shop-operations/data-and-telematics/article/55380097/right-to-repair-update-for-summer-2026)
- Maine: 2023 ballot law (effective Jan 5, 2025) directs the AG to designate an "independent entity"; legislature passed LD 1228 (June 2025) replacing it with a commission; Gov. Mills **vetoed Jan 7, 2026**; Auto Innovators sued Jan 2025 arguing the entity doesn't exist. — [Repairer Driven News Jan 8, 2026](https://www.repairerdrivennews.com/2026/01/08/maine-governor-vetoes-automotive-right-to-repair-bill/); [Nelson Mullins veto note](https://www.nelsonmullins.com/insights/blogs/driving-forward-developments-in-transportation-law-and-innovation/all/maine-governor-vetoes-amended-right-to-repair-law)
- Federal REPAIR Act (H.R. 1566) reintroduced Feb 2025; no further action reported. — [Nelson Mullins Oct 7, 2025](https://www.nelsonmullins.com/insights/blogs/driving-forward-developments-in-transportation-law-and-innovation/all/2026-shaping-up-to-be-big-year-for-access-to-vehicle-generated-telematics-data)
- No source mentions Ford specifically or any automaker launching an owner-facing data platform. — [Nelson Mullins](https://www.nelsonmullins.com/insights/blogs/driving-forward-developments-in-transportation-law-and-innovation/all/2026-shaping-up-to-be-big-year-for-access-to-vehicle-generated-telematics-data) (absence)

### Inferences
- Neither law gives a US owner outside MA/ME a usable API today, and even in-state the mandated platform doesn't exist; these laws are not a 2026 fuel-data path. (inference)
- FordConnect Query's consumer OAuth consent model is the closest thing Ford has to the owner-authorized access these laws contemplate. (inference)

### Gaps
- No First Circuit decision found after Aug 24, 2026; a ruling may have issued in Sept–Oct 2026.
- Ford's own filings/statements on MA/ME not located (acts via the Alliance).

---

## Cross-question practical summary for FordFuelAlert (synthesis, inferred unless cited)

| Channel | Open to a private individual w/ 2 vehicles? | Cost | Fuel level? | Programmatic? | Status date |
|---|---|---|---|---|---|
| FordConnect Query (developer.ford.com/developer-na) | Very likely yes ("personal use" checkbox) — [guide](https://github.com/marq24/ha-fordconnect-query/blob/main/docs/REGISTER_APPLICATION.md) | Free (no fee found) | Yes (gas/diesel; may exceed 100%) — [README](https://github.com/marq24/ha-fordconnect-query/blob/main/README.md) | Yes, OAuth 2.0 REST | Working in NA Mar 2026; "EU/USA/CAN" Jun 2026 |
| Ford Pro Telematics ($20) | Needs Ford Pro account (company name); individual eligibility unstated | $20/veh/mo — [dealer page](https://edmurdockford.worktrucksolutions.com/p/telematics) | Fuel efficiency; field unconfirmed | No (dashboard/app) | 2026 |
| Ford Pro Telematics Essentials | Same account; commercial framing | $0 | Unconfirmed | No | 2026 |
| Ford Pro Data Services API | B2B via partners; no individual path found | Quote only | Yes ("remaining fuel") — [page](https://5starford.worktrucksolutions.com/p/data-services) | Yes, via partner | 2026 |
| Geotab/Samsara/Motive OEM feed | Requires Ford Pro FIN + TSP contract | Quote; ~$20–45+/veh/mo industry | Yes (Motive, Navixy confirm) | Yes via TSP API | 2026 |
| Alexa/Google FordPass skill | Yes | Free | Range (fuel unconfirmed) | No | 2020–21 docs |
| MA/ME right-to-repair platforms | No platform exists | n/a | n/a | n/a | Oct 2026 |
| Dealer/Ford Credit/Rewards | None found | n/a | n/a | n/a | — |

Recommended verification step for the user: sign in at developer.ford.com → developer-na → attempt app registration with "personal use" checked, and confirm the fuel-level field on each VIN before any code change. The repo's `IFuelService` seam is already the right place to add a `FordConnectQueryFuelService` if confirmed.
