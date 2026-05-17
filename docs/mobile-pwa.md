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
