# CivicOps

CivicOps is a polished, app-first civic AI platform for AI-powered civic reporting, routing and public alerts. It is designed as a pilot-ready architecture for municipalities, ward offices, civic response teams, NGOs, public utilities or disaster-management-adjacent teams.

## Core flow

Landing page → report issue → Gemini/fallback AI agent → ticket/reference → dashboard/control room → status lookup → alerts/weather → Citizen App / PWA → connector readiness.

## Primary citizen channels

1. Citizen App / Installable PWA (`/Home/Mobile` and `/app`)
2. Web reporting portal (`/Home/Report`)
3. Public reference/status lookup (`/Home/Lookup`)
4. Area alerts/weather notices (`/Home/Alerts`, `/Home/Weather`)
5. Optional WhatsApp connector-ready integration (`/Demo/WhatsAppSimulator`)

WhatsApp Cloud API is connector-ready for sandbox/live-test and future production pilots. CivicOps does not depend on WhatsApp. Residents can report, track, and receive alerts through the Citizen App / PWA and web portal.

## Gemini AI agent layer

Gemini is openly embedded but event/action-triggered and quota-safe. Gemini does **not** run on startup, public page load, dashboard load, connector page load, weather/alerts page load, Citizen App opening, refreshes, background timers or smoke tests.

Gemini may run only when a resident report is submitted, a voice-note transcript is analyzed, an optional WhatsApp inbound report is processed, or staff/judges click an AI Agent action.

Default configuration:

```text
GEMINI_ENABLED=false
GEMINI_MODEL=gemini-2.5-flash
GEMINI_ROUTINE_MODEL=gemini-3.1-flash-lite
GEMINI_FALLBACK_MODELS=gemini-3.1-flash-lite,gemini-2.5-flash-lite,gemini-2.0-flash-lite,gemini-2.0-flash
GEMINI_AUTO_RUN_AGENT_PAGE=false
GEMINI_MANUAL_TEST_COOLDOWN_SECONDS=60
GEMINI_QUOTA_COOLDOWN_MINUTES=30
GEMINI_MODE=Hybrid
```

Do not commit `GEMINI_API_KEY`, WhatsApp tokens, phone numbers or credentials.

## Judge route

Open `/Home/DemoTour` and follow the 3–5 minute route: home, report, Citizen App, AI Agent, dashboard, lookup, alerts/weather, optional WhatsApp sandbox, connector readiness and Bob evidence.

## Local verification

```bash
dotnet restore
dotnet build
dotnet run
./scripts/smoke-test.sh http://localhost:5000
./scripts/api-check.sh http://localhost:5000
```

The scripts are designed to pass in fallback/sandbox mode and do not require live Gemini or WhatsApp credentials.

## Safety and honesty

CivicOps uses synthetic civic data for sandbox scenarios. It does not claim official municipal partnerships, does not replace emergency services, and keeps humans in the loop for dispatch or public alert decisions.

## IBM Bob evidence

IBM Bob was used to build and accelerate the main CivicOps hackathon implementation. Preserved evidence docs include:

- `docs/bob-report.md`
- `docs/build-log.md`
- `docs/ibm-bob-session-report.md`
- `docs/ibm-bob-final-continuity-report.md`
- `docs/evidence/`

Final engineering polish may have been completed after Bob and is not falsely claimed as Bob work.

## Final submission positioning

- Citizen App / Installable PWA is the main public channel. Reports, tracking, My Reports, Area Alerts, Weather/Area Risk, Follow My Area and Profile work without WhatsApp.
- Gemini is the civic AI agent layer for event-triggered enrichment only: report submission, voice-note transcript analysis, optional WhatsApp inbound processing, explicit AI Agent/staff/judge action, alert recommendation and department brief generation.
- Gemini/fallback cleans messy descriptions, corrects common area spelling such as Chatworth→Chatsworth and Pheonix→Phoenix, normalizes eThekwini demo suburbs, estimates synthetic wards where available, and flags “Needs ward confirmation” when uncertain.
- Department users see only incidents assigned to their department; admins and dispatchers can see all queues.
- The platform uses synthetic eThekwini scenario data and does not claim live municipal data, official municipal partnership, emergency-service replacement or production WhatsApp approval.
- WhatsApp is optional connector-ready only for future pilots/live-test messaging.
- Local deterministic fallback keeps classification, routing, citizen response, department brief and alert recommendations working if Gemini is disabled, quota-limited or missing a key.
- Production would require real identity, municipal integrations, privacy/security hardening, approved communication channels and authoritative GIS/ward data.


## Final CivicOps submission QA notes

- Citizen App / Installable PWA (`/app`) is the main public channel for reporting, tracking references, My Reports, Area Alerts, Weather/Area Risk, followed suburbs/wards, Gemini Copilot actions and lightweight Community Threads.
- Gemini is the civic AI agent layer and runs server-side only from explicit app/staff/judge actions: report submission, Copilot/AI Agent button click, voice-note transcript analysis, optional WhatsApp sandbox processing, generated citizen response, department brief or alert recommendation.
- Gemini/fallback enrichment cleans messy report text, corrects common area spellings such as Chatworth to Chatsworth and Pheonix to Phoenix, assigns category/department/priority, creates citizen responses and department briefs, and records audit notes.
- Ward values are synthetic estimates for the eThekwini scenario. If a ward cannot be inferred, CivicOps must show a ward estimate or Needs ward confirmation rather than pretending certainty.
- Department responders see only their own queues; Admin and Dispatcher can see broader operational views.
- Community Threads are lightweight local confirmation/update areas, not a full social network.
- The data set is synthetic eThekwini scenario data for hackathon judging. CivicOps does not claim live municipal data, an official municipal partnership, emergency-service replacement, or production WhatsApp approval.
- WhatsApp is optional connector-ready only for future pilots/live-test messaging; the Citizen App/PWA is the primary demo path.
- Production requires real identity, municipal integrations, privacy/security hardening, approved communications channels, and authoritative GIS/ward data.
- CivicOps was built with IBM Bob assistance and finalized into a working hackathon submission with verification, packaging and polish.
