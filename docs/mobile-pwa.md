# CivicOps Mobile/PWA

CivicOps is PWA-first. The primary citizen channel is the **CivicOps Mobile Citizen App** at `/Home/Mobile` and `/app`.

Working mobile flows:
- Report issue: `/Home/Report`
- Track reference: `/Home/Lookup`
- My reports/profile: `/Resident/MyReports`, `/Resident/Profile`
- Area alerts: `/Home/Alerts`
- Weather / area conditions: `/Home/Weather`
- Optional WhatsApp connector info: `/Demo/WhatsAppSimulator`

PWA assets:
- `wwwroot/manifest.json`
- `wwwroot/service-worker.js`
- `wwwroot/icons/civicops-icon.svg`
- Service worker registration in `wwwroot/js/site.js`

Opening the Citizen App does **not** call Gemini. AI runs only on report submission or explicit staff/judge actions.

## Final submission positioning

- Citizen App / Installable PWA is the main public channel. Reports, tracking, My Reports, Area Alerts, Weather/Area Risk, Follow My Area and Profile work without WhatsApp.
- Gemini is the civic AI agent layer for event-triggered enrichment only: report submission, voice-note transcript analysis, optional WhatsApp inbound processing, explicit AI Agent/staff/judge action, alert recommendation and department brief generation.
- Gemini/fallback cleans messy descriptions, corrects common area spelling such as Chatworth→Chatsworth and Pheonix→Phoenix, normalizes eThekwini demo suburbs, estimates synthetic wards where available, and flags “Needs ward confirmation” when uncertain.
- Department users see only incidents assigned to their department; admins and dispatchers can see all queues.
- The platform uses synthetic eThekwini scenario data and does not claim live municipal data, official municipal partnership, emergency-service replacement or production WhatsApp approval.
- WhatsApp is optional connector-ready only for future pilots/live-test messaging.
- Local deterministic fallback keeps classification, routing, citizen response, department brief and alert recommendations working if Gemini is disabled, quota-limited or missing a key.
- Production would require real identity, municipal integrations, privacy/security hardening, approved communication channels and authoritative GIS/ward data.

## APK status

Installable PWA is ready as the main public channel. An optional APK/WebView wrapper may be added or built by CI only when the wrapper exists and produces an actual artifact; UI should show a Download APK button only when an APK file is present.


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
