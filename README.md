# CivicOps Command

CivicOps is a polished, app-first civic AI platform for AI-powered civic reporting, routing and public alerts. It is designed as a pilot-ready architecture for municipalities, ward offices, civic response teams, NGOs, public utilities or disaster-management-adjacent teams.

---

## 🛰️ NEW: Band Multi-Agent Emergency Dispatch (Band of Agents Hackathon — Track 3)

CivicOps Command now coordinates emergency dispatch with **five specialised AI
agents that communicate _through_ [Band](https://band.ai), the shared agent
interaction layer** — not before or after it. Open **`/Band`** in the running app.

- **IncidentIntakeAgent** — receives a raw report (citizen app, WhatsApp, call
  centre, walk-in), classifies type/severity/area/required resource, and posts
  the structured incident to a per-incident Band room.
- **DispatchCoordinatorAgent** — reads the classified incident from Band, scores
  available units by ETA + skill + workload, proposes the best unit, and **waits
  for a human dispatcher to confirm in the same Band room.**
- **ResourceLogisticsAgent** — works the room in parallel: on a serious incident
  it pre-stages a backup unit and mutual-aid resources, and when the monitor
  escalates an SLA risk it commits that backup — all through Band.
- **ResponseMonitorAgent** — reads the active assignment from Band, monitors GPS +
  the SLA timer, escalates through Band, and closes the incident with an audit
  summary.
- **PublicInfoAgent** — owns the public-facing lane: notifies the reporting
  citizen on dispatch, drafts a public area alert for serious incidents (for human
  approval), and posts a transparent delay notice if the SLA slips.

**The room IS the incident** (`incident id = room id`). Every signal — raw report,
classification, backup staging, unit scoring, the human confirmation, GPS
heartbeats, SLA escalation, citizen alerts, resolution — flows through one shared
Band room that judges can replay end to end in the live **Band Room Viewer**.

```
                         ┌─ ResourceLogisticsAgent: stage backup + mutual aid ─┐
RawReport → Classified → ┤                                                     ├→ 🧑‍✈️ Human confirm →
 (Intake)   (Intake)     └─ DispatchCoordinatorAgent: score units + propose ───┘      (Human)

→ Dispatched → [PublicInfo: notify citizen + draft alert] → Monitored → Escalation → Resolved → Summary
  (Dispatch)                                                 (Monitor)  (Logistics)  (Monitor) (Monitor)
```

### Try it in 30 seconds
1. Run the app, open **`/Band`**.
2. Pick a scenario (e.g. *Structural fire with people trapped*) → **Launch in Band**.
3. Watch all five agents coordinate live; confirm the dispatch yourself
   (uncheck auto-confirm) to see the human-in-the-loop step, or let it auto-drive.
4. Or from a shell: `./scripts/band-demo.sh structure-fire`

Architecture deep-dive: [`docs/band-architecture.md`](docs/band-architecture.md).
Submission text: [`SUBMISSION.md`](SUBMISSION.md).
Judge readiness checklist: [`HACKATHON_READINESS.md`](HACKATHON_READINESS.md).

### Band configuration (`appsettings.json` → `Band`)
```jsonc
"Band": {
  "Mode": "Simulation",        // "Simulation" (in-process, always works) or "Live"
  "BridgeUrl": "http://localhost:8787",  // Node band-bridge sidecar (real SDK)
  "Workspace": "civicops-command",
  "TickSeconds": 2.5           // monitor heartbeat cadence
}
```
In **Simulation** mode the agents coordinate through an in-process model of Band,
so the demo runs with zero external dependencies. In **Live** mode the *same*
agents additionally relay every message to the **`band-bridge/`** Node sidecar,
which publishes them to a hosted Band workspace using the official Band SDK
([`@band-ai/sdk`](https://www.npmjs.com/package/@band-ai/sdk) +
`@thenvoi/rest-client`) — no agent code changes, because each agent is written
against the `IBandTransport` seam. Give each of the five agents its own Band key
(`THENVOI_AGENT_KEYS`) to have them appear as five distinct members in one shared
Band room. See [`band-bridge/`](band-bridge/).

---

## Enterprise Command platform (production blueprint)

The full enterprise Clean Architecture build (ASP.NET Core 8, Domain/Application/
Infrastructure/Api, EF Core + PostgreSQL/TimescaleDB, Redis, SignalR, Docker,
CI/CD) lives in [`enterprise-platform/`](enterprise-platform/). It is the
scale-out target for this same system; the Band coordination layer ports directly
onto it via the identical `IBandTransport` contract.

---

## Core flow

Landing page → report issue → Gemini/fallback AI agent → ticket/reference → dashboard/control room → status lookup → alerts/weather → Citizen App / PWA → connector readiness.

## Primary citizen channels

1. Citizen App / Installable PWA (`/citizen-app` and `/app`)
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

## Deploy

CivicOps Command ships as a single container (.NET 10) that binds to `$PORT` and
exposes `/healthz`. Band runs in Simulation by default, so a fresh deploy shows
the full five-agent dispatch with **zero secrets**.

```bash
# Docker (any host — Render, Railway, Fly.io, Azure, Cloud Run, a VM)
docker build -t civicops-command .
docker run -p 8080:8080 -e PORT=8080 civicops-command   # → http://localhost:8080

# One-click on Render: New → Blueprint → this repo (reads render.yaml)

# Full local stack with LIVE Band (mirrors the transcript to a real Band room):
THENVOI_API_KEY=sk_... docker compose up --build
```

A GitHub Actions workflow (`.github/workflows/dotnet.yml`) builds the app and
asserts a full Band scenario resolves with five collaborating agents on every
push. Full guide: [`docs/deployment.md`](docs/deployment.md).

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

## Citizen App, Android APK and PWA delivery

Primary install routes:

- `/citizen-app` — public Download / Install App hub.
- `/app` — app shell experience for PWA and Android WebView wrapper.
- `/downloads/CivicOpsCitizenCompanion-debug.apk` — exposed only after a real debug APK is copied into `wwwroot/downloads`.

Build Android locally with a machine that has Android SDK + Gradle access:

```bash
cd mobile/CivicOpsAndroid
./gradlew assembleDebug -PcivicopsBaseUrl=https://your-civicops-host.example
./gradlew copyDebugApkToWeb -PcivicopsBaseUrl=https://your-civicops-host.example
```

Expected output:

- Android build output: `mobile/CivicOpsAndroid/app/build/outputs/apk/debug/app-debug.apk`
- Web download artifact: `wwwroot/downloads/CivicOpsCitizenCompanion-debug.apk`

The project includes a GitHub Actions workflow at `.github/workflows/android-apk.yml` that installs Java/Android tooling, builds the debug APK and uploads it as an artifact. The Android shell is intentionally API-backed: it loads `/app`, keeps Gemini keys on the server, and relies on CivicOps backend routes rather than duplicating civic routing logic on-device.

## Premium overhaul highlights

- Public IA now prioritizes Home, Report Issue, Track Report, Area Alerts, Citizen App, AI Assistant, Sign In and subtle Staff Login.
- The homepage uses a darker control-room visual identity with a citizen product face and a staff operations face.
- The Citizen App hub explains PWA install, APK output, demo login, API-backed architecture, followed areas, Copilot and community threads.
- Seed logic maintains 100+ synthetic eThekwini incident reports and expands alerts across water, electricity, roads, waste, disaster, safety and environmental scenarios.
- Gemini model allocation is explicit: Gemini 3.1 Flash Lite for routine classification/extraction, Gemini 2.5 Flash for richer summaries/briefs/health actions, deterministic fallback for reliability.
