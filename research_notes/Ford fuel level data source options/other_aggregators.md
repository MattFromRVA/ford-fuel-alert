# Connected-car aggregators other than Smartcar for Ford ICE fuel level (US, private owner, 2021 F-250 / 2021 Expedition) — as of 2026-10-25

Scope note: Smartcar, High Mobility, Enode, Tronity, FordConnect direct credentials, and reverse-engineered FordPass endpoints were already covered by other researchers and are only mentioned here where another vendor depends on them. Labels used below: **[confirmed]** = stated on a primary/vendor source; **[inferred]** = reasoned from cited facts; **[unverified]** = could not be sourced.

## Key Question 1: Which aggregators have Ford ICE fuel-level support in the US, and do they accept individuals / tiny accounts in 2026?

### Takeaway
Every legitimate non-Smartcar aggregator with US Ford fuel data (Motorq, Navixy, Vinli, CerebrumX, Geotab/Samsara/Fleetio etc.) gets it through **Ford Pro Data Services**, Ford's commercial-fleet data product — a path that is fleet/business-oriented and "subscription required / contact sales". The one consumer-facing non-Smartcar platform that lists Ford (DIMO) is actually a Smartcar wrapper, so it inherits the same Ford web-login failure. No aggregator was found that offers an individual US owner self-serve, pay-as-you-go Ford ICE fuel data via a consent path other than Smartcar/FordPass login.

### Cited Findings

**Ford's own commercial channel (the upstream for most aggregators)**
- Ford Pro Data Services lists "odometer readings, VIN, remaining fuel, and refined GPS location" among its vehicle data and says "Starting with the 2020 model year, all Ford commercial vehicles come factory fitted with a Ford-engineered modem." It "serves businesses of all sizes and fleet makeup." Integrated third-party providers listed: Azuga, Connexion, Dealerware, Arsloaner, Fleet Complete, CerebrumX, Cox Automotive, Intellishift, GeoTab, Fleetio, Merchants Fleet, Mix Telematics, Motive, Motorq, Samsara, Teletrac Navman, TSD, Verizon Connect, Vinli, Spireon, Velocitor; plus "a developer API that connects third-party software to Ford Pro Data Services." No pricing, no consent-flow description, no mention of individual consumers. [confirmed] — [Ford Pro Data Services (dealer-hosted Ford Pro page)](https://academyforddealer.worktrucksolutions.com/p/Data-Services)
- Ford Pro Telematics Essentials (free tier): "commercial customers with embedded modem-equipped vehicles (2020 model year and newer) can sign up for this free program." Enrollment is via the New Customer Sign-Up form at fleetaccount.ford.com/customer-signup, where you "enter company information, including company name and address"; FIN code and fleet size are optional; "Ford Pro Intelligence Business Center will review and verify the account within 24 hours." Upgrading to paid Ford Pro Telematics requires completing a contract; cost is $20 per VIN per month. [confirmed] — [Ford Pro Telematics Essentials Training Job Aid (PDF)](https://content.fordpro.com/content/dam/fordpro/ca/en-ca/pdf/telematics/Ford-Pro-Telematics-Essentials-Training-Job-Aid.pdf)
- Dealer pages describe the free Essentials tier as covering vehicle health, dashboard, dealer scheduling and remote lock/unlock, while the $20/mo paid tier adds GPS tracking, geofencing, "fuel efficiency analysis", location/trip history. [confirmed, undated marketing] — [Green Ford Ford Pro Telematics page](https://greenford.worktrucksolutions.com/p/telematics); 2021 press coverage of free program: [FleetOwner, Nov 2021](https://www.fleetowner.com/technology/article/21181019/ford-pro-offers-free-fleet-telematics-for-gas-and-electric-commercial-vehicles)
- A third-party API directory says Ford's partner API program is at developer.ford.com, access "is gated behind a partner application and credentials issued through the Ford Developer Portal; Ford has periodically restricted individual-developer access." [secondary source] — [API Evangelist: FordPass provider page](https://providers.apievangelist.com/providers/fordpass/)
- Ford Pro May 2025 release notes mention "more powerful API access for deeper fleet data integration." [confirmed] — [Ford Pro release notes, May 2025](https://www.fordpro.com/en-us/intelligence/release-notes/may-2025/)

**Motorq**
- Motorq "connects directly to Ford Pro's telematics platform" and ingests data "in near real-time with no additional hardware required on already-connected Ford and Lincoln vehicles." Signal list includes "fuel level", odometer, GPS, DTCs, EV SoC; "Signal availability depends on vehicle make/model/year." Coverage: US, Canada, Europe. Target: Ford and Lincoln fleets (Ford Pro's "commercial vehicle and fleet business"). Pricing: "Subscription required. Contact a Motorq representative for details." Onboarding: "Remote activation with no truck roll or installer visit", book a demo. No API/webhook detail, no update frequency, no model years, no mention of individuals. [confirmed] — [Motorq: Ford OEM connectivity](https://motorq.com/oem-connectivity/ford)
- Dealerware's Motorq-powered Ford/Lincoln integration gives dealers "vehicle telemetry data, such as odometer and fuel level." [confirmed] — [Dealerware support: Ford/Lincoln](https://support.dealerware.com/hc/en-us/articles/6675425690131-Ford-Lincoln)
- Motorq raised $40M in Feb 2022 (enterprise/fleet positioning). [confirmed] — [GlobeNewswire, Feb 2022](https://www.globenewswire.com/de/news-release/2022/2/16/2386077/0/en/Motorq-Raises-40-Million-to-Advance-Connected-Vehicle-Data-Platform.html)

**Navixy (fleet platform, Ford Pro integration — illustrates the Ford Pro consent path)**
- "Connect your Ford Pro account to Navixy via OAuth"; Navixy never receives the Ford password; access can be revoked within Ford Pro. Signals: "Fuel level and EV battery state of charge", odometer, DTCs, maintenance reminders. Data arrives "at the refresh intervals available on the fleet's plan"; "API rate limits and refresh intervals vary by plan"; "Subscription tiers determine which data fields are exposed"; "Model-year restrictions — older vehicles may lack connectivity." No pricing. [confirmed] — [Navixy: Ford Pro integration](https://navixy.com/learn-more/oem-telematics/ford-pro-integration)

**Car IQ / Car IQ Pay**
- Car IQ Pay is a fleet payments product: "Car IQ issues payment credentials directly to the car"; it validates "the identity of the vehicle, its location, and fuel levels" before authorizing fuel purchases; "Cloud-based system requires no new hardware for the fleet customer." Partners are fleet telematics vendors (Intellishift, Azuga, Fleetio, Geotab marketplace). No Ford-specific integration or developer API found. [confirmed, fleet-only] — [Intellishift directory: Car IQ Pay](https://intellishift.com/?p=13577); [Fleetio integrations: Car IQ](https://www.fleetio.com/integrations-directory/fleet-fuel-cards/car-iq); [Geotab Marketplace: Car IQ Pay](https://marketplacetest.geotab.com/solutions/car-iq-pay)
- Element Fleet Management announced a definitive agreement to acquire Car IQ on Dec 2, 2025; Car IQ becomes a subsidiary under Element Mobility; a law-firm deal notice dates the close to Dec 31, 2025. [confirmed] — [Nasdaq/PR: Element acquires Car IQ](https://www.nasdaq.com/articles/element-acquires-car-iq-launch-first-scaled-vehicle-initiated-payments-platform); [Automotive World](https://www.automotiveworld.com/news/element-to-acquire-connected-payments-firm-car-iq/); [McCarthy Tétrault deal notice](https://www.mccarthy.ca/en/experience/car-iq-acquired-by-element-fleet-management-corp)

**Otonomo / Urgent.ly / Urban SDK**
- Otonomo was acquired by Urgent.ly Inc. in a reverse merger completed October 19, 2023 (Urgent.ly 10-K). The FY2024 10-K still describes Otonomo as "an automotive data service platform." [confirmed] — [Urgent.ly 10-K FY2023](https://www.sec.gov/Archives/edgar/data/1603652/000095017024038666/uly-20231231.htm); [Urgent.ly 10-K FY2024](https://www.sec.gov/Archives/edgar/data/1603652/000095017025039411/uly-20241231.htm)
- Before the merger, Otonomo said that in June 2023 it "completed the process of sunsetting part of its connected vehicle data services ('CVD services')" as a cost-reduction step. [confirmed] — [Otonomo Q2 2023 results press release (SEC exhibit)](https://sec.gov/Archives/edgar/data/1842498/000117891323001925/exhibit_99-1.htm)
- Three searches found **no evidence** that Urban SDK acquired Otonomo or any Otonomo asset; the task premise ("acquired by Urban SDK in 2023") appears to be incorrect — the acquirer was Urgent.ly. [confirmed negative for the Urban SDK claim] — see 10-K links above.

**Wejo**
- Joint administrators (Leonard Curtis) were appointed over Wejo Limited on July 10, 2023; all Wejo Group employees made redundant effective that date. [confirmed] — [Prolific North: Wejo entered administration](https://prolificnorth.co.uk/?p=78907)
- Administrators sold IP and technology platforms for about $14M, insufficient to cover ~$54.5M secured debt; "no dividend is expected to be paid to unsecured creditors" (fifth progress report). [confirmed defunct as an operating data business] — [Prolific North: no cash for unsecured creditors](https://www.prolificnorth.co.uk/?p=137162)
- Wejo's main OEM asset was GM data, reportedly sold at a loss (anonymous former-staff claim). [weak] — [Prolific North](https://www.prolificnorth.co.uk/?p=76262)

**Caruso Dataplace (EU)**
- Ford Europe (Ford Smart Mobility) data became available on Caruso's marketplace in Nov 2020; Caruso is a European B2B marketplace. No US offering found. [confirmed EU-only] — [GlobeNewswire, Nov 2020](https://www.globenewswire.com/news-release/2020/11/12/2125478/0/en/CARUSO-Welcomes-New-Car-Data-Supplier-with-Ford-Europe.html)

**Mobilisights (Stellantis)**
- Stellantis-owned data unit (established CES 2023); US integrations such as Samsara cover "eligible MY2018 or newer RAM, Dodge, Chrysler, Jeep models." No Ford coverage. [confirmed N/A] — [S&P AutoTechInsight, CES 2023](https://autotechinsight.spglobal.com/news/5269254/ces-2023-stellantis-establishes-independent-unit-to-generate-revenue-from-vehicle-data); [Samsara Marketplace: Free2Move/Mobilisights](https://samsara.com/resources/marketplace/free2move)

**CerebrumX**
- Nov 2022: CerebrumX to use Ford/Lincoln connected-vehicle data (eligible MY2020+) for usage-based insurance delivered to insurers; data from the factory modem, opt-in, requires owner and OEM consent. Enterprise/insurer-facing; no developer program found. [confirmed enterprise-only] — [IoT For All press release](https://www.iotforall.com/press-releases/cerebrumx-to-use-ford-connected-vehicle-data); [Ford Authority, Nov 2022](https://fordauthority.com/2022/11/ford-connected-vehicle-data-to-be-utilized-by-cerebrumx/)

**Vinli**
- Vinli is listed as a Ford Pro Data Services integration partner (see Ford Pro link above). Its most recent announcement is "Velona", an agentic fleet-management product (Oct 29, 2025, limited early access) built on Databricks — fleet-focused. No consumer/individual Ford data product found. [confirmed fleet focus] — [Ford Pro Data Services partner list](https://academyforddealer.worktrucksolutions.com/p/Data-Services); Vinli history: [S&P AutoTechInsight Series B](https://autotechinsight.spglobal.com/news/5248543/connected-car-platform-developer-vinli-raises-usd135-million-in-series-b-financing-round)

**Mojio / Mojio Force**
- Mojio is an OBD-dongle-based platform (dongle = excluded by the "no dongle" constraint). A Mojio employee pointed an openHAB user to Mojio's simulator; one user reported "Ford F-150 was not supported by Mojio" (older forum thread). Mojio appears still active and privately held, last funding 2022; no Ford OEM-data integration found. [confirmed dongle-based; Ford OEM support unverified] — [openHAB community: Mojio](https://community.openhab.org/t/mojio-connected-car/2446); [PitchBook: Mojio](https://pitchbook.com/profiles/company/59996-35)

**DIMO** (see Key Question 2 for consent-path detail)
- DIMO says developers can access fuel level via its APIs and that it supports Ford among 50+ brands; but "for the vast majority of cars, a hardware connection is required", and software connections exist only where "your car has an app associated with it (and you're subscribed to that app)". [confirmed] — [DIMO Top FAQs, Feb 2023](https://dimo.org/news/top-faqs-february-2023); [DIMO support: what data will I see](https://support.drivedimo.com/en-US/what-data-will-i-see-in-the-mobile-app-with-dimo-macaron-331889)
- DIMO's software connection for brands like Ford is a **Smartcar connection**: the docs describe it as logging in to the automaker's service with Ford named as an example; "a software connection to DIMO requires you to have an active subscription to your automaker's connected vehicle services product"; changing your Ford account password disconnects the vehicle. [confirmed] — [DIMO docs: Smartcar connection](https://docs.dimo.org/support/software-connection/smartcar-connection); [DIMO docs: compatibility FAQ](https://docs.dimo.org/support/faq/compatibility); [DIMO: A new way to connect to DIMO](https://dimo.org/news/a-new-way-to-connect-to-dimo)

**Sibros, Ridecell, Autonomo, Openbay, Honk, Nexar, Carbly, Vehigo, ConnectedCar.io, Autoconnect, Zendrive, Arity, Verisk/LexisNexis exchanges**
- A search for new Ford-OAuth connected-car API startups (2025) surfaced only Smartcar and High Mobility; no entrant named Carbly, Vehigo, ConnectedCar.io, or Autoconnect appeared with Ford OEM data. [confirmed negative search] — [search context: Smartcar Ford partnership Aug 2025](https://smartcar.com/blog/smartcar-partners-with-ford-to-expand-access-to-connected-vehicle-apis); [High Mobility Ford API page now says Ford "yet to be available"](https://www.high-mobility.com/car-api/ford-data-api)
- Arity (Allstate) 2021: Ford/Lincoln owners can opt in to have Arity score Ford-provided driving data; insights are "delivered to insurers via an Arity API". Insurer-facing only. [confirmed] — [Digital Insurance: Ford, Arity UBI partnership](https://www.dig-in.com/news/ford-allstates-arity-enter-ubi-data-partnership); [Coverager](https://coverager.com/arity-partners-with-ford-to-enable-more-efficient-ubi-offerings/)
- CB Insights classifies the "connected vehicle data platforms" market leaders as Smartcar, Continental, Cubic Telecom, Aeris — i.e., the non-Smartcar leaders are OEM/telco infrastructure vendors, not owner-facing APIs. [confirmed] — [CB Insights: Smartcar](https://www.cbinsights.com/company/smartcar)

### Inferences
- The only non-Smartcar route to US Ford ICE fuel data that is live in 2026 is Ford Pro Data Services (directly via Ford Pro Telematics / its developer API, or indirectly via a TSP like Motorq, Navixy, Geotab, Samsara, Fleetio). All of these presume a Ford Pro **business/fleet account**; the sign-up form asks for company name/address and is reviewed by the "Ford Pro Intelligence Business Center". Whether a sole proprietor with one F-250 would be approved is untested. [inferred]
- A 2021 F-250 and 2021 Expedition both fall inside Ford Pro's "2020 model year and newer embedded modem" eligibility, so hardware is not the blocker; account type is. [inferred from Ford Pro MY2020+ statement]
- Motorq, CerebrumX, Vinli, Car IQ are enterprise/fleet sales-led ("contact sales", "subscription required") — not realistic for a one-vehicle personal project. [inferred]
- Caruso and Mobilisights are irrelevant for US Ford (EU-only and Stellantis-only respectively). Wejo is defunct. Otonomo is inside Urgent.ly and had already sunset part of its CVD services in 2023; no evidence of a current self-serve developer product. [inferred]
- The task's "Toyota-style OEM-neutral players" and the long tail of startup names did not surface any Ford-capable US product; I treat them as non-options rather than unknowns, but with low confidence for the obscure names. [inferred]

### Gaps
- No public pricing for Ford Pro Data Services developer API access, Motorq, or Navixy's Ford Pro add-on; all are quote-based.
- Could not confirm whether Ford Pro accepts an individual (non-business) applicant; the job aid only shows business fields. Direct test of fleetaccount.ford.com/customer-signup would be needed.
- Could not retrieve Urgent.ly 10-K text on the current state of the Otonomo data platform (whether it still sells vehicle data to third parties in 2026).
- No sources found at all for Carbly, Vehigo, ConnectedCar.io, Autoconnect, Openbay, Honk, Nexar, Sibros, Ridecell, Autonomo in relation to Ford OEM data; Sibros (OEM OTA/data software), Ridecell (fleet automation), Nexar (dashcams), Honk/Openbay (roadside/repair marketplaces) are, from general knowledge, not vehicle-data aggregators — **unverified in this research**.
- Mojio's current Ford OEM (non-dongle) support could not be verified from its own site.

## Key Question 2: Does each aggregator's Ford connection use the same Ford web login (and hit the same Ford login error) or a different consent path?

### Takeaway
There are exactly two legitimate Ford consent paths in the US: (1) the consumer **FordPass owner login** (used by Smartcar and therefore by DIMO's software connection — same failure mode as Smartcar today), and (2) the **Ford Pro fleet account** consent (OAuth against a Ford Pro account, VIN-based fleet enrollment) used by Motorq, Navixy, Geotab, etc. Insurance programs use a third, Ford-internal opt-in that exposes nothing to the owner.

### Cited Findings
- Smartcar's Ford connection uses the FordPass Connect login flow where owners link their car to an app; Ford vehicles from 2015 onward are listed. [confirmed] — [Smartcar: Ford](https://smartcar.com/ford)
- Smartcar's Aug 6, 2025 Ford partnership integrates data "from vehicles in Europe" (Mustang Mach-E, E-Transit, Puma, Fiesta, Kuga; Ford quote from Ford Pro Software, Europe); "This integration will be available soon." No US change, no consent detail. [confirmed] — [Smartcar blog, Aug 2025](https://smartcar.com/blog/smartcar-partners-with-ford-to-expand-access-to-connected-vehicle-apis)
- DIMO's software connection is a Smartcar connection: user logs in to the automaker's service (Ford given as example); requires an active automaker connected-services subscription; changing the Ford password disconnects the vehicle. [confirmed — same Ford web login as Smartcar] — [DIMO docs: Smartcar connection](https://docs.dimo.org/support/software-connection/smartcar-connection); [DIMO: How a car connects to DIMO](https://dimo.org/news/how-a-car-connects-to-dimo)
- Ford Pro path: "Connect your Ford Pro account to Navixy via OAuth"; revocable within Ford Pro. [confirmed — Ford Pro account, not FordPass consumer app] — [Navixy Ford Pro integration](https://navixy.com/learn-more/oem-telematics/ford-pro-integration)
- Ford Pro account creation is a business sign-up reviewed by Ford Pro Intelligence Business Center (company name/address required). [confirmed] — [Ford Pro Essentials Job Aid](https://content.fordpro.com/content/dam/fordpro/ca/en-ca/pdf/telematics/Ford-Pro-Telematics-Essentials-Training-Job-Aid.pdf)
- Motorq connects "directly to Ford Pro's telematics platform" for Ford and Lincoln fleets. [confirmed Ford Pro path] — [Motorq Ford page](https://motorq.com/oem-connectivity/ford)
- Insurance path: State Farm Drive Safe & Save Connected Car — "Eligible 2020 Model Year and newer connected Ford and Lincoln vehicles, with the consent of the vehicle owner, can share information directly with State Farm." Launched Feb 2022 in AK/ID/MT/OR. [confirmed] — [State Farm newsroom](https://newsroom.statefarm.com/ford-usage-based-insurance/); Arity/Allstate: Ford provides driving data from activated vehicles to Arity's platform, delivered to insurers via Arity API. [confirmed] — [Digital Insurance](https://www.dig-in.com/news/ford-allstates-arity-enter-ubi-data-partnership); Ford also had direct insurance integrations with State Farm, Allstate, Liberty Mutual and Metromile. [confirmed, 2021] — [Coverager](https://coverager.com/arity-partners-with-ford-to-enable-more-efficient-ubi-offerings/)
- Community Home Assistant integrations state they are "not officially supported by Ford" and warn use "could lead to a (temporary) lock of your FordPass account"; one fork reports a Ford API change broke the original integration (Aug 2024). These store FordPass credentials / use unofficial endpoints — **excluded by project policy**. [confirmed excluded] — [no-gram/ha-fordpass](https://github.com/no-gram/ha-fordpass); [itchannel/fordpass-ha](https://github.com/itchannel/fordpass-ha); [marq24/ha-fordpass](https://gittrend.io/repo/marq24/ha-fordpass)

### Inferences
- Because DIMO's Ford software connection is literally Smartcar Connect, it will fail at the same Ford login step Smartcar is failing at now; it buys nothing except a different billing wrapper. [inferred]
- The Ford Pro OAuth consent is against a Ford Pro (fleet) identity and enrolls VINs under a fleet account; it is a different consent surface than the FordPass consumer OAuth and would not necessarily share the current Ford web-login outage — but this is an inference from the Navixy description, not a tested fact. [inferred]
- Insurance-telematics programs (State Farm, Allstate/Arity, Progressive) consume Ford OEM data via Ford-internal B2B agreements; the owner's consent is given inside the insurer/Ford flow and no owner-facing API exists; their data is driving-behavior/mileage, and none of the sources mention exposing fuel level to the policyholder. [inferred — confirms the "probably not" expectation]
- Progressive's Snapshot OEM work was with GM OnStar; no Ford arrangement found. [inferred from absence] — [Digital Insurance: Progressive–OnStar](https://www.dig-in.com/news/how-the-progressive-onstar-partnership-works)

### Gaps
- Whether the Ford Pro OAuth login shares the same Ford identity provider/outage as the FordPass consumer login is unverified.
- Whether Ford Pro Telematics Essentials (free tier) exposes fuel level in the UI or API, versus only the $20/mo tier ("fuel efficiency analysis" is a paid-tier feature on dealer pages) — not confirmed.

## Key Question 3: IFTTT, Zapier, Make, Home Assistant official partner programs — any official Ford channel?

### Takeaway
No official Ford/FordPass channel exists on IFTTT, Zapier, Make, or Home Assistant; all Home Assistant Ford integrations are community projects that Ford does not support and that risk account lock.

### Cited Findings
- Search of IFTTT/Zapier/Make material returned no Ford or FordPass channel; the only Ford hits were unofficial Home Assistant GitHub projects. [confirmed negative search] — [no-gram/ha-fordpass README (not supported by Ford)](https://github.com/no-gram/ha-fordpass); [itchannel/fordpass-ha](https://github.com/itchannel/fordpass-ha)
- Home Assistant community thread points to developer.ford.com or "the SmartCar API layer" as the official-ish routes. [confirmed] — [HA community: FordPass Integration](https://community.home-assistant.io/t/fordpass-integration/136778)
- marq24/ha-fordpass (last push Apr 24, 2026) and no-gram fork ("FordPass Home Assistant Integration 2025 (EV/PHEV/Petrol/Diesel)") push data via a websocket from Ford backend systems — unofficial. [confirmed, excluded] — [gittrend: marq24/ha-fordpass](https://gittrend.io/repo/marq24/ha-fordpass)
- Ford's 2026 app news is an AI assistant in the FordPass/Lincoln apps hosted on Google Cloud, reaching ~8M vehicles; in-car Gemini arrives 2027. No third-party developer API for it was found. [confirmed, no API] — [TNW: Ford AI assistant rollout](https://thenextweb.com/news/ford-ai-assistant-mobile-app-rollout-google-cloud); [Ford Authority, Dec 2025](https://fordauthority.com/2025/12/ford-exec-says-gemini-ai-assistant-arrives-in-2026/)

### Inferences
- There is no official Ford MCP/LLM integration or automation-platform channel an owner could poll; Ford's AI work is closed inside its own apps. [inferred]

### Gaps
- IFTTT's and Zapier's app directories were not fetched directly (search-level negative only).

## Key Question 4: Apple CarPlay / Android Auto / Google Built-in — any owner-accessible fuel-level API, and is a server-side poller feasible?

### Takeaway
Android Auto's Car App Library exposes fuel level (EnergyLevel listener, API level 3, `CAR_FUEL` permission) but only to an app running on the phone while projected to the head unit, with availability dependent on the OEM head unit; CarPlay exposes no public fuel API. Neither supports a cloud poller for a parked truck.

### Cited Findings
- Android Car App Library: "Starting with Car App API level 3, you can use Car App Library APIs to access vehicle properties and sensors." `CarInfo.addEnergyLevelListener` supplies "Battery level, fuel level, fuel level low, range remaining." Android Auto permission: `com.google.android.gms.permission.CAR_FUEL`; Android Automotive OS: `android.car.permission.CAR_ENERGY`, `CAR_ENERGY_PORTS`, `READ_CAR_DISPLAY_UNITS`; user must grant. "Don't assume that the car's data is available at all times"; sensor availability "might depend on the OEM." Requires `androidx.car.app:app-projected` for Android Auto. [confirmed] — [Android Developers: Car Hardware APIs](https://developer.android.com/training/cars/apps/library/car-hardware-api); [Android Developers Blog, Jul 2021](https://android-developers.googleblog.com/2021/07/accessing-car-hardware-apis-in-your-app.html)
- Ford SYNC 3 vehicles gained Android Auto projection support from MY2017. [confirmed, 2016] — [Android Authority](https://androidauthority.com/ford-announces-android-auto-support-665280)
- CarPlay: an Apple developer-forum reply says a fuel/odometer API is "exactly the sort of API you shouldn't expect in a public SDK" and would be limited to "Automaker Apps"; OEM-only CarPlay vehicle-status apps exist (Porsche, SEAT). [confirmed forum opinion, not Apple doc] — [Apple Developer Forums thread 690092](https://developer.apple.com/forums/thread/690092); [Porsche newsroom PDF](https://newsroom.porsche.com/pdf/35f612a6-f788-45f8-b5ce-28c74a015fe7)
- Third-party App Store "Ford Car Play Connect"-style apps claim to show fuel level but do not document their data source. [unverified claims] — [App Store listing](https://apps.apple.com/us/app/-/id6447566962)

### Inferences
- The Android Auto path is technically official and owner-authorized, but it only works while the phone is in the vehicle and connected; it cannot serve a timer-triggered Azure Function that checks a parked vehicle every 2 hours. At best it could opportunistically push a reading to the backend during drives. [inferred]
- 2021 F-250 (SYNC 3/SYNC 4) and 2021 Expedition (SYNC 3/SYNC 4) are Android Auto projection vehicles, not Google Built-in (Android Automotive OS), so the AAOS permissions do not apply; whether Ford's SYNC head units actually populate the EnergyLevel fuel field over projection is **unverified**. [inferred/unverified]
- Those third-party "Car Play Connect" apps almost certainly use FordPass credentials/unofficial endpoints and should be treated as excluded. [inferred]

### Gaps
- No source confirms Ford SYNC 3/4 populating Android Auto's fuel EnergyLevel.
- No Apple primary documentation retrieved on CarPlay vehicle-data restrictions.

## Key Question 5: For each viable option — onboarding, business-entity requirement, pricing, auth model, polling/webhooks, Ford consent mechanism (summary matrix)

### Takeaway
Only Ford Pro (direct or via a TSP) is a viable non-Smartcar path, and it hinges on obtaining a Ford Pro business account; everything else is either Smartcar-dependent, enterprise-only, defunct, out-of-region, or dongle-based.

### Cited Findings
| Option | Ford ICE fuel in US | Accepts individual? | Pricing | Auth / consent | Freshness / webhooks | Status |
|---|---|---|---|---|---|---|
| Ford Pro Telematics / Data Services (direct) | Yes ("remaining fuel") — [Ford Pro Data Services](https://academyforddealer.worktrucksolutions.com/p/Data-Services) | Business sign-up (company name/address, Business Center review) — [Job Aid](https://content.fordpro.com/content/dam/fordpro/ca/en-ca/pdf/telematics/Ford-Pro-Telematics-Essentials-Training-Job-Aid.pdf) | Essentials free; Telematics $20/VIN/mo (contract) — [Job Aid](https://content.fordpro.com/content/dam/fordpro/ca/en-ca/pdf/telematics/Ford-Pro-Telematics-Essentials-Training-Job-Aid.pdf) | Ford Pro account OAuth; VIN enrolled under fleet — [Navixy](https://navixy.com/learn-more/oem-telematics/ford-pro-integration) | "refresh intervals vary by plan" — [Navixy](https://navixy.com/learn-more/oem-telematics/ford-pro-integration) | Live; MY2020+ |
| Motorq | Yes — [Motorq](https://motorq.com/oem-connectivity/ford) | Fleet sales only; "contact a representative" | Undisclosed subscription | Via Ford Pro platform | "near real-time", no spec | Live, enterprise |
| Navixy (and Geotab/Samsara/Fleetio etc.) | Yes — [Navixy](https://navixy.com/learn-more/oem-telematics/ford-pro-integration) | Fleet platforms; business presumed | Platform subscription + Ford Pro plan | Ford Pro OAuth | Plan-dependent | Live, fleet |
| DIMO (software connection) | Yes via Smartcar — [DIMO docs](https://docs.dimo.org/support/software-connection/smartcar-connection) | Yes (consumer app + developer API) | Not retrieved | FordPass login via Smartcar — same failure | Not retrieved | Live but Smartcar-dependent |
| Car IQ Pay | Fuel level used internally for fleet payments — [Intellishift](https://intellishift.com/?p=13577) | No (fleets; now Element subsidiary) — [Nasdaq](https://www.nasdaq.com/articles/element-acquires-car-iq-launch-first-scaled-vehicle-initiated-payments-platform) | n/a | n/a | n/a | Not a data API |
| CerebrumX | Ford data for insurers — [IoT For All](https://www.iotforall.com/press-releases/cerebrumx-to-use-ford-connected-vehicle-data) | No | n/a | Ford B2B + owner opt-in | n/a | Enterprise |
| Arity / State Farm / Progressive | Driving data only, insurer-facing — [State Farm](https://newsroom.statefarm.com/ford-usage-based-insurance/) | Policyholder opt-in; no API | n/a | Ford–insurer agreement | n/a | No owner API |
| Otonomo (Urgent.ly) | Unknown in 2026 — [10-K FY2024](https://www.sec.gov/Archives/edgar/data/1603652/000095017025039411/uly-20241231.htm) | Unknown | Unknown | Unknown | Unknown | Partly sunset 2023 |
| Wejo | — | — | — | — | — | Defunct (administration Jul 2023) — [Prolific North](https://prolificnorth.co.uk/?p=78907) |
| Caruso | EU only — [GlobeNewswire](https://www.globenewswire.com/news-release/2020/11/12/2125478/0/en/CARUSO-Welcomes-New-Car-Data-Supplier-with-Ford-Europe.html) | — | — | — | — | Out of region |
| Mobilisights | Stellantis only — [Samsara](https://samsara.com/resources/marketplace/free2move) | — | — | — | — | N/A |
| Mojio | Dongle-based — [openHAB](https://community.openhab.org/t/mojio-connected-car/2446) | Yes (consumer dongle) | n/a | n/a | n/a | Excluded (dongle) |
| Vinli | Ford Pro partner, fleet — [Ford Pro partner list](https://academyforddealer.worktrucksolutions.com/p/Data-Services) | No | Unknown | Ford Pro | Unknown | Fleet |
| Android Auto Car App Library | Yes, on-phone only — [Android docs](https://developer.android.com/training/cars/apps/library/car-hardware-api) | Yes (any developer) | Free | User grants `CAR_FUEL` in-car | Only while projected | Not server-pollable |
| CarPlay | No public API — [Apple forum](https://developer.apple.com/forums/thread/690092) | — | — | — | — | N/A |
| IFTTT/Zapier/Make/HA official | None found | — | — | — | — | N/A |

### Inferences
- The single actionable experiment for this project outside Smartcar is: attempt a Ford Pro Telematics Essentials sign-up at fleetaccount.ford.com/customer-signup for the two VINs (possibly as a sole proprietorship), then check whether fuel level is exposed in the free tier or whether the $20/VIN/month Telematics tier (and/or Data Services developer API) is needed. Expected cost if paid: $40/month for two vehicles. [inferred]
- Everything else in the matrix either reduces to Smartcar (DIMO) or is not purchasable by an individual. [inferred]

### Gaps
- Ford Pro Data Services developer-API onboarding steps, pricing, polling/webhook model, and whether a single-vehicle "fleet" is accepted — none publicly documented in sources found.
- DIMO developer pricing/credits and polling intervals were not retrieved (docs.dimo.org pages returned 404/redirect during research).
